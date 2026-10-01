using CandidateAssessment.Application.Abstractions.Persistence;
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

        var person = await _personRepository.GetByIdAsync(query.PersonId, cancellationToken);
        if (person is null)
        {
            throw new ApplicationValidationException(
                "PersonNotFound",
                $"Person with id '{query.PersonId}' was not found.");
        }

        return person.Phones
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Number)
            .ToList();
    }
}
