using CandidateAssessment.Domain.Entities;
using CandidateAssessment.Domain.Enums;
using CandidateAssessment.Domain.Exceptions;
using CandidateAssessment.Domain.ValueObjects;
using Xunit;

namespace CandidateAssessment.UnitTests.Entities;

public class PersonTests
{
    private static readonly DateTime FixedNow =
        new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static Person BuildPerson(DateTime? now = null)
        => Person.Create(
            "João da Silva",
            Cpf.Create("12345678909"),
            new DateOnly(1990, 5, 20),
            now ?? FixedNow);

    [Fact]
    public void Create_ShouldGenerateIdAndSetAuditFields()
    {
        var person = BuildPerson();

        Assert.NotEqual(Guid.Empty, person.Id);
        Assert.True(person.IsActive);
        Assert.Equal(FixedNow, person.CreatedAtUtc);
        Assert.Equal(FixedNow, person.UpdatedAtUtc);
        Assert.Null(person.DeletedAtUtc);
        Assert.Null(person.RestoredAtUtc);
    }

    [Fact]
    public void Create_ShouldNormalizeName()
    {
        var person = Person.Create(
            "  Maria  ",
            Cpf.Create("12345678909"),
            new DateOnly(1990, 1, 1),
            FixedNow);

        Assert.Equal("Maria", person.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_ShouldThrow_WhenNameIsBlank(string? name)
    {
        Assert.Throws<DomainException>(() =>
            Person.Create(
                name!,
                Cpf.Create("12345678909"),
                new DateOnly(1990, 1, 1),
                FixedNow));
    }

    [Fact]
    public void Create_ShouldThrow_WhenNameExceedsMaxLength()
    {
        var longName = new string('a', Person.MaxNameLength + 1);

        Assert.Throws<DomainException>(() =>
            Person.Create(
                longName,
                Cpf.Create("12345678909"),
                new DateOnly(1990, 1, 1),
                FixedNow));
    }

    [Fact]
    public void Create_ShouldThrow_WhenBirthDateIsInFuture()
    {
        var future = DateOnly.FromDateTime(FixedNow).AddDays(1);

        Assert.Throws<DomainException>(() =>
            Person.Create(
                "Maria",
                Cpf.Create("12345678909"),
                future,
                FixedNow));
    }

    [Fact]
    public void Update_ShouldChangeNameAndBirthDate()
    {
        var person = BuildPerson();
        var newBirth = new DateOnly(1991, 6, 1);
        var later = FixedNow.AddDays(5);

        person.Update("Maria Souza", newBirth, later);

        Assert.Equal("Maria Souza", person.Name);
        Assert.Equal(newBirth, person.BirthDate);
        Assert.Equal(later, person.UpdatedAtUtc);
        Assert.Equal(FixedNow, person.CreatedAtUtc);
    }

    [Fact]
    public void AddPhone_ShouldAppendPhone()
    {
        var person = BuildPerson();
        var number = PhoneNumber.Create("(13) 99999-9999", PhoneType.Mobile);

        var phone = person.AddPhone(PhoneType.Mobile, number, FixedNow);

        Assert.NotEqual(Guid.Empty, phone.Id);
        Assert.Equal(person.Id, phone.PersonId);
        Assert.Single(person.Phones);
    }

    [Fact]
    public void AddPhone_ShouldThrow_WhenLimitReached()
    {
        var person = BuildPerson();

        for (var i = 1; i <= Person.MaxActivePhonesPerPerson; i++)
        {
            var digits = $"1399999000{i - 1}";
            var number = PhoneNumber.Create(digits, PhoneType.Mobile);
            person.AddPhone(PhoneType.Mobile, number, FixedNow);
        }

        var overflow = PhoneNumber.Create("13999999999", PhoneType.Mobile);
        Assert.Throws<DomainException>(() => person.AddPhone(PhoneType.Mobile, overflow, FixedNow));
    }

    [Fact]
    public void AddPhone_ShouldThrow_WhenPersonIsInactive()
    {
        var person = BuildPerson();
        person.Delete(FixedNow);

        var number = PhoneNumber.Create("(13) 99999-9999", PhoneType.Mobile);
        Assert.Throws<DomainException>(() => person.AddPhone(PhoneType.Mobile, number, FixedNow));
    }

    [Fact]
    public void RemovePhone_ShouldRemoveExistingPhone()
    {
        var person = BuildPerson();
        var number = PhoneNumber.Create("(13) 99999-9999", PhoneType.Mobile);
        var phone = person.AddPhone(PhoneType.Mobile, number, FixedNow);

        person.RemovePhone(phone.Id, FixedNow);

        Assert.Empty(person.Phones);
    }

    [Fact]
    public void RemovePhone_ShouldThrow_WhenPhoneNotFound()
    {
        var person = BuildPerson();

        Assert.Throws<DomainException>(() => person.RemovePhone(Guid.NewGuid(), FixedNow));
    }

    [Fact]
    public void Delete_ShouldMarkAsInactive()
    {
        var person = BuildPerson();
        var later = FixedNow.AddDays(1);

        person.Delete(later);

        Assert.False(person.IsActive);
        Assert.Equal(later, person.DeletedAtUtc);
        Assert.Equal(later, person.UpdatedAtUtc);
    }

    [Fact]
    public void Delete_ShouldThrow_WhenAlreadyInactive()
    {
        var person = BuildPerson();
        person.Delete(FixedNow);

        Assert.Throws<DomainException>(() => person.Delete(FixedNow));
    }

    [Fact]
    public void Restore_ShouldReactivatePerson()
    {
        var person = BuildPerson();
        person.Delete(FixedNow);

        var later = FixedNow.AddDays(2);
        person.Restore(later);

        Assert.True(person.IsActive);
        Assert.Equal(later, person.RestoredAtUtc);
        Assert.Equal(later, person.UpdatedAtUtc);
    }

    [Fact]
    public void Restore_ShouldThrow_WhenAlreadyActive()
    {
        var person = BuildPerson();

        Assert.Throws<DomainException>(() => person.Restore(FixedNow));
    }
}
