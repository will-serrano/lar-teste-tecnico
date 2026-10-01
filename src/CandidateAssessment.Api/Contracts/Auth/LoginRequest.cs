namespace CandidateAssessment.Api.Contracts.Auth;

public sealed class LoginRequest
{
    public string Username { get; init; } = default!;

    public string Password { get; init; } = default!;
}
