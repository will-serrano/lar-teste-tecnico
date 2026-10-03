namespace CandidateAssessment.Infrastructure.Idempotency;

public sealed class IdempotencyRecord
{
    public string ScopeHash { get; set; } = string.Empty;

    public string KeyHash { get; set; } = string.Empty;

    public string Fingerprint { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public byte[] Body { get; set; } = Array.Empty<byte>();

    public string? ContentType { get; set; }

    public string? Location { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
}
