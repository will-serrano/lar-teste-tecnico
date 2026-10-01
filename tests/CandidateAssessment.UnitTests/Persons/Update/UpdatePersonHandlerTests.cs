using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Application.Persons.Update;
using CandidateAssessment.Domain.Entities;
using CandidateAssessment.Domain.ValueObjects;
using CandidateAssessment.UnitTests.Fakes;
using Xunit;

namespace CandidateAssessment.UnitTests.Persons.Update;

public class UpdatePersonHandlerTests
{
    private static readonly DateTime FixedNow =
        new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Should_UpdateNameAndBirthDate_When_PersonExists()
    {
        var repo = new FakePersonRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FixedDateTimeProvider(FixedNow);
        var (_, personCache) = TestCacheFactory.Create();
        var handler = new UpdatePersonHandler(repo, uow, clock, personCache);

        var cpf = Cpf.Create("12345678909");
        var person = Person.Create("Maria", cpf, new DateOnly(1990, 1, 1), FixedNow);
        await repo.AddAsync(person);

        var later = FixedNow.AddDays(2);
        var laterClock = new FixedDateTimeProvider(later);
        var (_, personCache2) = TestCacheFactory.Create();
        var handler2 = new UpdatePersonHandler(repo, uow, laterClock, personCache2);

        await handler2.HandleAsync(new UpdatePersonCommand(person.Id, "Maria Souza", new DateOnly(1991, 2, 2)));

        var fetched = await repo.GetByIdAsync(person.Id);
        Assert.Equal("Maria Souza", fetched!.Name);
        Assert.Equal(new DateOnly(1991, 2, 2), fetched.BirthDate);
        Assert.Equal(later, fetched.UpdatedAtUtc);
    }

    [Fact]
    public async Task Should_Throw_When_PersonNotFound()
    {
        var (_, personCache) = TestCacheFactory.Create();
        var handler = new UpdatePersonHandler(
            new FakePersonRepository(),
            new FakeUnitOfWork(),
            new FixedDateTimeProvider(FixedNow),
            personCache);

        var ex = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(new UpdatePersonCommand(Guid.NewGuid(), "X", new DateOnly(1990, 1, 1))));

        Assert.Contains("PersonNotFound", ex.Errors.Select(e => e.Code));
    }

    [Fact]
    public async Task Should_InvalidateCache_OnSuccessfulUpdate()
    {
        var repo = new FakePersonRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FixedDateTimeProvider(FixedNow);
        var (cache, personCache) = TestCacheFactory.Create();
        var handler = new UpdatePersonHandler(repo, uow, clock, personCache);

        var person = Person.Create(
            "Maria",
            Cpf.Create("12345678909"),
            new DateOnly(1990, 1, 1),
            FixedNow);
        await repo.AddAsync(person);

        await handler.HandleAsync(new UpdatePersonCommand(person.Id, "Maria Updated", new DateOnly(1991, 1, 1)));

        Assert.True(cache.RemoveCount >= 1);
    }
}
