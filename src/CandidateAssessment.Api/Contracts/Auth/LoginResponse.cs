namespace CandidateAssessment.Api.Contracts.Auth;

public sealed class LoginResponse
{
    public string AccessToken { get; init; } = default!;

    public DateTime ExpiresAtUtc { get; init; }

    public string Username { get; init; } = default!;

    public IReadOnlyList<string> Roles { get; init; } = [];
}
