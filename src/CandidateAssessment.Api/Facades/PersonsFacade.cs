using CandidateAssessment.Application.Abstractions.Pagination;
using CandidateAssessment.Application.Persons.Create;
using CandidateAssessment.Application.Persons.Delete;
using CandidateAssessment.Application.Persons.GetById;
using CandidateAssessment.Application.Persons.GetDeleted;
using CandidateAssessment.Application.Persons.Restore;
using CandidateAssessment.Application.Persons.Search;
using CandidateAssessment.Application.Persons.Update;
using CandidateAssessment.Domain.Entities;

namespace CandidateAssessment.Api.Facades;

/// <summary>
/// Ponto de entrada enxuto da camada Application para o contexto delimitado <c>Persons</c>.
/// Existe apenas para evitar que os controllers precisem injetar cada handler de caso de uso
/// individualmente, preservando a arquitetura Vertical Slice na camada Application —
/// cada handler mantém suas próprias dependências, testes e ciclo de vida.
///
/// Não há lógica de negócio aqui; cada método apenas delega ao respectivo handler.
/// O construtor do controller passa de 11 dependências (7 handlers + 4 validadores) para 1.
/// </summary>
public sealed class PersonsFacade
{
    private readonly CreatePersonHandler _create;
    private readonly GetPersonByIdHandler _getById;
    private readonly SearchPersonsHandler _search;
    private readonly UpdatePersonHandler _update;
    private readonly DeletePersonHandler _delete;
    private readonly RestorePersonHandler _restore;
    private readonly GetDeletedPersonsHandler _getDeleted;

    public PersonsFacade(
        CreatePersonHandler create,
        GetPersonByIdHandler getById,
        SearchPersonsHandler search,
        UpdatePersonHandler update,
        DeletePersonHandler delete,
        RestorePersonHandler restore,
        GetDeletedPersonsHandler getDeleted)
    {
        _create = create;
        _getById = getById;
        _search = search;
        _update = update;
        _delete = delete;
        _restore = restore;
        _getDeleted = getDeleted;
    }

    public Task<Guid> CreateAsync(CreatePersonCommand command, CancellationToken cancellationToken)
        => _create.HandleAsync(command, cancellationToken);

    public Task<CachedPerson> GetByIdAsync(GetPersonByIdQuery query, CancellationToken cancellationToken)
        => _getById.HandleAsync(query, cancellationToken);

    public Task<PagedResult<Person>> SearchAsync(SearchPersonsQuery query, CancellationToken cancellationToken)
        => _search.HandleAsync(query, cancellationToken);

    public Task UpdateAsync(UpdatePersonCommand command, CancellationToken cancellationToken)
        => _update.HandleAsync(command, cancellationToken);

    public Task DeleteAsync(DeletePersonCommand command, CancellationToken cancellationToken)
        => _delete.HandleAsync(command, cancellationToken);

    public Task RestoreAsync(RestorePersonCommand command, CancellationToken cancellationToken)
        => _restore.HandleAsync(command, cancellationToken);

    public Task<PagedResult<Person>> GetDeletedAsync(GetDeletedPersonsQuery query, CancellationToken cancellationToken)
        => _getDeleted.HandleAsync(query, cancellationToken);
}
