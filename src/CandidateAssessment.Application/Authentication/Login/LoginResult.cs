namespace CandidateAssessment.Application.Authentication.Login;

public sealed record LoginResult(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string Username,
    IReadOnlyList<string> Roles);
