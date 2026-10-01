namespace CandidateAssessment.Application.Authentication.Login;

public sealed record LoginCommand(
    string Username,
    string Password);
