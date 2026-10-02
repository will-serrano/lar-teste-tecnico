using FluentValidation;

namespace CandidateAssessment.Api.Contracts.Persons;

internal sealed class CreatePersonRequestValidator : AbstractValidator<CreatePersonRequest>
{
    public CreatePersonRequestValidator()
    {
        RuleFor(r => r.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(100)
            .WithMessage("Name cannot exceed 100 characters.");

        RuleFor(r => r.Cpf)
            .NotEmpty()
            .WithMessage("Cpf is required.")
            .Must(HaveAtLeast11Digits)
            .WithMessage("Cpf must contain 11 digits.");

        RuleFor(r => r.BirthDate)
            .NotEqual(default(DateOnly))
            .WithMessage("BirthDate is required.");
    }

    private static bool HaveAtLeast11Digits(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
        {
            return false;
        }

        var digitCount = 0;
        foreach (var ch in cpf)
        {
            if (char.IsDigit(ch))
            {
                digitCount++;
            }
        }

        return digitCount == 11;
    }
}
