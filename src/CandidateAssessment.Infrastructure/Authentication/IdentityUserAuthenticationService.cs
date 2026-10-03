using CandidateAssessment.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Identity;

namespace CandidateAssessment.Infrastructure.Authentication;

/// <summary>
/// Implementação de <see cref="IUserAuthenticationService"/> baseada no Identity.
/// Mantém os tipos de Microsoft.AspNetCore.Identity fora da camada Application.
/// </summary>
public sealed class IdentityUserAuthenticationService : IUserAuthenticationService
{
    private readonly UserManager<IdentityUser> _userManager;

    public IdentityUserAuthenticationService(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    public async Task<AuthenticatedUser?> FindByNameAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        var user = await _userManager.FindByNameAsync(userName);
        return user is null ? null : new AuthenticatedUser(user.Id, user.UserName ?? string.Empty);
    }

    public async Task<bool> CheckPasswordAsync(
        AuthenticatedUser user,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var identityUser = await _userManager.FindByIdAsync(user.Id);
        if (identityUser is null)
        {
            return false;
        }

        return await _userManager.CheckPasswordAsync(identityUser, password);
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(
        AuthenticatedUser user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var identityUser = await _userManager.FindByIdAsync(user.Id);
        if (identityUser is null)
        {
            return [];
        }

        var roles = await _userManager.GetRolesAsync(identityUser);
        return [.. roles];
    }
}
