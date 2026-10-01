using System.Security.Claims;

namespace CandidateAssessment.Application.Abstractions.Authentication;

/// <summary>
/// Issues short-lived JWT access tokens for authenticated users.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Builds a signed access token for the supplied identity.
    /// </summary>
    TokenDescriptor IssueToken(IEnumerable<Claim> claims);
}

public sealed record TokenDescriptor(string Token, DateTime ExpiresAtUtc);
