namespace CandidateAssessment.Api.Authorization;


/// <summary>
/// Named authorization policies. Centralizing them here keeps controllers free
/// of role-name string literals and makes the role-to-permission mapping auditable.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Read access to persons — both Admin and User roles.
    /// </summary>
    public const string CanReadPersons = nameof(CanReadPersons);

    /// <summary>
    /// Manage access to persons (create, update, delete, restore) — Admin only.
    /// </summary>
    public const string CanManagePersons = nameof(CanManagePersons);

    /// <summary>
    /// Access to the administrative endpoint listing soft-deleted persons — Admin only.
    /// </summary>
    public const string CanViewDeletedPersons = nameof(CanViewDeletedPersons);
}
