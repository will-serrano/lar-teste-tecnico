using CandidateAssessment.Application.Abstractions.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace CandidateAssessment.Infrastructure.Caching;

/// <summary>
/// In-process memory cache implementation backed by <see cref="IMemoryCache"/>.
/// Application use cases are intentionally unaware of the backing technology
/// (this class is only resolved via <see cref="ICacheService"/>).
/// </summary>
public sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        var value = _cache.TryGetValue(key, out var entry) ? entry as T : null;

        return Task.FromResult(value);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan absoluteExpiration,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        using var entry = _cache.CreateEntry(key);
        entry.Value = value;

        if (absoluteExpiration > TimeSpan.Zero)
        {
            entry.AbsoluteExpirationRelativeToNow = absoluteExpiration;
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        _cache.Remove(key);

        return Task.CompletedTask;
    }
}
