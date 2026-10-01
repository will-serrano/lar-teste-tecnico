namespace CandidateAssessment.Domain.Roles;

/// <summary>
/// Centralized role names used by the application. Kept in the Domain because
/// they are part of the business identity of the system, not a transport concern.
/// </summary>
public static class ApplicationRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
}
