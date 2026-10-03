namespace CandidateAssessment.UnitTests.Fakes;

/// <summary>
/// Cria dublês de cache de forma consistente entre os testes dos handlers.
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
