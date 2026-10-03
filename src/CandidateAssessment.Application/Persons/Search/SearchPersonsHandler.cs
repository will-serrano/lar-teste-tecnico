using CandidateAssessment.Application.Abstractions.Pagination;
using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Diagnostics;
using CandidateAssessment.Domain.Entities;

namespace CandidateAssessment.Application.Persons.Search;

public sealed class SearchPersonsHandler
{
    private readonly IPersonRepository _personRepository;

    public SearchPersonsHandler(IPersonRepository personRepository)
    {
        _personRepository = personRepository;
    }

    public async Task<PagedResult<Person>> HandleAsync(
        SearchPersonsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        using var operation = ApplicationDiagnostics.StartOperation("persons.search");

        var page = PaginationOptions.NormalizePage(query.Page);
        var pageSize = PaginationOptions.NormalizePageSize(query.PageSize);

        var name = string.IsNullOrWhiteSpace(query.Name) ? null : query.Name.Trim();
        var cpf = string.IsNullOrWhiteSpace(query.Cpf) ? null : query.Cpf.Trim();

        var totalItems = await _personRepository.CountSearchAsync(name, cpf, cancellationToken);
        var items = await _personRepository.SearchAsync(
            name,
            cpf,
            page,
            pageSize,
            cancellationToken);

        operation.Complete();
        return new PagedResult<Person>(items, page, pageSize, totalItems);
    }
}
