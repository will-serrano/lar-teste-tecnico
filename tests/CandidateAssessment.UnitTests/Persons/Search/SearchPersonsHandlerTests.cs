using CandidateAssessment.Application.Persons.Search;
using CandidateAssessment.Domain.Entities;
using CandidateAssessment.Domain.ValueObjects;
using CandidateAssessment.UnitTests.Fakes;

namespace CandidateAssessment.UnitTests.Persons.Search;

public class SearchPersonsHandlerTests
{
    private static readonly DateTime FixedNow =
        new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Should_FilterByName_When_NameIsProvided()
    {
        var repo = new FakePersonRepository();
        await SeedPerson(repo, "Ana Beatriz", "11144477735");
        await SeedPerson(repo, "Bruno Costa", "52998224725");
        await SeedPerson(repo, "Carlos Souza", "39053344705");

        var handler = new SearchPersonsHandler(repo);

        var result = await handler.HandleAsync(
            new SearchPersonsQuery("bruno", null, null, null));

        Assert.Single(result.Items);
        Assert.Equal("Bruno Costa", result.Items[0].Name);
    }

    [Fact]
    public async Task Should_FilterByCpf_When_CpfIsProvided()
    {
        var repo = new FakePersonRepository();
        await SeedPerson(repo, "Ana Beatriz", "11144477735");
        await SeedPerson(repo, "Bruno Costa", "52998224725");

        var handler = new SearchPersonsHandler(repo);

        var result = await handler.HandleAsync(
            new SearchPersonsQuery(null, "529.982.247-25", null, null));

        Assert.Single(result.Items);
        Assert.Equal("Bruno Costa", result.Items[0].Name);
    }

    [Theory]
    [InlineData("5299822472")]
    [InlineData("529982247259")]
    [InlineData("529.982.247-25.9")]
    [InlineData("abc")]
    public async Task Should_ReturnNoMatches_When_CpfLengthIsInvalid(string cpf)
    {
        var repo = new FakePersonRepository();
        await SeedPerson(repo, "Bruno Costa", "52998224725");
        var handler = new SearchPersonsHandler(repo);

        var result = await handler.HandleAsync(new SearchPersonsQuery(null, cpf, null, null));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalItems);
    }

    [Fact]
    public async Task Should_Paginate_When_PageAndPageSizeProvided()
    {
        var repo = new FakePersonRepository();
        for (var i = 0; i < 25; i++)
        {
            await SeedPerson(repo, $"Person {i:D2}", GenerateValidCpf(i));
        }

        var handler = new SearchPersonsHandler(repo);

        var first = await handler.HandleAsync(new SearchPersonsQuery(null, null, 1, 10));
        var second = await handler.HandleAsync(new SearchPersonsQuery(null, null, 2, 10));
        var third = await handler.HandleAsync(new SearchPersonsQuery(null, null, 3, 10));

        Assert.Equal(10, first.Items.Count);
        Assert.Equal(10, second.Items.Count);
        Assert.Equal(5, third.Items.Count);
        Assert.Equal(25, first.TotalItems);
        Assert.Equal(3, first.TotalPages);
        Assert.True(first.HasNext);
        Assert.False(first.HasPrevious);
        Assert.False(third.HasNext);
        Assert.True(third.HasPrevious);
    }

    [Fact]
    public async Task Should_ExcludeSoftDeletedPersons_FromDefaultSearch()
    {
        var repo = new FakePersonRepository();
        var person = await SeedPerson(repo, "Maria", "11144477735");
        person.Delete(FixedNow);

        var handler = new SearchPersonsHandler(repo);

        var result = await handler.HandleAsync(new SearchPersonsQuery(null, null, null, null));

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Should_NormalizePagination_When_InputsAreInvalid()
    {
        var repo = new FakePersonRepository();
        var handler = new SearchPersonsHandler(repo);

        var result = await handler.HandleAsync(new SearchPersonsQuery(null, null, -5, 9999));

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
    }

    private static async Task<Person> SeedPerson(FakePersonRepository repo, string name, string cpf)
    {
        var person = Person.Create(
            name,
            Cpf.Create(cpf),
            new DateOnly(1990, 1, 1),
            FixedNow);
        await repo.AddAsync(person);
        return person;
    }

    /// <summary>
    /// Gera um CPF matematicamente válido cujos nove primeiros dígitos codificam a semente.
    /// Usado para produzir vários CPFs distintos sem precisar listá-los manualmente.
    /// </summary>
    private static string GenerateValidCpf(int index)
    {
        var digits = new char[11];
        var n = index + 1;
        digits[0] = (char)('0' + (n % 10));
        n /= 10;
        digits[1] = (char)('0' + (n % 10));
        n /= 10;
        digits[2] = (char)('0' + (n % 10));
        n /= 10;
        digits[3] = (char)('0' + (n % 10));
        digits[4] = '0';
        digits[5] = '1';
        digits[6] = '2';
        digits[7] = '3';
        digits[8] = '4';

        int[] weights1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        var sum1 = 0;
        for (var i = 0; i < 9; i++)
        {
            sum1 += (digits[i] - '0') * weights1[i];
        }

        var remainder1 = sum1 % 11;
        digits[9] = (char)('0' + (remainder1 < 2 ? 0 : 11 - remainder1));

        int[] weights2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        var sum2 = 0;
        for (var i = 0; i < 10; i++)
        {
            sum2 += (digits[i] - '0') * weights2[i];
        }

        var remainder2 = sum2 % 11;
        digits[10] = (char)('0' + (remainder2 < 2 ? 0 : 11 - remainder2));

        return new string(digits);
    }
}
