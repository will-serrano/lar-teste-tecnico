using CandidateAssessment.Domain.Enums;
using CandidateAssessment.Domain.ValueObjects;
using FluentValidation;

namespace CandidateAssessment.Application.Phones.Create;

public sealed class CreatePhoneValidator : AbstractValidator<CreatePhoneCommand>
{
    public CreatePhoneValidator()
    {
        RuleFor(c => c.PersonId)
            .NotEqual(Guid.Empty)
            .WithMessage("PersonId is required.");

        RuleFor(c => c.Type)
            .IsInEnum()
            .WithMessage("PhoneType must be a valid value.");

        RuleFor(c => c.Number)
            .NotEmpty()
            .WithMessage("Number is required.")
            .Must(BeDigitsOrFormatted)
            .WithMessage("Number must contain only digits, spaces, '(', ')', '+', or '-'.");

        RuleFor(c => c)
            .Must(c => PhoneNumber.TryCreate(c.Number, c.Type, out _))
            .WithMessage(c => $"Number is not valid for type {c.Type}.")
            .When(c => c.Type != 0);
    }

    private static bool BeDigitsOrFormatted(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return false;
        }

        foreach (var ch in number)
        {
            var allowed = char.IsDigit(ch)
                || char.IsWhiteSpace(ch)
                || ch == '('
                || ch == ')'
                || ch == '+'
                || ch == '-';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }
}
