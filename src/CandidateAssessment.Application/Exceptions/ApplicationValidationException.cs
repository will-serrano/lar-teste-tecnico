using FluentValidation.Results;

namespace CandidateAssessment.Application.Exceptions;

/// <summary>
/// Exceção lançada pelos casos de uso da camada Application quando uma entrada não atende às
/// regras de validação dessa camada (por exemplo, CPF já utilizado ou recurso não encontrado).
/// Convertida em ProblemDetails (400/404/409) pelo middleware de exceções da API.
/// </summary>
public class ApplicationValidationException : Exception
{
    public IReadOnlyList<ApplicationValidationError> Errors { get; }

    public ApplicationValidationException(IReadOnlyList<ApplicationValidationError> errors)
        : base("One or more validation errors occurred.")
    {
        if (errors.Count == 0)
        {
            throw new ArgumentException(
                "ApplicationValidationException requires at least one error.",
                nameof(errors));
        }

        Errors = errors;
    }

    public ApplicationValidationException(string code, string message)
        : this(new[] { new ApplicationValidationError(code, message) })
    {
    }

    public static ApplicationValidationException FromFluentValidation(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var errors = result.Errors
            .Select(e => new ApplicationValidationError(
                string.IsNullOrWhiteSpace(e.ErrorCode) ? "ValidationError" : e.ErrorCode,
                e.ErrorMessage))
            .ToList();

        return new ApplicationValidationException(errors);
    }
}

public sealed record ApplicationValidationError(string Code, string Message);
