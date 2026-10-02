using FluentValidation;

namespace CandidateAssessment.Api.Contracts.Phones;

internal sealed class UpdatePhoneRequestValidator : AbstractValidator<UpdatePhoneRequest>
{
    public UpdatePhoneRequestValidator()
    {
        RuleFor(r => r.Type)
            .IsInEnum()
            .WithMessage("PhoneType must be a valid value.");

        RuleFor(r => r.Number)
            .NotEmpty()
            .WithMessage("Number is required.");

        RuleFor(r => r)
            .Must(r => CandidateAssessment.Domain.ValueObjects.PhoneNumber.TryCreate(
                r.Number,
                r.Type,
                out _))
            .WithMessage(r => $"Number is not valid for type {r.Type}.")
            .When(r => r.Type != 0);
    }
}
