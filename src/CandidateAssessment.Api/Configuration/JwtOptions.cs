using System.ComponentModel.DataAnnotations;

namespace CandidateAssessment.Api.Configuration;

/// <summary>
/// Configuração JWT fortemente tipada. Vinculada à seção "Jwt".
/// Em produção, os segredos devem ser fornecidos por variáveis de ambiente ou por um gerenciador de segredos.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = default!;

    [Required]
    public string Audience { get; set; } = default!;

    [Required]
    public string SigningKey { get; set; } = default!;

    /// <summary>
    /// Tempo de vida do token de acesso em minutos. O padrão é 60.
    /// </summary>
    [Range(1, 24 * 60)]
    public int ExpiresInMinutes { get; set; } = 60;
}
