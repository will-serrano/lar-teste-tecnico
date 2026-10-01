namespace CandidateAssessment.Application.Abstractions.Caching;

/// <summary>
/// Abstraction over the application's cache layer.
/// Application use cases depend on this contract, never on <c>IMemoryCache</c>,
/// so the backing implementation can be swapped (Memory, Redis, distributed) without
/// touching Domain or Application.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/> or <c>null</c> when missing.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="key"/> for the given duration.
    /// A non-positive <paramref name="absoluteExpiration"/> writes without expiry.
    /// </summary>
    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan absoluteExpiration,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Removes the entry for <paramref name="key"/> when present. No-op otherwise.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
