using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Domain.Entities;

namespace CandidateAssessment.Application.Persons.GetById;

public sealed class GetPersonByIdHandler
{
    private readonly IPersonRepository _personRepository;

    public GetPersonByIdHandler(IPersonRepository personRepository)
    {
        _personRepository = personRepository;
    }

    public async Task<Person> HandleAsync(
        GetPersonByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var person = await _personRepository.GetByIdAsync(query.Id, cancellationToken);
        if (person is null)
        {
            throw new ApplicationValidationException(
                "PersonNotFound",
                $"Person with id '{query.Id}' was not found.");
        }

        return person;
    }
}
