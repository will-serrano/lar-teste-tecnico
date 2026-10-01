namespace CandidateAssessment.Application.Abstractions.Caching;

/// <summary>
/// Centralizes cache invalidation for the Person aggregate so mutation handlers
/// stay free of cache-key construction. Keeps read and write paths in sync.
/// </summary>
public interface IPersonCache
{
    /// <summary>
    /// Removes the cached Person detail for the given id, when present.
    /// </summary>
    Task InvalidateAsync(Guid personId, CancellationToken cancellationToken = default);
}
