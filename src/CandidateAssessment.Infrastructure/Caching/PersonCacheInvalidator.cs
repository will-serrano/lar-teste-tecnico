using CandidateAssessment.Application.Abstractions.Caching;

namespace CandidateAssessment.Infrastructure.Caching;

/// <summary>
/// Implementação de <see cref="IPersonCache"/> baseada em memória.
/// Obtém a chave de cache por meio de <see cref="PersonCacheKeys"/> para que a
/// invalidação sempre use a mesma chave gerada pelo fluxo de leitura.
/// </summary>
public sealed class PersonCacheInvalidator : IPersonCache
{
    private readonly ICacheService _cache;

    public PersonCacheInvalidator(ICacheService cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
    }

    public Task InvalidateAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        // personId é um tipo por valor, portanto uma verificação explícita de null é redundante
        // (CA2264). O cancelamento é encaminhado à chamada do cache subjacente.
        return _cache.RemoveAsync(PersonCacheKeys.ForDetail(personId), cancellationToken);
    }
}
