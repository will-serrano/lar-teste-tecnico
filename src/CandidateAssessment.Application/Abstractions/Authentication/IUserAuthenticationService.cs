namespace CandidateAssessment.Application.Abstractions.Authentication;

/// <summary>
/// Abstração das operações de usuário/papel do ASP.NET Core Identity, mantendo a
/// camada Application sem dependência direta de Microsoft.AspNetCore.Identity.
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
