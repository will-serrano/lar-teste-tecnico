using System.ComponentModel.DataAnnotations;

namespace CandidateAssessment.Api.Configuration;

/// <summary>
/// Configuration for seeding initial Admin and User accounts on startup.
/// Passwords are never stored in source-controlled configuration files.
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
