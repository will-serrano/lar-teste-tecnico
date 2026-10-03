using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Application.Persons.Create;
using CandidateAssessment.UnitTests.Fakes;

namespace CandidateAssessment.UnitTests.Persons.Create;

public class CreatePersonHandlerTests
{
    private static readonly DateTime FixedNow =
        new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Should_ReturnNewId_And_PersistPerson_When_CpfIsUnique()
    {
        var repo = new FakePersonRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FixedDateTimeProvider(FixedNow);
        var (_, personCache) = TestCacheFactory.Create();
        var handler = new CreatePersonHandler(repo, uow, clock, personCache);

        var command = new CreatePersonCommand("Maria", "123.456.789-09", new DateOnly(1990, 1, 1));

        var id = await handler.HandleAsync(command);

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(1, uow.SaveChangesCallCount);
    }

    [Fact]
    public async Task Should_Throw_When_CpfAlreadyExists()
    {
        var repo = new FakePersonRepository();
        var clock = new FixedDateTimeProvider(FixedNow);
        var (_, personCache) = TestCacheFactory.Create();
        var handler = new CreatePersonHandler(repo, new FakeUnitOfWork(), clock, personCache);

        var command = new CreatePersonCommand("Maria", "123.456.789-09", new DateOnly(1990, 1, 1));
        await handler.HandleAsync(command);

        var second = new CreatePersonCommand("Other", "12345678909", new DateOnly(1992, 2, 2));

        var ex = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(second));
        Assert.Contains("CpfAlreadyExists", ex.Errors.Select(e => e.Code));
    }

    [Fact]
    public async Task Should_Throw_When_CpfIsMathematicallyInvalid()
    {
        var repo = new FakePersonRepository();
        var clock = new FixedDateTimeProvider(FixedNow);
        var (_, personCache) = TestCacheFactory.Create();
        var handler = new CreatePersonHandler(repo, new FakeUnitOfWork(), clock, personCache);

        var command = new CreatePersonCommand("Maria", "00000000000", new DateOnly(1990, 1, 1));

        await Assert.ThrowsAsync<Domain.Exceptions.DomainException>(
            () => handler.HandleAsync(command));
    }

    [Fact]
    public async Task Should_Throw_When_BirthDateIsInFuture()
    {
        var repo = new FakePersonRepository();
        var clock = new FixedDateTimeProvider(FixedNow);
        var (_, personCache) = TestCacheFactory.Create();
        var handler = new CreatePersonHandler(repo, new FakeUnitOfWork(), clock, personCache);

        var future = DateOnly.FromDateTime(FixedNow).AddDays(1);
        var command = new CreatePersonCommand("Maria", "123.456.789-09", future);

        await Assert.ThrowsAsync<Domain.Exceptions.DomainException>(
            () => handler.HandleAsync(command));
    }

    [Fact]
    public async Task Should_InvalidateCache_OnSuccessfulCreate()
    {
        var repo = new FakePersonRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FixedDateTimeProvider(FixedNow);
        var (cache, personCache) = TestCacheFactory.Create();
        var handler = new CreatePersonHandler(repo, uow, clock, personCache);
        _ = await handler.HandleAsync(
            new CreatePersonCommand("Maria", "123.456.789-09", new DateOnly(1990, 1, 1)));

        Assert.True(cache.RemoveCount >= 1);
    }

    [Fact]
    public async Task Should_StampAuditTimestampsFromDateProvider_OnPersist()
    {
        // Regressão: o controller criava o DTO PersonResponse a partir da requisição,
        // omitindo CreatedAtUtc/UpdatedAtUtc. Embora o domínio os preenchesse corretamente,
        // a API retornava valores zerados. Este teste garante que a entidade persistida
        // contenha os timestamps reais fornecidos por IDateTimeProvider, evitando que
        // uma alteração futura no handler que exponha a entidade (ou a mapeie diretamente)
        // reintroduza esse problema sem ser percebida.
        var repo = new FakePersonRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FixedDateTimeProvider(FixedNow);
        var (_, personCache) = TestCacheFactory.Create();
        var handler = new CreatePersonHandler(repo, uow, clock, personCache);

        var id = await handler.HandleAsync(
            new CreatePersonCommand("Maria", "123.456.789-09", new DateOnly(1990, 1, 1)));

        var stored = await repo.GetByIdAsync(id);
        Assert.NotNull(stored);
        Assert.Equal(FixedNow, stored!.CreatedAtUtc);
        Assert.Equal(FixedNow, stored.UpdatedAtUtc);
        Assert.Null(stored.DeletedAtUtc);
        Assert.Null(stored.RestoredAtUtc);
    }
}
