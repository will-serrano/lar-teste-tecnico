namespace CandidateAssessment.Api.Configuration;

/// <summary>
/// Tipo reservado para a vinculação de Cache na camada da API (mantido separado do tipo
/// <see cref="CandidateAssessment.Application.Abstractions.Caching.CacheOptions"/>)
/// para evitar uma referência entre camadas durante a composição.
/// As opções efetivamente usadas pelos handlers são resolvidas da configuração via DI.
/// </summary>
internal static class CacheSection
{
    public const string Name = "Cache";
}
