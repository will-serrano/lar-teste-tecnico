namespace CandidateAssessment.Application.Abstractions.Caching;

/// <summary>
/// Strongly typed cache policy used by Application handlers.
/// Bound from configuration in the Infrastructure composition root.
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>
    /// Default absolute expiration for cached Person detail entries.
    /// A value of zero or less disables expiration.
    /// </summary>
    public TimeSpan PersonDetailAbsoluteExpiration { get; set; } = TimeSpan.FromMinutes(5);
}
