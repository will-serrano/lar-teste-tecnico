namespace CandidateAssessment.Application.Abstractions.Caching;

/// <summary>
/// Política de cache fortemente tipada usada pelos handlers da camada Application.
/// Vinculada à configuração na raiz de composição da camada Infrastructure.
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>
    /// Expiração absoluta padrão para entradas em cache com detalhes de pessoa.
    /// Um valor igual ou menor que zero desativa a expiração.
    /// </summary>
    public TimeSpan PersonDetailAbsoluteExpiration { get; set; } = TimeSpan.FromMinutes(5);
}
