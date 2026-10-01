using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CandidateAssessment.Api.Configuration;
using CandidateAssessment.Application.Abstractions.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CandidateAssessment.Api.Authentication;

/// <summary>
/// Issues short-lived JWT access tokens using the configured signing key.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public TokenDescriptor IssueToken(IEnumerable<Claim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var nowUtc = DateTime.UtcNow;
        var expiresAt = nowUtc.AddMinutes(_options.ExpiresInMinutes);

        var keyBytes = Encoding.UTF8.GetBytes(_options.SigningKey);
        var key = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: nowUtc,
            expires: expiresAt,
            signingCredentials: creds);

        var value = new JwtSecurityTokenHandler().WriteToken(token);
        return new TokenDescriptor(value, expiresAt);
    }
}
