using CandidateAssessment.Application.Abstractions.Caching;
using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Application.Persons.GetById;
using CandidateAssessment.Domain.Entities;
using CandidateAssessment.Domain.ValueObjects;
using CandidateAssessment.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CandidateAssessment.UnitTests.Persons.GetById;

public class GetPersonByIdHandlerTests
{
    private static readonly DateTime FixedNow =
        new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Should_ReturnPerson_When_Found()
    {
        var repo = new FakePersonRepository();
        var cache = new FakeCacheService();
        var handler = BuildHandler(repo, cache);
        var cpf = Cpf.Create("12345678909");
        var person = Person.Create("Maria", cpf, new DateOnly(1990, 1, 1), FixedNow);
        await repo.AddAsync(person);

        var result = await handler.HandleAsync(new GetPersonByIdQuery(person.Id));

        Assert.Equal(person.Id, result.Id);
    }

    [Fact]
    public async Task Should_Throw_When_PersonNotFound()
    {
        var repo = new FakePersonRepository();
        var cache = new FakeCacheService();
        var handler = BuildHandler(repo, cache);

        var ex = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(new GetPersonByIdQuery(Guid.NewGuid())));

        Assert.Contains("PersonNotFound", ex.Errors.Select(e => e.Code));
    }

    [Fact]
    public async Task Should_Throw_When_PersonIsSoftDeleted()
    {
        var repo = new FakePersonRepository();
        var cache = new FakeCacheService();
        var handler = BuildHandler(repo, cache);
        var cpf = Cpf.Create("12345678909");
        var person = Person.Create("Maria", cpf, new DateOnly(1990, 1, 1), FixedNow);
        person.Delete(FixedNow);
        await repo.AddAsync(person);

        await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(new GetPersonByIdQuery(person.Id)));
    }

    [Fact]
    public async Task Should_PopulateCache_OnFirstRead_And_UseCache_OnSecond()
    {
        var repo = new FakePersonRepository();
        var cache = new FakeCacheService();
        var handler = BuildHandler(repo, cache);

        var cpf = Cpf.Create("12345678909");
        var person = Person.Create("Maria", cpf, new DateOnly(1990, 1, 1), FixedNow);
        await repo.AddAsync(person);

        var first = await handler.HandleAsync(new GetPersonByIdQuery(person.Id));
        var second = await handler.HandleAsync(new GetPersonByIdQuery(person.Id));

        Assert.Equal(person.Id, first.Id);
        Assert.Equal(person.Id, second.Id);
        Assert.Equal(2, cache.GetCount);
        Assert.Equal(1, cache.SetCount);
    }

    private static GetPersonByIdHandler BuildHandler(
        FakePersonRepository repo,
        FakeCacheService cache)
    {
        var options = Options.Create(new CacheOptions());
        return new GetPersonByIdHandler(
            repo,
            cache,
            options,
            NullLogger<GetPersonByIdHandler>.Instance);
    }
}
