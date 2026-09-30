namespace CandidateAssessment.Api.Contracts;

/// <summary>
/// Generic pagination envelope for list endpoints.
/// Field names match the contract defined in plan.md section 21.
/// </summary>
public sealed class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalItems { get; init; }

    public int TotalPages { get; init; }

    public bool HasNext { get; init; }

    public bool HasPrevious { get; init; }
}
