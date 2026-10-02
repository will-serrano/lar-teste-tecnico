using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Application.Phones.Create;
using CandidateAssessment.Application.Phones.Update;
using CandidateAssessment.Domain.Enums;
using CandidateAssessment.UnitTests.Fakes;

namespace CandidateAssessment.UnitTests.Phones.Update;

public class UpdatePhoneHandlerTests
{
    private static readonly DateTime FixedNow =
        new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Should_UpdatePhone_When_PayloadIsValid()
    {
        var (personId, phoneId, personRepo, phoneRepo, uow, clock, _, personCache) = await SeedAsync();

        var handler = new UpdatePhoneHandler(personRepo, phoneRepo, uow, clock, personCache);
        await handler.HandleAsync(new UpdatePhoneCommand(
            personId,
            phoneId,
            PhoneType.Commercial,
            "1133334444"));

        var stored = await phoneRepo.GetByIdAsync(phoneId);
        Assert.NotNull(stored);
        Assert.Equal(PhoneType.Commercial, stored!.Type);
        Assert.Equal("1133334444", stored.Number);
    }

    [Fact]
    public async Task Should_ThrowPhoneNotFound_When_PhoneIdDoesNotMatchPerson()
    {
        var (personId, _, personRepo, phoneRepo, uow, clock, _, personCache) = await SeedAsync();

        var handler = new UpdatePhoneHandler(personRepo, phoneRepo, uow, clock, personCache);

        var ex = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(new UpdatePhoneCommand(
                personId,
                Guid.NewGuid(),
                PhoneType.Mobile,
                "13999999999")));
        Assert.Contains("PhoneNotFound", ex.Errors.Select(e => e.Code));
    }

    [Fact]
    public async Task Should_ThrowPhoneAlreadyExists_When_DuplicateTargetNumber()
    {
        var (personId, _, personRepo, phoneRepo, uow, clock, _, personCache) = await SeedAsync();

        var createHandler = new CreatePhoneHandler(personRepo, phoneRepo, uow, clock, personCache);
        await createHandler.HandleAsync(new CreatePhoneCommand(personId, PhoneType.Commercial, "1133334444"));

        var existing = (await personRepo.GetByIdAsync(personId))!.Phones.First();
        var updateHandler = new UpdatePhoneHandler(personRepo, phoneRepo, uow, clock, personCache);

        var ex = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => updateHandler.HandleAsync(new UpdatePhoneCommand(
                personId,
                existing.Id,
                PhoneType.Commercial,
                "1133334444")));
        Assert.Contains("PhoneAlreadyExists", ex.Errors.Select(e => e.Code));
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
        var phoneRepo = new FakePhoneRepository(personRepo);
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
