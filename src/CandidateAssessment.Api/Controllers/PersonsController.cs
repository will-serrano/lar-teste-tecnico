using CandidateAssessment.Api.Authorization;
using CandidateAssessment.Api.Contracts;
using CandidateAssessment.Api.Contracts.Persons;
using CandidateAssessment.Application.Persons.Create;
using CandidateAssessment.Application.Persons.Delete;
using CandidateAssessment.Application.Persons.GetById;
using CandidateAssessment.Application.Persons.GetDeleted;
using CandidateAssessment.Application.Persons.Restore;
using CandidateAssessment.Application.Persons.Search;
using CandidateAssessment.Application.Persons.Update;
using FluentValidation;
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
    private readonly CreatePersonHandler _createHandler;
    private readonly GetPersonByIdHandler _getByIdHandler;
    private readonly SearchPersonsHandler _searchHandler;
    private readonly UpdatePersonHandler _updateHandler;
    private readonly DeletePersonHandler _deleteHandler;
    private readonly RestorePersonHandler _restoreHandler;
    private readonly GetDeletedPersonsHandler _getDeletedHandler;
    private readonly IValidator<CreatePersonRequest> _createValidator;
    private readonly IValidator<UpdatePersonRequest> _updateValidator;

    public PersonsController(
        CreatePersonHandler createHandler,
        GetPersonByIdHandler getByIdHandler,
        SearchPersonsHandler searchHandler,
        UpdatePersonHandler updateHandler,
        DeletePersonHandler deleteHandler,
        RestorePersonHandler restoreHandler,
        GetDeletedPersonsHandler getDeletedHandler,
        IValidator<CreatePersonRequest> createValidator,
        IValidator<UpdatePersonRequest> updateValidator)
    {
        _createHandler = createHandler;
        _getByIdHandler = getByIdHandler;
        _searchHandler = searchHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _restoreHandler = restoreHandler;
        _getDeletedHandler = getDeletedHandler;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpPost]
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

        var command = new CreatePersonCommand(request.Name, request.Cpf, request.BirthDate);
        var id = await _createHandler.HandleAsync(command, cancellationToken);

        var response = new PersonResponse
        {
            Id = id,
            Name = request.Name.Trim(),
            Cpf = CpfFormatter.Format(request.Cpf),
            BirthDate = request.BirthDate,
            IsActive = true,
        };

        return CreatedAtAction(nameof(GetById), new { id }, response);
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
        var query = new GetPersonByIdQuery(id);
        var cached = await _getByIdHandler.HandleAsync(query, cancellationToken);
        return Ok(cached.ToResponse());
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.CanReadPersons)]
    [ProducesResponseType(typeof(PagedResponse<PersonResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<PersonResponse>>> Search(
        [FromQuery] string? name,
        [FromQuery] string? cpf,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = new SearchPersonsQuery(name, cpf, page, pageSize);
        var result = await _searchHandler.HandleAsync(query, cancellationToken);
        return Ok(result.ToResponse(p => p.ToResponse()));
    }

    [HttpPut("{id:guid}")]
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

        var command = new UpdatePersonCommand(id, request.Name, request.BirthDate);
        await _updateHandler.HandleAsync(command, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePersons)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeletePersonCommand(id);
        await _deleteHandler.HandleAsync(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
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
        var command = new RestorePersonCommand(id);
        await _restoreHandler.HandleAsync(command, cancellationToken);
        return NoContent();
    }

    [HttpGet("deleted")]
    [Authorize(Policy = AuthorizationPolicies.CanViewDeletedPersons)]
    [ProducesResponseType(typeof(PagedResponse<PersonResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<PersonResponse>>> GetDeleted(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = new GetDeletedPersonsQuery(page, pageSize);
        var result = await _getDeletedHandler.HandleAsync(query, cancellationToken);
        return Ok(result.ToResponse(p => p.ToResponse()));
    }
}
