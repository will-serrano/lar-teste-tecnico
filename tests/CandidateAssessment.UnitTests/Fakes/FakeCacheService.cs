using CandidateAssessment.Application.Abstractions.Caching;

namespace CandidateAssessment.UnitTests.Fakes;

/// <summary>
/// Implementação substituta de <see cref="ICacheService"/> em memória para testes de unidade.
/// Armazena entradas em um dicionário para que os testes possam verificar o comportamento
/// de Get/Set/Remove.
/// </summary>
internal sealed class FakeCacheService : ICacheService
{
    private readonly Dictionary<string, object> _store = new();

    public int GetCount { get; private set; }

    public int SetCount { get; private set; }

    public int RemoveCount { get; private set; }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class
    {
        GetCount++;
        var value = _store.TryGetValue(key, out var entry) ? entry as T : null;
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan absoluteExpiration,
        CancellationToken cancellationToken = default)
        where T : class
    {
        SetCount++;
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        RemoveCount++;
        _store.Remove(key);
        return Task.CompletedTask;
    }
}
