using System.ComponentModel.DataAnnotations;

namespace CandidateAssessment.Api.Configuration;

/// <summary>
/// Configuração para criar as contas iniciais Admin e User na inicialização.
/// Senhas nunca são armazenadas em arquivos de configuração versionados.
/// </summary>
public sealed class SeedUsersOptions
{
    public const string SectionName = "SeedUsers";

    public SeedUserAccount Admin { get; set; } = new();

    public SeedUserAccount User { get; set; } = new();
}

public sealed class SeedUserAccount
{
    [Required]
    public string Username { get; set; } = default!;

    [Required]
    public string Password { get; set; } = default!;
}
