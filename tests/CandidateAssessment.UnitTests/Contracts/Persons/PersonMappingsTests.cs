using CandidateAssessment.Api.Contracts.Persons;
using CandidateAssessment.Application.Abstractions.Pagination;
using CandidateAssessment.Application.Persons.GetById;
using CandidateAssessment.Domain.Entities;
using CandidateAssessment.Domain.Exceptions;
using CandidateAssessment.Domain.ValueObjects;

namespace CandidateAssessment.UnitTests.Contracts.Persons;

public class PersonMappingsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Should_MapAllFieldsFromEntityAndCache_When_PersonStateChanges(int state)
    {
        var person = CreatePerson();
        var createdAt = person.CreatedAtUtc;
        if (state >= 1)
        {
            person.Update("Updated Person", new DateOnly(1991, 2, 2), createdAt.AddHours(1));
        }

        if (state >= 2)
        {
            person.Delete(createdAt.AddHours(2));
        }

        if (state >= 3)
        {
            person.Restore(createdAt.AddHours(3));
        }

        if (state >= 4)
        {
            person.Delete(createdAt.AddHours(4));
        }

        AssertResponse(person, person.ToResponse());
        AssertResponse(person, CachedPerson.From(person).ToResponse());
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_PersonIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => PersonMappings.ToResponse((Person)null!));

        Assert.Equal("person", exception.ParamName);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_CachedPersonIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => PersonMappings.ToResponse((CachedPerson)null!));

        Assert.Equal("cached", exception.ParamName);
    }

    [Fact]
    public void Should_ThrowDomainException_When_CachedCpfIsInvalid()
    {
        var cached = new CachedPerson { Cpf = "00000000000" };

        Assert.Throws<DomainException>(() => cached.ToResponse());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_MapItemsAndPagination_When_ResultHasItems(bool useCache)
    {
        var person = CreatePerson();
        var result = new PagedResult<Person>([person], 2, 1, 3);

        var response = useCache
            ? new PagedResult<CachedPerson>(
                [CachedPerson.From(person)], 2, 1, 3)
                .ToResponse(cached => cached.ToResponse())
            : result.ToResponse(item => item.ToResponse());

        AssertResponse(person, Assert.Single(response.Items));
        Assert.Equal(result.Page, response.Page);
        Assert.Equal(result.PageSize, response.PageSize);
        Assert.Equal(result.TotalItems, response.TotalItems);
        Assert.Equal(result.TotalPages, response.TotalPages);
        Assert.True(response.HasNext);
        Assert.True(response.HasPrevious);
    }

    [Fact]
    public void Should_MapEmptyPage_When_ResultHasNoItems()
    {
        var result = new PagedResult<Person>(Array.Empty<Person>(), 1, 10, 0);

        var response = result.ToResponse(person => person.ToResponse());

        Assert.Empty(response.Items);
        Assert.Equal(1, response.Page);
        Assert.Equal(10, response.PageSize);
        Assert.Equal(0, response.TotalItems);
        Assert.Equal(0, response.TotalPages);
        Assert.False(response.HasNext);
        Assert.False(response.HasPrevious);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_PagedResultIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => PersonMappings.ToResponse<Person>(null!, person => person.ToResponse()));

        Assert.Equal("result", exception.ParamName);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_MapIsNull()
    {
        var result = new PagedResult<Person>(Array.Empty<Person>(), 1, 10, 0);

        var exception = Assert.Throws<ArgumentNullException>(() => result.ToResponse(null!));

        Assert.Equal("map", exception.ParamName);
    }

    private static Person CreatePerson() => Person.Create(
        "Maria Souza",
        Cpf.Create("52998224725"),
        new DateOnly(1990, 5, 20),
        new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    private static void AssertResponse(Person person, PersonResponse response)
    {
        Assert.Equal(person.Id, response.Id);
        Assert.Equal(person.Name, response.Name);
        Assert.Equal("529.982.247-25", response.Cpf);
        Assert.Equal(person.BirthDate, response.BirthDate);
        Assert.Equal(person.IsActive, response.IsActive);
        Assert.Equal(person.CreatedAtUtc, response.CreatedAtUtc);
        Assert.Equal(person.UpdatedAtUtc, response.UpdatedAtUtc);
        Assert.Equal(person.DeletedAtUtc, response.DeletedAtUtc);
        Assert.Equal(person.RestoredAtUtc, response.RestoredAtUtc);
    }
}
