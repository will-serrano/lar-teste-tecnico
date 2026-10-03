using FluentValidation;
using CandidateAssessment.Api.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace CandidateAssessment.Api.Validation;

/// <summary>
/// Filtro global de ação que executa cada <see cref="IValidator{T}"/> registrado
/// para cada argumento da ação antes de sua execução, evitando que os controllers
/// precisem injetar validadores, criar consultas ou copiar mensagens de erro para
/// <c>ModelState</c> manualmente.
///
/// A resolução é opt-in por tipo: um argumento só é validado quando um
/// <c>IValidator&lt;T&gt;</c> fechado para seu tipo em tempo de execução está registrado
/// no contêiner de DI. Isso mantém o filtro sem efeitos colaterais para argumentos
/// do framework (IDs de rota, <see cref="CancellationToken"/> etc.) e automatiza
/// a validação de novos endpoints — basta registrar o validador.
///
/// Em caso de falha, o filtro preenche o <c>ModelState</c> do controller com
/// uma chave por propriedade (usando <c>"request"</c>/<c>"query"</c> como alternativa)
/// e interrompe a execução com uma resposta 400 do tipo <c>ValidationProblemDetails</c>,
/// mantendo o comportamento manual existente em todos os controllers.
/// </summary>
public sealed class ValidationActionFilter : IAsyncActionFilter
{
    private readonly ProblemDetailsFactory _problemDetailsFactory;

    public ValidationActionFilter(ProblemDetailsFactory problemDetailsFactory)
    {
        _problemDetailsFactory = problemDetailsFactory;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.ActionArguments.Count == 0)
        {
            await next();
            return;
        }

        foreach (var (name, value) in context.ActionArguments)
        {
            if (value is null || IsFrameworkType(value.GetType()))
            {
                continue;
            }

            var validator = ResolveValidator(context.HttpContext.RequestServices, value.GetType());
            if (validator is null)
            {
                continue;
            }

            var validation = await InvokeValidateAsync(validator, value.GetType(), value, context.HttpContext.RequestAborted);
            if (validation.IsValid)
            {
                continue;
            }

            foreach (var error in validation.Errors)
            {
                var key = string.IsNullOrEmpty(error.PropertyName)
                    ? name
                    : error.PropertyName;
                context.ModelState.AddModelError(key, error.ErrorMessage);
            }
        }

        if (!context.ModelState.IsValid)
        {
            var problem = _problemDetailsFactory.CreateValidationProblemDetails(
                context.HttpContext,
                context.ModelState,
                statusCode: StatusCodes.Status400BadRequest);
            problem.Extensions["traceId"] = RequestCorrelation.GetId(context.HttpContext);
            context.Result = new BadRequestObjectResult(problem);
            return;
        }

        await next();
    }

    private static object? ResolveValidator(IServiceProvider services, Type argumentType)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);
        return services.GetService(validatorType);
    }

    private static Task<FluentValidation.Results.ValidationResult> InvokeValidateAsync(
        object validator,
        Type argumentType,
        object instance,
        CancellationToken cancellationToken)
    {
        // Resolve o método genérico fechado IValidator<T>.ValidateAsync(T, CancellationToken)
        // procurando-o no tipo da interface, não na classe concreta.
        // Procurar na classe concreta pela assinatura (object, CancellationToken)
        // retornaria null silenciosamente e causaria uma exceção — IValidator<T> declara
        // (T, ct), não (object, ct), e T é o tipo do argumento em tempo de execução.
        var validatorInterface = typeof(IValidator<>).MakeGenericType(argumentType);
        var method = validatorInterface.GetMethod(
            nameof(IValidator<object>.ValidateAsync),
            new[] { argumentType, typeof(CancellationToken) })
            ?? throw new InvalidOperationException(
                $"Validator interface '{validatorInterface.FullName}' does not expose ValidateAsync({argumentType.Name}, CancellationToken).");

        return (Task<FluentValidation.Results.ValidationResult>)method.Invoke(
            validator,
            new[] { instance, cancellationToken })!;
    }

    private static bool IsFrameworkType(Type type)
    {
        // Ignora tipos primitivos/do framework que não devem ser validados: IDs de rota,
        // CancellationToken, streams FormFile etc. Tipos sem um IValidator<T> registrado
        // já são ignorados; este atalho evita reflexão nos casos mais comuns.
        if (type.IsPrimitive || type.IsEnum)
        {
            return true;
        }

        if (type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset)
            || type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(TimeSpan) || type == typeof(Guid)
            || type == typeof(CancellationToken))
        {
            return true;
        }

        var nullableUnderlying = Nullable.GetUnderlyingType(type);
        if (nullableUnderlying is not null && IsFrameworkType(nullableUnderlying))
        {
            return true;
        }

        return false;
    }
}
