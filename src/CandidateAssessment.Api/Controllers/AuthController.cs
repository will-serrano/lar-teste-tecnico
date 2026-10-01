using CandidateAssessment.Api.Contracts.Auth;
using CandidateAssessment.Application.Authentication.Login;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiVersionAttribute = Asp.Versioning.ApiVersionAttribute;

namespace CandidateAssessment.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/auth")]
[Produces("application/json")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly LoginHandler _loginHandler;
    private readonly IValidator<LoginRequest> _validator;

    public AuthController(LoginHandler loginHandler, IValidator<LoginRequest> validator)
    {
        _loginHandler = loginHandler;
        _validator = validator;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(
                    string.IsNullOrEmpty(error.PropertyName) ? "request" : error.PropertyName,
                    error.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        var command = new LoginCommand(request.Username, request.Password);
        var result = await _loginHandler.HandleAsync(command, cancellationToken);

        return Ok(new LoginResponse
        {
            AccessToken = result.AccessToken,
            ExpiresAtUtc = result.ExpiresAtUtc,
            Username = result.Username,
            Roles = result.Roles,
        });
    }
}
