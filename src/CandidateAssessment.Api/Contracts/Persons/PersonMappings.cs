using CandidateAssessment.Application.Abstractions.Pagination;
using CandidateAssessment.Domain.Entities;
using CandidateAssessment.Domain.ValueObjects;

namespace CandidateAssessment.Api.Contracts.Persons;

internal static class PersonMappings
{
    public static PersonResponse ToResponse(this Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        return new PersonResponse
        {
            Id = person.Id,
            Name = person.Name,
            Cpf = Cpf.Create(person.Cpf).Format(),
            BirthDate = person.BirthDate,
            IsActive = person.IsActive,
            CreatedAtUtc = person.CreatedAtUtc,
            UpdatedAtUtc = person.UpdatedAtUtc,
            DeletedAtUtc = person.DeletedAtUtc,
            RestoredAtUtc = person.RestoredAtUtc,
        };
    }

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
