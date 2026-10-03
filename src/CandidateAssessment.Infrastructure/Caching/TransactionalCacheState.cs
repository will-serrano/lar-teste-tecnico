namespace CandidateAssessment.Infrastructure.Caching;

public sealed class TransactionalCacheState
{
    private readonly HashSet<string> _invalidations = new(StringComparer.Ordinal);

    public bool IsActive { get; private set; }

    public void Begin()
    {
        if (IsActive)
        {
            throw new InvalidOperationException("A cache transaction is already active.");
        }

        IsActive = true;
    }

    public void Invalidate(string key) => _invalidations.Add(key);

    public async Task CommitAsync(MemoryCacheService cache)
    {
        IsActive = false;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            foreach (var key in _invalidations)
            {
                await cache.RemoveAsync(key, timeout.Token);
                CacheDiagnostics.Record("invalidate", "removed");
            }
        }
        finally
        {
            _invalidations.Clear();
        }
    }

    public void Reset()
    {
        IsActive = false;
        _invalidations.Clear();
    }
}
