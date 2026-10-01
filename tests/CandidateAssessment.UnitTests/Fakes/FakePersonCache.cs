using CandidateAssessment.Application.Abstractions.Caching;

namespace CandidateAssessment.UnitTests.Fakes;

internal sealed class FakePersonCache : IPersonCache
{
    private readonly FakeCacheService _cache;

    public FakePersonCache(FakeCacheService cache)
    {
        _cache = cache;
    }

    public Task InvalidateAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        return _cache.RemoveAsync(PersonCacheKeys.ForDetail(personId), cancellationToken);
    }
}
