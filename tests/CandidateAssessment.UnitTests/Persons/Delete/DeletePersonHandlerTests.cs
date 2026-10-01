using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Application.Persons.Delete;
using CandidateAssessment.Domain.Entities;
using CandidateAssessment.Domain.ValueObjects;
using CandidateAssessment.UnitTests.Fakes;
using Xunit;

namespace CandidateAssessment.UnitTests.Persons.Delete;

public class DeletePersonHandlerTests
{
    private static readonly DateTime FixedNow =
        new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Should_SoftDelete_When_PersonExists()
    {
        var repo = new FakePersonRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FixedDateTimeProvider(FixedNow);
        var (_, personCache) = TestCacheFactory.Create();
        var handler = new DeletePersonHandler(repo, uow, clock, personCache);

        var person = Person.Create(
            "Maria",
            Cpf.Create("12345678909"),
            new DateOnly(1990, 1, 1),
            FixedNow);
        await repo.AddAsync(person);

        await handler.HandleAsync(new DeletePersonCommand(person.Id));

        var fetched = await repo.GetByIdAsync(person.Id);
        Assert.Null(fetched);

        var deleted = await repo.GetByIdIncludingDeletedAsync(person.Id);
        Assert.NotNull(deleted);
        Assert.False(deleted!.IsActive);
        Assert.Equal(FixedNow, deleted.DeletedAtUtc);
    }

    [Fact]
    public async Task Should_Throw_When_PersonNotFound()
    {
        var (_, personCache) = TestCacheFactory.Create();
        var handler = new DeletePersonHandler(
            new FakePersonRepository(),
            new FakeUnitOfWork(),
            new FixedDateTimeProvider(FixedNow),
            personCache);

        await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(new DeletePersonCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task Should_InvalidateCache_OnSuccessfulDelete()
    {
        var repo = new FakePersonRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FixedDateTimeProvider(FixedNow);
        var (cache, personCache) = TestCacheFactory.Create();
        var handler = new DeletePersonHandler(repo, uow, clock, personCache);

        var person = Person.Create(
            "Maria",
            Cpf.Create("12345678909"),
            new DateOnly(1990, 1, 1),
            FixedNow);
        await repo.AddAsync(person);

        await handler.HandleAsync(new DeletePersonCommand(person.Id));

        Assert.True(cache.RemoveCount >= 1);
    }
}
