using CandidateAssessment.Application.Persons.Create;
using FluentValidation.TestHelper;

namespace CandidateAssessment.UnitTests.Persons.Create;

public class CreatePersonValidatorTests
{
    private readonly CreatePersonValidator _validator = new();

    [Fact]
    public void Should_HaveError_When_NameIsEmpty()
    {
        var command = BuildValidCommand() with { Name = string.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Should_HaveError_When_NameExceedsMaxLength()
    {
        var command = BuildValidCommand() with { Name = new string('a', 101) };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Should_HaveError_When_CpfIsEmpty()
    {
        var command = BuildValidCommand() with { Cpf = string.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.Cpf);
    }

    [Fact]
    public void Should_HaveError_When_CpfContainsLetters()
    {
        var command = BuildValidCommand() with { Cpf = "abc45678909" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.Cpf);
    }

    [Fact]
    public void Should_HaveError_When_CpfDoesNotHave11Digits()
    {
        var command = BuildValidCommand() with { Cpf = "12345" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.Cpf);
    }

    [Fact]
    public void Should_HaveError_When_BirthDateIsDefault()
    {
        var command = BuildValidCommand() with { BirthDate = default };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.BirthDate);
    }

    [Fact]
    public void Should_NotHaveErrors_When_CommandIsValid()
    {
        var result = _validator.TestValidate(BuildValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static CreatePersonCommand BuildValidCommand() => new(
        Name: "Maria",
        Cpf: "123.456.789-09",
        BirthDate: new DateOnly(1990, 1, 1));
}
