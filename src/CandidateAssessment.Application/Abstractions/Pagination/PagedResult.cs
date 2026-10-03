namespace CandidateAssessment.Application.Abstractions.Pagination;

/// <summary>
/// Estrutura de paginação da camada Application. As entidades de domínio passam por aqui
/// e são mapeadas para DTOs da API pelos controllers.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems)
{
    public int TotalPages => PageSize == 0
        ? 0
        : (int)Math.Ceiling((double)TotalItems / PageSize);

    public bool HasNext => Page < TotalPages;

    public bool HasPrevious => Page > 1;
}
