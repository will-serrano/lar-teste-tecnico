using System.Security.Claims;

namespace CandidateAssessment.Application.Abstractions.Authentication;

/// <summary>
/// Emite tokens de acesso JWT de curta duração para usuários autenticados.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Cria um token de acesso assinado para a identidade informada.
    /// </summary>
    TokenDescriptor IssueToken(IEnumerable<Claim> claims);
}

public sealed record TokenDescriptor(string Token, DateTime ExpiresAtUtc);
