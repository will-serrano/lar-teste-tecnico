namespace CandidateAssessment.Application.Abstractions.Authentication;

/// <summary>
/// Abstraction over ASP.NET Core Identity user/role operations, keeping the
/// Application layer free of direct dependency on Microsoft.AspNetCore.Identity.
/// </summary>
public interface IUserAuthenticationService
{
    Task<AuthenticatedUser?> FindByNameAsync(
        string userName,
        CancellationToken cancellationToken = default);

    Task<bool> CheckPasswordAsync(
        AuthenticatedUser user,
        string password,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRolesAsync(
        AuthenticatedUser user,
        CancellationToken cancellationToken = default);
}

public sealed record AuthenticatedUser(string Id, string UserName);
