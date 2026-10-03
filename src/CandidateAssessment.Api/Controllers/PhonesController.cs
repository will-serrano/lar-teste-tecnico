using CandidateAssessment.Api.Authorization;
using CandidateAssessment.Api.Contracts.Phones;
using CandidateAssessment.Api.Facades;
using CandidateAssessment.Api.Idempotency;
using CandidateAssessment.Application.Phones.Create;
using CandidateAssessment.Application.Phones.Delete;
using CandidateAssessment.Application.Phones.GetById;
using CandidateAssessment.Application.Phones.Update;
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
    private readonly PhonesFacade _phones;

    public PhonesController(PhonesFacade phones)
    {
        _phones = phones;
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
        var phones = await _phones.ListAsync(new Application.Phones.List.ListPhonesQuery(personId), cancellationToken);
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
        var phone = await _phones.GetByIdAsync(new GetPhoneByIdQuery(personId, phoneId), cancellationToken);
        return Ok(phone.ToResponse());
    }

    [HttpPost]
    [Idempotent]
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
        var id = await _phones.CreateAsync(new CreatePhoneCommand(personId, request.Type, request.Number), cancellationToken);
        var phone = await _phones.GetByIdAsync(new GetPhoneByIdQuery(personId, id), cancellationToken);
        return CreatedAtRoute(nameof(GetPhoneById), new { personId, phoneId = id }, phone.ToResponse());
    }

    [HttpPut("{phoneId:guid}")]
    [Idempotent]
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
        await _phones.UpdateAsync(new UpdatePhoneCommand(personId, phoneId, request.Type, request.Number), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{phoneId:guid}")]
    [Idempotent]
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
        await _phones.DeleteAsync(new DeletePhoneCommand(personId, phoneId), cancellationToken);
        return NoContent();
    }
}
