using CandidateAssessment.Application.Abstractions.Caching;

namespace CandidateAssessment.UnitTests.Fakes;

/// <summary>
/// Builds cache-related fakes consistently across handler tests.
/// </summary>
internal static class TestCacheFactory
{
    public static (FakeCacheService Cache, FakePersonCache PersonCache) Create()
    {
        var cache = new FakeCacheService();
        var personCache = new FakePersonCache(cache);
        return (cache, personCache);
    }
}
