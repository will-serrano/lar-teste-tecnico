namespace CandidateAssessment.Application.Abstractions.Caching;

/// <summary>
/// Centralizes cache keys for the Person aggregate so producers and consumers
/// stay in sync. Invalidation of any person detail also covers the
/// "active person by id" read path.
/// </summary>
public static class PersonCacheKeys
{
    private const string PersonDetailPrefix = "person:detail:";

    public static string ForDetail(Guid personId) => $"{PersonDetailPrefix}{personId:N}";
}
