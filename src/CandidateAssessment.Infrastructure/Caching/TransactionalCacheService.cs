using CandidateAssessment.Application.Abstractions.Caching;

namespace CandidateAssessment.Infrastructure.Caching;

public sealed class TransactionalCacheService : ICacheService
{
    private readonly MemoryCacheService _cache;
    private readonly TransactionalCacheState _state;

    public TransactionalCacheService(MemoryCacheService cache, TransactionalCacheState state)
    {
        _cache = cache;
        _state = state;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        using var activity = CacheDiagnostics.Source.StartActivity("cache.get");
        cancellationToken.ThrowIfCancellationRequested();
        var value = _state.IsActive ? null : await _cache.GetAsync<T>(key, cancellationToken);
        CacheDiagnostics.Record("get", _state.IsActive ? "bypass" : value is null ? "miss" : "hit");
        return value;
    }

    public Task SetAsync<T>(string key, T value, TimeSpan absoluteExpiration, CancellationToken cancellationToken = default)
        where T : class
    {
        using var activity = CacheDiagnostics.Source.StartActivity("cache.set");
        cancellationToken.ThrowIfCancellationRequested();
        CacheDiagnostics.Record("set", _state.IsActive ? "bypass" : "stored");
        return _state.IsActive ? Task.CompletedTask : _cache.SetAsync(key, value, absoluteExpiration, cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        using var activity = CacheDiagnostics.Source.StartActivity("cache.invalidate");
        cancellationToken.ThrowIfCancellationRequested();
        CacheDiagnostics.Record("invalidate", _state.IsActive ? "deferred" : "removed");
        if (!_state.IsActive)
        {
            return _cache.RemoveAsync(key, cancellationToken);
        }

        _state.Invalidate(key);
        return Task.CompletedTask;
    }
}
