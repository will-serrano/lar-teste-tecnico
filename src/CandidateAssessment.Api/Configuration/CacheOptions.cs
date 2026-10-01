namespace CandidateAssessment.Api.Configuration;

/// <summary>
/// Placeholder for API-layer Cache binding (kept distinct from Application's
/// <see cref="CandidateAssessment.Application.Abstractions.Caching.CacheOptions"/>)
/// to avoid a cross-layer reference at composition time.
/// The actual handler-level options are resolved from configuration through DI.
/// </summary>
internal static class CacheSection
{
    public const string Name = "Cache";
}
