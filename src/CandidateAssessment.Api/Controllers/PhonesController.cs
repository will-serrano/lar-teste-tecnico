using CandidateAssessment.Api.Authorization;
using CandidateAssessment.Api.Contracts.Phones;
using CandidateAssessment.Application.Phones.Create;
using CandidateAssessment.Application.Phones.Delete;
using CandidateAssessment.Application.Phones.GetById;
using CandidateAssessment.Application.Phones.List;
using CandidateAssessment.Application.Phones.Update;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiVersionAttribute = Asp.Versioning.ApiVersionAttribute;

namespace CandidateAssessment.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/persons/{personId:guid}/phones")]
[Produces("application/json")]
[Authorize]
public class PhonesController : ControllerBase
{
    private readonly CreatePhoneHandler _createHandler;
    private readonly UpdatePhoneHandler _updateHandler;
    private readonly DeletePhoneHandler _deleteHandler;
    private readonly GetPhoneByIdHandler _getByIdHandler;
    private readonly ListPhonesHandler _listHandler;
    private readonly IValidator<CreatePhoneRequest> _createValidator;
    private readonly IValidator<UpdatePhoneRequest> _updateValidator;

    public PhonesController(
        CreatePhoneHandler createHandler,
        UpdatePhoneHandler updateHandler,
        DeletePhoneHandler deleteHandler,
        GetPhoneByIdHandler getByIdHandler,
        ListPhonesHandler listHandler,
        IValidator<CreatePhoneRequest> createValidator,
        IValidator<UpdatePhoneRequest> updateValidator)
    {
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _getByIdHandler = getByIdHandler;
        _listHandler = listHandler;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.CanReadPersons)]
    [ProducesResponseType(typeof(IReadOnlyList<PhoneResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PhoneResponse>>> List(
        Guid personId,
        CancellationToken cancellationToken)
    {
        var query = new ListPhonesQuery(personId);
        var phones = await _listHandler.HandleAsync(query, cancellationToken);
        return Ok(phones.Select(p => p.ToResponse()).ToList());
    }

    [HttpGet("{phoneId:guid}", Name = nameof(GetPhoneById))]
    [Authorize(Policy = AuthorizationPolicies.CanReadPersons)]
    [ProducesResponseType(typeof(PhoneResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PhoneResponse>> GetPhoneById(
        Guid personId,
        Guid phoneId,
        CancellationToken cancellationToken)
    {
        var query = new GetPhoneByIdQuery(personId, phoneId);
        var phone = await _getByIdHandler.HandleAsync(query, cancellationToken);
        return Ok(phone.ToResponse());
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanManagePersons)]
    [ProducesResponseType(typeof(PhoneResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PhoneResponse>> Create(
        Guid personId,
        [FromBody] CreatePhoneRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
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

        var command = new CreatePhoneCommand(personId, request.Type, request.Number);
        var id = await _createHandler.HandleAsync(command, cancellationToken);

        var query = new GetPhoneByIdQuery(personId, id);
        var phone = await _getByIdHandler.HandleAsync(query, cancellationToken);

        return CreatedAtRoute(nameof(GetPhoneById), new { personId, phoneId = id }, phone.ToResponse());
    }

    [HttpPut("{phoneId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePersons)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid personId,
        Guid phoneId,
        [FromBody] UpdatePhoneRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
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

        var command = new UpdatePhoneCommand(personId, phoneId, request.Type, request.Number);
        await _updateHandler.HandleAsync(command, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{phoneId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePersons)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid personId,
        Guid phoneId,
        CancellationToken cancellationToken)
    {
        var command = new DeletePhoneCommand(personId, phoneId);
        await _deleteHandler.HandleAsync(command, cancellationToken);
        return NoContent();
    }
}
