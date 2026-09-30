using CandidateAssessment.Application.Persons.Search;
using FluentValidation.TestHelper;
using Xunit;

namespace CandidateAssessment.UnitTests.Persons.Search;

public class SearchPersonsValidatorTests
{
    private readonly SearchPersonsValidator _validator = new();

    [Fact]
    public void Should_HaveError_When_PageIsZero()
    {
        var query = new SearchPersonsQuery(null, null, 0, null);
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.Page);
    }

    [Fact]
    public void Should_HaveError_When_PageSizeExceedsMax()
    {
        var query = new SearchPersonsQuery(null, null, 1, 500);
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.PageSize);
    }

    [Fact]
    public void Should_NotHaveErrors_When_AllFieldsAreValid()
    {
        var query = new SearchPersonsQuery("maria", "12345678909", 1, 20);
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_NotHaveErrors_When_AllFieldsAreNull()
    {
        var query = new SearchPersonsQuery(null, null, null, null);
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }
}
