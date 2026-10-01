using System.ComponentModel.DataAnnotations;

namespace CandidateAssessment.Api.Configuration;

/// <summary>
/// Strongly-typed JWT configuration. Bound from the "Jwt" section.
/// Secrets must be supplied via environment variables or a secret manager in production.
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
    /// Access token lifetime in minutes. Defaults to 60.
    /// </summary>
    [Range(1, 24 * 60)]
    public int ExpiresInMinutes { get; set; } = 60;
}
