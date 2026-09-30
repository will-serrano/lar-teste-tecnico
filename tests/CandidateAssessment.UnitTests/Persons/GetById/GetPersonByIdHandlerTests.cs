using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Application.Persons.GetById;
using CandidateAssessment.Domain.Entities;
using CandidateAssessment.Domain.ValueObjects;
using CandidateAssessment.UnitTests.Fakes;
using Xunit;

namespace CandidateAssessment.UnitTests.Persons.GetById;

public class GetPersonByIdHandlerTests
{
    private static readonly DateTime FixedNow =
        new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Should_ReturnPerson_When_Found()
    {
        var repo = new FakePersonRepository();
        var cpf = Cpf.Create("12345678909");
        var person = Person.Create("Maria", cpf, new DateOnly(1990, 1, 1), FixedNow);
        await repo.AddAsync(person);

        var handler = new GetPersonByIdHandler(repo);
        var result = await handler.HandleAsync(new GetPersonByIdQuery(person.Id));

        Assert.Equal(person.Id, result.Id);
    }

    [Fact]
    public async Task Should_Throw_When_PersonNotFound()
    {
        var repo = new FakePersonRepository();
        var handler = new GetPersonByIdHandler(repo);

        var ex = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(new GetPersonByIdQuery(Guid.NewGuid())));

        Assert.Contains("PersonNotFound", ex.Errors.Select(e => e.Code));
    }

    [Fact]
    public async Task Should_Throw_When_PersonIsSoftDeleted()
    {
        var repo = new FakePersonRepository();
        var cpf = Cpf.Create("12345678909");
        var person = Person.Create("Maria", cpf, new DateOnly(1990, 1, 1), FixedNow);
        person.Delete(FixedNow);
        await repo.AddAsync(person);

        var handler = new GetPersonByIdHandler(repo);

        await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(new GetPersonByIdQuery(person.Id)));
    }
}
