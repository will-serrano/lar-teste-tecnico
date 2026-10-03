using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Diagnostics;
using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Domain.Entities;

namespace CandidateAssessment.Application.Phones.List;

public sealed class ListPhonesHandler
{
    private readonly IPersonRepository _personRepository;

    public ListPhonesHandler(IPersonRepository personRepository)
    {
        _personRepository = personRepository;
    }

    public async Task<IReadOnlyList<Phone>> HandleAsync(
        ListPhonesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        using var operation = ApplicationDiagnostics.StartOperation("phones.list");

        var person = await _personRepository.GetByIdAsync(query.PersonId, cancellationToken) ?? throw new ApplicationValidationException(
                "PersonNotFound",
                $"Person with id '{query.PersonId}' was not found.");
        operation.Complete();
        return person.Phones
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Number)
            .ToArray();
    }
}
