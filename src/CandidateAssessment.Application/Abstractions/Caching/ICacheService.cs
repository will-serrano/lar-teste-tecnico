namespace CandidateAssessment.Application.Abstractions.Caching;

/// <summary>
/// Abstração da camada de cache da aplicação.
/// Os casos de uso da camada Application dependem deste contrato, nunca de <c>IMemoryCache</c>,
/// permitindo substituir a implementação subjacente (memória, Redis ou distribuída) sem
/// alterar Domain ou Application.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retorna o valor em cache para <paramref name="key"/> ou <c>null</c> quando não existir.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Armazena <paramref name="value"/> com a chave <paramref name="key"/> pela duração informada.
    /// Um <paramref name="absoluteExpiration"/> não positivo grava sem expiração.
    /// </summary>
    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan absoluteExpiration,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Remove a entrada de <paramref name="key"/> quando existir. Caso contrário, não faz nada.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
