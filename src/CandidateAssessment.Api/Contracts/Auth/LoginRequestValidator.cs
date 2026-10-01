using FluentValidation;

namespace CandidateAssessment.Api.Contracts.Auth;

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Username)
            .NotEmpty()
            .WithMessage("Username is required.");

        RuleFor(r => r.Password)
            .NotEmpty()
            .WithMessage("Password is required.");
    }
}
