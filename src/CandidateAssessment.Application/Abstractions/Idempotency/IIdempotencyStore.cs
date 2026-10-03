namespace CandidateAssessment.Application.Abstractions.Idempotency;

public sealed record IdempotencyResponse(int StatusCode, byte[] Body, string? ContentType, string? Location);

public sealed record IdempotencyEntry(string Fingerprint, IdempotencyResponse Response);

public interface IIdempotencySession : IAsyncDisposable
{
    IdempotencyEntry? Existing { get; }

    Task CompleteAsync(IdempotencyResponse response, TimeSpan lifetime, CancellationToken cancellationToken);
}

public interface IIdempotencyStore
{
    Task<IdempotencyEntry?> FindAsync(string scopeHash, string keyHash, int lockTimeoutSeconds, CancellationToken cancellationToken);

    Task<IIdempotencySession> BeginAsync(
        string scopeHash, string keyHash, string fingerprint, int lockTimeoutSeconds, CancellationToken cancellationToken);

    Task<int> DeleteExpiredAsync(int batchSize, int lockTimeoutSeconds, CancellationToken cancellationToken);
}

public sealed class IdempotencyBusyException : Exception
{
    public IdempotencyBusyException(Exception innerException)
        : base("The database is busy. Retry the operation with the same idempotency key.", innerException)
    {
    }
}
