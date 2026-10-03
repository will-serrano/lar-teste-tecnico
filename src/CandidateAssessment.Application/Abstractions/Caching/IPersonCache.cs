namespace CandidateAssessment.Application.Abstractions.Caching;

/// <summary>
/// Centraliza a invalidação do cache do agregado Person para que os handlers de mutação
/// não precisem construir chaves de cache. Mantém os fluxos de leitura e gravação sincronizados.
/// </summary>
public interface IPersonCache
{
    /// <summary>
    /// Remove do cache os detalhes da pessoa com o ID informado, se existirem.
    /// </summary>
    Task InvalidateAsync(Guid personId, CancellationToken cancellationToken = default);
}
