using CandidateAssessment.Api.Authorization;
using CandidateAssessment.Api.Contracts;
using CandidateAssessment.Api.Contracts.Persons;
using CandidateAssessment.Api.Facades;
using CandidateAssessment.Api.Idempotency;
using CandidateAssessment.Application.Persons.Create;
using CandidateAssessment.Application.Persons.Delete;
using CandidateAssessment.Application.Persons.GetById;
using CandidateAssessment.Application.Persons.GetDeleted;
using CandidateAssessment.Application.Persons.Restore;
using CandidateAssessment.Application.Persons.Search;
using CandidateAssessment.Application.Persons.Update;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiVersionAttribute = Asp.Versioning.ApiVersionAttribute;

namespace CandidateAssessment.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/persons")]
[Produces("application/json")]
[Authorize]
public class PersonsController : ControllerBase
{
    private readonly PersonsFacade _persons;

    public PersonsController(PersonsFacade persons)
    {
        _persons = persons;
    }

    [HttpPost]
    [Idempotent]
    [Authorize(Policy = AuthorizationPolicies.CanManagePersons)]
    [ProducesResponseType(typeof(PersonResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonResponse>> Create(
        [FromBody] CreatePersonRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreatePersonCommand(request.Name, request.Cpf, request.BirthDate);
        var id = await _persons.CreateAsync(command, cancellationToken);

        // Lê a pessoa novamente para que a resposta 201 inclua os timestamps persistidos pelo
        // domínio (CreatedAtUtc/UpdatedAtUtc). Criar o DTO diretamente a partir da requisição —
        // como fazia a versão anterior — descartava esses campos silenciosamente e fazia com que
        // aparecessem como DateTime.MinValue (0001-01-01T00:00:00). Segue o padrão já usado por
        // PhonesController.Create.
        var cached = await _persons.GetByIdAsync(new GetPersonByIdQuery(id), cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, cached.ToResponse());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanReadPersons)]
    [ProducesResponseType(typeof(PersonResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var cached = await _persons.GetByIdAsync(new GetPersonByIdQuery(id), cancellationToken);
        return Ok(cached.ToResponse());
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.CanReadPersons)]
    [ProducesResponseType(typeof(PagedResponse<PersonResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<PersonResponse>>> Search(
        [FromQuery] SearchPersonsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _persons.SearchAsync(query, cancellationToken);
        return Ok(result.ToResponse(p => p.ToResponse()));
    }

    [HttpPut("{id:guid}")]
    [Idempotent]
    [Authorize(Policy = AuthorizationPolicies.CanManagePersons)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePersonRequest request,
        CancellationToken cancellationToken)
    {
        await _persons.UpdateAsync(new UpdatePersonCommand(id, request.Name, request.BirthDate), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Idempotent]
    [Authorize(Policy = AuthorizationPolicies.CanManagePersons)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _persons.DeleteAsync(new DeletePersonCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    [Idempotent]
    [Authorize(Policy = AuthorizationPolicies.CanManagePersons)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Restore(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _persons.RestoreAsync(new RestorePersonCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpGet("deleted")]
    [Authorize(Policy = AuthorizationPolicies.CanViewDeletedPersons)]
    [ProducesResponseType(typeof(PagedResponse<PersonResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<PersonResponse>>> GetDeleted(
        [FromQuery] GetDeletedPersonsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _persons.GetDeletedAsync(query, cancellationToken);
        return Ok(result.ToResponse(p => p.ToResponse()));
    }
}
