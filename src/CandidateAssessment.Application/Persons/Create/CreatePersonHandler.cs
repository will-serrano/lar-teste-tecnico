using CandidateAssessment.Application.Abstractions.Caching;
using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Abstractions.Time;
using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Domain.Entities;
using CandidateAssessment.Domain.ValueObjects;

namespace CandidateAssessment.Application.Persons.Create;

public sealed class CreatePersonHandler
{
    private readonly IPersonRepository _personRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPersonCache _personCache;

    public CreatePersonHandler(
        IPersonRepository personRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IPersonCache personCache)
    {
        _personRepository = personRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _personCache = personCache;
    }

    public async Task<Guid> HandleAsync(
        CreatePersonCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var cpf = Cpf.Create(command.Cpf);

        if (await _personRepository.CpfExistsAsync(cpf.Value, cancellationToken))
        {
            throw new ApplicationValidationException(
                "CpfAlreadyExists",
                "A person with this CPF already exists.");
        }

        var nowUtc = _dateTimeProvider.UtcNow;
        var person = Person.Create(command.Name, cpf, command.BirthDate, nowUtc);

        await _personRepository.AddAsync(person, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Defensive: a fresh id is never in cache, but invalidate to keep a single
        // invalidation policy across all mutation paths.
        await _personCache.InvalidateAsync(person.Id, cancellationToken);

        return person.Id;
    }
}
