using CandidateAssessment.Application.Abstractions.Pagination;
using CandidateAssessment.Application.Persons.GetById;
using CandidateAssessment.Domain.Entities;
using CandidateAssessment.Domain.ValueObjects;

namespace CandidateAssessment.Api.Contracts.Persons;

internal static class PersonMappings
{
    public static PersonResponse ToResponse(this Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        return ToResponseFromCore(
            person.Id,
            person.Name,
            person.Cpf,
            person.BirthDate,
            person.IsActive,
            person.CreatedAtUtc,
            person.UpdatedAtUtc,
            person.DeletedAtUtc,
            person.RestoredAtUtc);
    }

    public static PersonResponse ToResponse(this CachedPerson cached)
    {
        ArgumentNullException.ThrowIfNull(cached);

        return ToResponseFromCore(
            cached.Id,
            cached.Name,
            cached.Cpf,
            cached.BirthDate,
            cached.IsActive,
            cached.CreatedAtUtc,
            cached.UpdatedAtUtc,
            cached.DeletedAtUtc,
            cached.RestoredAtUtc);
    }

    private static PersonResponse ToResponseFromCore(
        Guid id,
        string name,
        string cpf,
        DateOnly birthDate,
        bool isActive,
        DateTime createdAtUtc,
        DateTime updatedAtUtc,
        DateTime? deletedAtUtc,
        DateTime? restoredAtUtc) => new()
        {
            Id = id,
            Name = name,
            Cpf = Cpf.Create(cpf).Format(),
            BirthDate = birthDate,
            IsActive = isActive,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc,
            DeletedAtUtc = deletedAtUtc,
            RestoredAtUtc = restoredAtUtc,
        };

    public static PagedResponse<PersonResponse> ToResponse<TDomain>(
        this PagedResult<TDomain> result,
        Func<TDomain, PersonResponse> map)
        where TDomain : class
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(map);

        return new PagedResponse<PersonResponse>
        {
            Items = result.Items.Select(map).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalItems = result.TotalItems,
            TotalPages = result.TotalPages,
            HasNext = result.HasNext,
            HasPrevious = result.HasPrevious,
        };
    }
}
