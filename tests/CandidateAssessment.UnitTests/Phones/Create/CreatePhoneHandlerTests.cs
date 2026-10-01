using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Application.Phones.Create;
using CandidateAssessment.Domain.Enums;
using CandidateAssessment.UnitTests.Fakes;
using Xunit;

namespace CandidateAssessment.UnitTests.Phones.Create;

public class CreatePhoneHandlerTests
{
    private static readonly DateTime FixedNow =
        new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Should_AddPhone_When_PersonExists_And_NumberIsUnique()
    {
        var personRepo = new FakePersonRepository();
        var phoneRepo = new FakePhoneRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FixedDateTimeProvider(FixedNow);
        var handler = new CreatePhoneHandler(personRepo, phoneRepo, uow, clock);

        var createPerson = await new Application.Persons.Create.CreatePersonHandler(
            personRepo,
            uow,
            clock).HandleAsync(new Application.Persons.Create.CreatePersonCommand(
                "Maria",
                "123.456.789-09",
                new DateOnly(1990, 1, 1)));

        var command = new CreatePhoneCommand(createPerson, PhoneType.Mobile, "13999999999");

        var id = await handler.HandleAsync(command);

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(2, uow.SaveChangesCallCount);
    }

    [Fact]
    public async Task Should_ThrowPhoneNotFound_When_PersonDoesNotExist()
    {
        var personRepo = new FakePersonRepository();
        var phoneRepo = new FakePhoneRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FixedDateTimeProvider(FixedNow);
        var handler = new CreatePhoneHandler(personRepo, phoneRepo, uow, clock);

        var command = new CreatePhoneCommand(Guid.NewGuid(), PhoneType.Mobile, "13999999999");

        var ex = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(command));
        Assert.Contains("PersonNotFound", ex.Errors.Select(e => e.Code));
    }

    [Fact]
    public async Task Should_ThrowPhoneAlreadyExists_When_DuplicateNumberForSamePerson()
    {
        var personRepo = new FakePersonRepository();
        var phoneRepo = new FakePhoneRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FixedDateTimeProvider(FixedNow);
        var createPerson = await new Application.Persons.Create.CreatePersonHandler(
            personRepo,
            uow,
            clock).HandleAsync(new Application.Persons.Create.CreatePersonCommand(
                "Maria",
                "123.456.789-09",
                new DateOnly(1990, 1, 1)));

        var handler = new CreatePhoneHandler(personRepo, phoneRepo, uow, clock);

        await handler.HandleAsync(new CreatePhoneCommand(createPerson, PhoneType.Mobile, "13999999999"));

        var ex = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(new CreatePhoneCommand(
                createPerson,
                PhoneType.Commercial,
                "13999999999")));
        Assert.Contains("PhoneAlreadyExists", ex.Errors.Select(e => e.Code));
    }

    [Fact]
    public async Task Should_ThrowDomain_When_PersonAlreadyHasFivePhones()
    {
        var personRepo = new FakePersonRepository();
        var phoneRepo = new FakePhoneRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FixedDateTimeProvider(FixedNow);
        var createPerson = await new Application.Persons.Create.CreatePersonHandler(
            personRepo,
            uow,
            clock).HandleAsync(new Application.Persons.Create.CreatePersonCommand(
                "Maria",
                "123.456.789-09",
                new DateOnly(1990, 1, 1)));

        var handler = new CreatePhoneHandler(personRepo, phoneRepo, uow, clock);

        for (var i = 0; i < 5; i++)
        {
            var suffix = i.ToString().PadLeft(2, '0');
            await handler.HandleAsync(new CreatePhoneCommand(
                createPerson,
                PhoneType.Mobile,
                $"139999999{suffix}"));
        }

        await Assert.ThrowsAsync<Domain.Exceptions.DomainException>(
            () => handler.HandleAsync(new CreatePhoneCommand(
                createPerson,
                PhoneType.Mobile,
                "13999999888")));
    }
}
