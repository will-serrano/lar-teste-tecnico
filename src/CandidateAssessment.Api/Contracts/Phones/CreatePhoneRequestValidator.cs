using FluentValidation;

namespace CandidateAssessment.Api.Contracts.Phones;

internal sealed class CreatePhoneRequestValidator : AbstractValidator<CreatePhoneRequest>
{
    public CreatePhoneRequestValidator()
    {
        RuleFor(r => r.Type)
            .IsInEnum()
            .WithMessage("PhoneType must be a valid value.");

        RuleFor(r => r.Number)
            .NotEmpty()
            .WithMessage("Number is required.")
            .Must(HaveOnlyAllowedCharacters)
            .WithMessage("Number must contain only digits, spaces, '(', ')', '+', or '-'.");

        RuleFor(r => r)
            .Must(r => CandidateAssessment.Domain.ValueObjects.PhoneNumber.TryCreate(
                r.Number,
                r.Type,
                out _))
            .WithMessage(r => $"Number is not valid for type {r.Type}.")
            .When(r => r.Type != 0);
    }

    private static bool HaveOnlyAllowedCharacters(string? number)
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
