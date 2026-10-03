using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace CandidateAssessment.Api.Validation;

/// <summary>
/// Global action filter that runs every registered <see cref="IValidator{T}"/>
/// for each action argument before the action executes, so controllers do not
/// have to inject validators, build queries, or copy error messages into
/// <c>ModelState</c> by hand.
///
/// Resolution is opt-in by type: an argument is only validated when a closed
/// <c>IValidator&lt;T&gt;</c> for its runtime type is registered in the DI
/// container. This keeps the filter side-effect-free for framework-level
/// arguments (route ids, <see cref="CancellationToken"/>, etc.) and makes
/// adding a new endpoint automatic — register the validator and validation
/// "just works".
///
/// On failure, the filter populates the controller's <c>ModelState</c> with
/// a key per property (falling back to <c>"request"</c>/<c>"query"</c>) and
/// short-circuits with a <c>ValidationProblemDetails</c> 400 response, matching
/// the existing manual behavior in every controller.
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
        // Resolve the closed-generic IValidator<T>.ValidateAsync(T, CancellationToken)
        // method by looking it up on the interface type, not on the concrete class.
        // Looking on the concrete class with signature (object, CancellationToken)
        // would silently return null and throw — IValidator<T> declares (T, ct),
        // not (object, ct), and T is the runtime argument type.
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
        // Skip primitive/framework types we never want to validate: route ids,
        // CancellationToken, FormFile streams, etc. Anything without a registered
        // IValidator<T> is already ignored; this is a fast-path to avoid reflection
        // for the most common cases.
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