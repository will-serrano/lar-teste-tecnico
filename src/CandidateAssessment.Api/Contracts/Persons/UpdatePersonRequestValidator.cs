using FluentValidation;

namespace CandidateAssessment.Api.Contracts.Persons;

internal sealed class UpdatePersonRequestValidator : AbstractValidator<UpdatePersonRequest>
{
    public UpdatePersonRequestValidator()
    {
        RuleFor(r => r.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(100)
            .WithMessage("Name cannot exceed 100 characters.");

        RuleFor(r => r.BirthDate)
            .NotEqual(default(DateOnly))
            .WithMessage("BirthDate is required.");
    }
}
