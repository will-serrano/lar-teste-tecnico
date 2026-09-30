using CandidateAssessment.Domain.Enums;
using CandidateAssessment.Domain.Exceptions;
using CandidateAssessment.Domain.ValueObjects;
using Xunit;

namespace CandidateAssessment.UnitTests.ValueObjects;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("(13) 99999-9999", PhoneType.Mobile, "13999999999")]
    [InlineData("13999999999", PhoneType.Mobile, "13999999999")]
    [InlineData("(11) 3333-4444", PhoneType.Residential, "1133334444")]
    [InlineData("1133334444", PhoneType.Residential, "1133334444")]
    [InlineData("(11) 3333-4444", PhoneType.Commercial, "1133334444")]
    [InlineData("(11) 99999-9999", PhoneType.Commercial, "11999999999")]
    public void Create_ShouldNormalizeValidPhone(string input, PhoneType type, string expected)
    {
        var phone = PhoneNumber.Create(input, type);

        Assert.Equal(expected, phone.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_ShouldThrow_WhenInputIsEmpty(string? input)
    {
        Assert.Throws<DomainException>(() => PhoneNumber.Create(input, PhoneType.Mobile));
    }

    [Theory]
    [InlineData("123", PhoneType.Mobile)]
    [InlineData("1399999999", PhoneType.Mobile)]
    [InlineData("139999999999", PhoneType.Mobile)]
    [InlineData("113333", PhoneType.Residential)]
    [InlineData("113333444455", PhoneType.Residential)]
    [InlineData("113333", PhoneType.Commercial)]
    public void Create_ShouldThrow_WhenLengthIsInvalidForType(string input, PhoneType type)
    {
        Assert.Throws<DomainException>(() => PhoneNumber.Create(input, type));
    }

    [Theory]
    [InlineData("abc", PhoneType.Mobile)]
    [InlineData("(11) 9999a-9999", PhoneType.Mobile)]
    public void Create_ShouldThrow_WhenContainsNonDigits(string input, PhoneType type)
    {
        Assert.Throws<DomainException>(() => PhoneNumber.Create(input, type));
    }

    [Fact]
    public void Equality_ShouldBeBasedOnValue()
    {
        var a = PhoneNumber.Create("(13) 99999-9999", PhoneType.Mobile);
        var b = PhoneNumber.Create("13999999999", PhoneType.Mobile);

        Assert.Equal(a, b);
    }
}
