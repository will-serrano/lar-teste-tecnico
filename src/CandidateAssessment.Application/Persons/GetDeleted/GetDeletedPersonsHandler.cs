using CandidateAssessment.Application.Abstractions.Pagination;
using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Diagnostics;
using CandidateAssessment.Domain.Entities;

namespace CandidateAssessment.Application.Persons.GetDeleted;

public sealed class GetDeletedPersonsHandler
{
    private readonly IPersonRepository _personRepository;

    public GetDeletedPersonsHandler(IPersonRepository personRepository)
    {
        _personRepository = personRepository;
    }

    public async Task<PagedResult<Person>> HandleAsync(
        GetDeletedPersonsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        using var operation = ApplicationDiagnostics.StartOperation("persons.deleted");

        var page = PaginationOptions.NormalizePage(query.Page);
        var pageSize = PaginationOptions.NormalizePageSize(query.PageSize);

        var totalItems = await _personRepository.CountDeletedAsync(cancellationToken);
        var items = await _personRepository.GetDeletedAsync(
            page,
            pageSize,
            cancellationToken);

        operation.Complete();
        return new PagedResult<Person>(items, page, pageSize, totalItems);
    }
}
