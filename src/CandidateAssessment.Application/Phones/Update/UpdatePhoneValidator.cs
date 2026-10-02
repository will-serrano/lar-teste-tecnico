using CandidateAssessment.Domain.ValueObjects;
using FluentValidation;

namespace CandidateAssessment.Application.Phones.Update;

public sealed class UpdatePhoneValidator : AbstractValidator<UpdatePhoneCommand>
{
    public UpdatePhoneValidator()
    {
        RuleFor(c => c.PersonId)
            .NotEqual(Guid.Empty)
            .WithMessage("PersonId is required.");

        RuleFor(c => c.PhoneId)
            .NotEqual(Guid.Empty)
            .WithMessage("PhoneId is required.");

        RuleFor(c => c.Type)
            .IsInEnum()
            .WithMessage("PhoneType must be a valid value.");

        RuleFor(c => c.Number)
            .NotEmpty()
            .WithMessage("Number is required.");

        RuleFor(c => c)
            .Must(c => PhoneNumber.TryCreate(c.Number, c.Type, out _))
            .WithMessage(c => $"Number is not valid for type {c.Type}.")
            .When(c => c.Type != 0);
    }
}
