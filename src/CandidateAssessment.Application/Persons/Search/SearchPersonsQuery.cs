namespace CandidateAssessment.Application.Persons.Search;

/// <summary>
/// Query for searching active persons.
/// Filters are optional and combine with AND.
/// Pagination defaults are applied by the handler.
/// </summary>
public sealed record SearchPersonsQuery(
    string? Name,
    string? Cpf,
    int? Page,
    int? PageSize);
