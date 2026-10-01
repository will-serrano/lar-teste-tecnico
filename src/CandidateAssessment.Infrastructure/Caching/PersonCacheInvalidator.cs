using CandidateAssessment.Application.Abstractions.Caching;

namespace CandidateAssessment.Infrastructure.Caching;

/// <summary>
/// Memory-backed implementation of <see cref="IPersonCache"/>.
/// Resolves the cache key through <see cref="PersonCacheKeys"/> so the
/// invalidation always targets the same key the read path produces.
/// </summary>
public sealed class PersonCacheInvalidator : IPersonCache
{
    private readonly ICacheService _cache;

    public PersonCacheInvalidator(ICacheService cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
    }

    public Task InvalidateAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(personId);
        return _cache.RemoveAsync(PersonCacheKeys.ForDetail(personId), cancellationToken);
    }
}
