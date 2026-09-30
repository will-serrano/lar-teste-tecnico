using CandidateAssessment.Application.Persons.GetDeleted;
using CandidateAssessment.Domain.Entities;
using CandidateAssessment.Domain.ValueObjects;
using CandidateAssessment.UnitTests.Fakes;
using Xunit;

namespace CandidateAssessment.UnitTests.Persons.GetDeleted;

public class GetDeletedPersonsHandlerTests
{
    private static readonly DateTime FixedNow =
        new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Should_ReturnOnlyDeletedPersons_OrderedByDeletedAtDesc()
    {
        var repo = new FakePersonRepository();

        var first = Person.Create("Maria", Cpf.Create("11144477735"), new DateOnly(1990, 1, 1), FixedNow);
        var second = Person.Create("Bruno", Cpf.Create("52998224725"), new DateOnly(1990, 1, 1), FixedNow.AddHours(-1));
        var active = Person.Create("Carlos", Cpf.Create("39053344705"), new DateOnly(1990, 1, 1), FixedNow.AddHours(-2));

        first.Delete(FixedNow);
        second.Delete(FixedNow.AddHours(-1));

        await repo.AddAsync(first);
        await repo.AddAsync(second);
        await repo.AddAsync(active);

        var handler = new GetDeletedPersonsHandler(repo);

        var result = await handler.HandleAsync(new GetDeletedPersonsQuery(null, null));

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Maria", result.Items[0].Name);
        Assert.Equal("Bruno", result.Items[1].Name);
        Assert.Equal(2, result.TotalItems);
    }
}
