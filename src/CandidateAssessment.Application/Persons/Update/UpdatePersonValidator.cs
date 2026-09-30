using CandidateAssessment.Domain.Entities;
using FluentValidation;

namespace CandidateAssessment.Application.Persons.Update;

public sealed class UpdatePersonValidator : AbstractValidator<UpdatePersonCommand>
{
    public UpdatePersonValidator()
    {
        RuleFor(c => c.Id)
            .NotEqual(Guid.Empty)
            .WithMessage("Id is required.");

        RuleFor(c => c.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(Person.MaxNameLength)
            .WithMessage($"Name cannot exceed {Person.MaxNameLength} characters.");

        RuleFor(c => c.BirthDate)
            .NotEqual(default(DateOnly))
            .WithMessage("BirthDate is required.");
    }
}
