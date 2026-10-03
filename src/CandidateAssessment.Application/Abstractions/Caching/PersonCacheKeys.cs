namespace CandidateAssessment.Application.Abstractions.Caching;

/// <summary>
/// Centraliza as chaves de cache do agregado Person para manter produtores e consumidores
/// sincronizados. A invalidação de qualquer detalhe da pessoa também abrange o fluxo de
/// leitura por ID de pessoa ativa.
/// </summary>
public static class PersonCacheKeys
{
    private const string PersonDetailPrefix = "person:detail:";

    public static string ForDetail(Guid personId) => $"{PersonDetailPrefix}{personId:N}";
}
