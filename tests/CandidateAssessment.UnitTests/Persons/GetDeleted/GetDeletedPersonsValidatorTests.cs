using CandidateAssessment.Application.Persons.GetDeleted;
using FluentValidation.TestHelper;

namespace CandidateAssessment.UnitTests.Persons.GetDeleted;

public class GetDeletedPersonsValidatorTests
{
    private readonly GetDeletedPersonsValidator _validator = new();

    [Fact]
    public void Should_HaveError_When_PageIsZero()
    {
        var query = new GetDeletedPersonsQuery(0, null);
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.Page);
    }

    [Fact]
    public void Should_HaveError_When_PageIsNegative()
    {
        var query = new GetDeletedPersonsQuery(-1, null);
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.Page);
    }

    [Fact]
    public void Should_HaveError_When_PageSizeIsZero()
    {
        var query = new GetDeletedPersonsQuery(null, 0);
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.PageSize);
    }

    [Fact]
    public void Should_HaveError_When_PageSizeExceedsMax()
    {
        var query = new GetDeletedPersonsQuery(null, 500);
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.PageSize);
    }

    [Fact]
    public void Should_NotHaveErrors_When_PageAndPageSizeAreValid()
    {
        var query = new GetDeletedPersonsQuery(1, 20);
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_NotHaveErrors_When_AllFieldsAreNull()
    {
        var query = new GetDeletedPersonsQuery(null, null);
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_NotHaveErrors_When_PageSizeIsAtMax()
    {
        var query = new GetDeletedPersonsQuery(1, 100);
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }
}
