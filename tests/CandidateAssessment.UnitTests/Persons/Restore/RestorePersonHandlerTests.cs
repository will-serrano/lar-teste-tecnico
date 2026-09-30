using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Application.Persons.Restore;
using CandidateAssessment.Domain.Entities;
using CandidateAssessment.Domain.ValueObjects;
using CandidateAssessment.UnitTests.Fakes;
using Xunit;

namespace CandidateAssessment.UnitTests.Persons.Restore;

public class RestorePersonHandlerTests
{
    private static readonly DateTime FixedNow =
        new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Should_ReactivatePerson_When_SoftDeleted()
    {
        var repo = new FakePersonRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FixedDateTimeProvider(FixedNow);
        var handler = new RestorePersonHandler(repo, uow, clock);

        var person = Person.Create(
            "Maria",
            Cpf.Create("12345678909"),
            new DateOnly(1990, 1, 1),
            FixedNow);
        person.Delete(FixedNow);
        await repo.AddAsync(person);

        await handler.HandleAsync(new RestorePersonCommand(person.Id));

        var fetched = await repo.GetByIdAsync(person.Id);
        Assert.NotNull(fetched);
        Assert.True(fetched!.IsActive);
        Assert.Equal(FixedNow, fetched.RestoredAtUtc);
    }

    [Fact]
    public async Task Should_Throw_When_PersonNotFound()
    {
        var handler = new RestorePersonHandler(
            new FakePersonRepository(),
            new FakeUnitOfWork(),
            new FixedDateTimeProvider(FixedNow));

        await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(new RestorePersonCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task Should_Throw_When_PersonIsAlreadyActive()
    {
        var repo = new FakePersonRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FixedDateTimeProvider(FixedNow);
        var handler = new RestorePersonHandler(repo, uow, clock);

        var person = Person.Create(
            "Maria",
            Cpf.Create("12345678909"),
            new DateOnly(1990, 1, 1),
            FixedNow);
        await repo.AddAsync(person);

        await Assert.ThrowsAsync<Domain.Exceptions.DomainException>(
            () => handler.HandleAsync(new RestorePersonCommand(person.Id)));
    }
}
