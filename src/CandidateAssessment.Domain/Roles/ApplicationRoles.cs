namespace CandidateAssessment.Domain.Roles;

/// <summary>
/// Nomes de papéis usados pela aplicação, centralizados. Mantidos no Domain porque
/// fazem parte da identidade de negócio do sistema, não da camada de transporte.
/// </summary>
public static class ApplicationRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
}
