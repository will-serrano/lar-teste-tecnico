using System.ComponentModel.DataAnnotations;

namespace CandidateAssessment.Api.Configuration;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    public TimeSpan Lifetime { get; init; } = TimeSpan.FromHours(24);

    [Range(1, 30)]
    public int LockTimeoutSeconds { get; init; } = 2;

    [Range(1, 16 * 1024 * 1024)]
    public int MaxRequestBytes { get; init; } = 1024 * 1024;

    [Range(1, 16 * 1024 * 1024)]
    public int MaxResponseBytes { get; init; } = 1024 * 1024;

    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromHours(1);

    [Range(1, 1000)]
    public int CleanupBatchSize { get; init; } = 100;
}
