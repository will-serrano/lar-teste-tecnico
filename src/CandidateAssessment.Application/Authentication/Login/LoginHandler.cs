using System.Security.Claims;
using CandidateAssessment.Application.Abstractions.Authentication;
using CandidateAssessment.Application.Exceptions;

namespace CandidateAssessment.Application.Authentication.Login;

/// <summary>
/// Autentica um usuário pelo ASP.NET Core Identity e retorna um token de acesso JWT.
/// </summary>
public sealed class LoginHandler
{
    public const string InvalidCredentialsCode = "InvalidCredentials";

    private readonly IUserAuthenticationService _userAuthentication;
    private readonly ITokenService _tokenService;

    public LoginHandler(
        IUserAuthenticationService userAuthentication,
        ITokenService tokenService)
    {
        _userAuthentication = userAuthentication;
        _tokenService = tokenService;
    }

    public async Task<LoginResult> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await _userAuthentication.FindByNameAsync(command.Username, cancellationToken) ?? throw InvalidCredentials();
        var passwordValid = await _userAuthentication.CheckPasswordAsync(
            user,
            command.Password,
            cancellationToken);
        if (!passwordValid)
        {
            throw InvalidCredentials();
        }

        var roles = await _userAuthentication.GetRolesAsync(user, cancellationToken);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = _tokenService.IssueToken(claims);
        return new LoginResult(token.Token, token.ExpiresAtUtc, user.UserName, roles);
    }

    private static ApplicationValidationException InvalidCredentials()
        => new(InvalidCredentialsCode, "Invalid username or password.");
}
