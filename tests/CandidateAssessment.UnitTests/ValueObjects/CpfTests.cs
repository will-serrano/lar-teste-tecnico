using CandidateAssessment.Domain.Exceptions;
using CandidateAssessment.Domain.ValueObjects;
using Xunit;

namespace CandidateAssessment.UnitTests.ValueObjects;

public class CpfTests
{
    [Theory]
    [InlineData("123.456.789-09", "12345678909")]
    [InlineData("12345678909", "12345678909")]
    [InlineData("  123.456.789-09  ", "12345678909")]
    public void Create_ShouldNormalizeValidCpf(string input, string expected)
    {
        var cpf = Cpf.Create(input);

        Assert.Equal(expected, cpf.Value);
    }

    [Fact]
    public void Format_ShouldProduceMaskedCpf()
    {
        var cpf = Cpf.Create("12345678909");

        Assert.Equal("123.456.789-09", cpf.Format());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_ShouldThrow_WhenInputIsEmpty(string? input)
    {
        Assert.Throws<DomainException>(() => Cpf.Create(input));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    [InlineData("abc")]
    [InlineData("123.456.789-0a")]
    public void Create_ShouldThrow_WhenLengthIsNot11(string input)
    {
        Assert.Throws<DomainException>(() => Cpf.Create(input));
    }

    [Theory]
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    [InlineData("22222222222")]
    [InlineData("33333333333")]
    [InlineData("44444444444")]
    [InlineData("55555555555")]
    [InlineData("66666666666")]
    [InlineData("77777777777")]
    [InlineData("88888888888")]
    [InlineData("99999999999")]
    public void Create_ShouldThrow_WhenAllDigitsAreEqual(string input)
    {
        Assert.Throws<DomainException>(() => Cpf.Create(input));
    }

    [Theory]
    [InlineData("12345678900")]
    [InlineData("52998224724")]
    [InlineData("52998224726")]
    public void Create_ShouldThrow_WhenCheckDigitsAreInvalid(string input)
    {
        Assert.Throws<DomainException>(() => Cpf.Create(input));
    }

    [Fact]
    public void TryCreate_ShouldReturnFalse_WhenInvalid()
    {
        var result = Cpf.TryCreate("not-a-cpf", out var cpf);

        Assert.False(result);
        Assert.Null(cpf);
    }

    [Fact]
    public void TryCreate_ShouldReturnTrue_WhenValid()
    {
        var result = Cpf.TryCreate("123.456.789-09", out var cpf);

        Assert.True(result);
        Assert.NotNull(cpf);
        Assert.Equal("12345678909", cpf.Value);
    }

    [Fact]
    public void Equality_ShouldBeBasedOnValue()
    {
        var a = Cpf.Create("12345678909");
        var b = Cpf.Create("123.456.789-09");

        Assert.Equal(a, b);
    }
}
