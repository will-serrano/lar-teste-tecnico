using CandidateAssessment.Application.Persons.Update;
using FluentValidation.TestHelper;

namespace CandidateAssessment.UnitTests.Persons.Update;

public class UpdatePersonValidatorTests
{
    private readonly UpdatePersonValidator _validator = new();

    [Fact]
    public void Should_HaveError_When_IdIsEmpty()
    {
        var command = BuildValidCommand() with { Id = Guid.Empty };
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Id);
    }

    [Fact]
    public void Should_HaveError_When_NameIsBlank()
    {
        var command = BuildValidCommand() with { Name = string.Empty };
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Should_HaveError_When_BirthDateIsDefault()
    {
        var command = BuildValidCommand() with { BirthDate = default };
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.BirthDate);
    }

    [Fact]
    public void Should_NotHaveErrors_When_CommandIsValid()
    {
        _validator.TestValidate(BuildValidCommand()).ShouldNotHaveAnyValidationErrors();
    }

    private static UpdatePersonCommand BuildValidCommand() => new(
        Id: Guid.NewGuid(),
        Name: "Maria",
        BirthDate: new DateOnly(1990, 1, 1));
}
