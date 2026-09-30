using CandidateAssessment.Domain.Entities;
using FluentValidation;

namespace CandidateAssessment.Application.Persons.Create;

public sealed class CreatePersonValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(Person.MaxNameLength)
            .WithMessage($"Name cannot exceed {Person.MaxNameLength} characters.");

        RuleFor(c => c.Cpf)
            .NotEmpty()
            .WithMessage("Cpf is required.")
            .Must(BeCpfLike)
            .WithMessage("Cpf must contain 11 digits.");

        RuleFor(c => c.BirthDate)
            .NotEqual(default(DateOnly))
            .WithMessage("BirthDate is required.");
    }

    private static bool BeCpfLike(string? cpf)
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
            else if (!char.IsWhiteSpace(ch)
                && ch != '.'
                && ch != '-')
            {
                return false;
            }
        }

        return digitCount == 11;
    }
}
