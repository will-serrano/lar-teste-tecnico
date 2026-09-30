namespace CandidateAssessment.Application.Abstractions.Pagination;

/// <summary>
/// Application-layer pagination wrapper. Domain entities flow through here
/// and are mapped to API DTOs by controllers.
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
