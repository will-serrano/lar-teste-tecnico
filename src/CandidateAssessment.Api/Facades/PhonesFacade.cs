using CandidateAssessment.Application.Phones.Create;
using CandidateAssessment.Application.Phones.Delete;
using CandidateAssessment.Application.Phones.GetById;
using CandidateAssessment.Application.Phones.List;
using CandidateAssessment.Application.Phones.Update;
using CandidateAssessment.Domain.Entities;

namespace CandidateAssessment.Api.Facades;

/// <summary>
/// Thin Application-layer entry point for the <c>Phones</c> bounded context.
/// See <see cref="PersonsFacade"/> for the rationale — same trade-offs apply.
/// </summary>
public sealed class PhonesFacade
{
    private readonly CreatePhoneHandler _create;
    private readonly UpdatePhoneHandler _update;
    private readonly DeletePhoneHandler _delete;
    private readonly GetPhoneByIdHandler _getById;
    private readonly ListPhonesHandler _list;

    public PhonesFacade(
        CreatePhoneHandler create,
        UpdatePhoneHandler update,
        DeletePhoneHandler delete,
        GetPhoneByIdHandler getById,
        ListPhonesHandler list)
    {
        _create = create;
        _update = update;
        _delete = delete;
        _getById = getById;
        _list = list;
    }

    public Task<Guid> CreateAsync(CreatePhoneCommand command, CancellationToken cancellationToken)
        => _create.HandleAsync(command, cancellationToken);

    public Task UpdateAsync(UpdatePhoneCommand command, CancellationToken cancellationToken)
        => _update.HandleAsync(command, cancellationToken);

    public Task DeleteAsync(DeletePhoneCommand command, CancellationToken cancellationToken)
        => _delete.HandleAsync(command, cancellationToken);

    public Task<Phone> GetByIdAsync(GetPhoneByIdQuery query, CancellationToken cancellationToken)
        => _getById.HandleAsync(query, cancellationToken);

    public Task<IReadOnlyList<Phone>> ListAsync(ListPhonesQuery query, CancellationToken cancellationToken)
        => _list.HandleAsync(query, cancellationToken);
}