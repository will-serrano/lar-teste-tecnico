using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Application.Phones.Create;
using CandidateAssessment.Application.Phones.Delete;
using CandidateAssessment.Domain.Enums;
using CandidateAssessment.UnitTests.Fakes;
using Xunit;

namespace CandidateAssessment.UnitTests.Phones.Delete;

public class DeletePhoneHandlerTests
{
    private static readonly DateTime FixedNow =
        new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Should_RemovePhone_When_PhoneExistsForPerson()
    {
        var (personId, phoneId, personRepo, _, uow, clock, _, personCache) = await SeedAsync();

        var handler = new DeletePhoneHandler(personRepo, uow, clock, personCache);
        await handler.HandleAsync(new DeletePhoneCommand(personId, phoneId));

        var person = await personRepo.GetByIdIncludingDeletedAsync(personId);
        Assert.NotNull(person);
        Assert.DoesNotContain(person!.Phones, p => p.Id == phoneId);
    }

    [Fact]
    public async Task Should_ThrowPhoneNotFound_When_PhoneDoesNotBelongToPerson()
    {
        var (personId, _, personRepo, _, uow, clock, _, personCache) = await SeedAsync();

        var handler = new DeletePhoneHandler(personRepo, uow, clock, personCache);

        var ex = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(new DeletePhoneCommand(personId, Guid.NewGuid())));
        Assert.Contains("PhoneNotFound", ex.Errors.Select(e => e.Code));
    }

    private static async Task<(
        Guid PersonId,
        Guid PhoneId,
        FakePersonRepository personRepo,
        FakePhoneRepository phoneRepo,
        FakeUnitOfWork uow,
        FixedDateTimeProvider clock,
        FakeCacheService cache,
        FakePersonCache personCache)> SeedAsync()
    {
        var personRepo = new FakePersonRepository();
        var phoneRepo = new FakePhoneRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FixedDateTimeProvider(FixedNow);
        var (cache, personCache) = TestCacheFactory.Create();
        var createPerson = await new Application.Persons.Create.CreatePersonHandler(
            personRepo,
            uow,
            clock,
            personCache).HandleAsync(new Application.Persons.Create.CreatePersonCommand(
                "Maria",
                "123.456.789-09",
                new DateOnly(1990, 1, 1)));

        var createHandler = new CreatePhoneHandler(personRepo, phoneRepo, uow, clock, personCache);
        var phoneId = await createHandler.HandleAsync(new CreatePhoneCommand(
            createPerson,
            PhoneType.Mobile,
            "13999999999"));

        return (createPerson, phoneId, personRepo, phoneRepo, uow, clock, cache, personCache);
    }
}
