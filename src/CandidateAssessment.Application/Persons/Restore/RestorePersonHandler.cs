using CandidateAssessment.Application.Abstractions.Caching;
using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Abstractions.Time;
using CandidateAssessment.Application.Exceptions;

namespace CandidateAssessment.Application.Persons.Restore;

public sealed class RestorePersonHandler
{
    private readonly IPersonRepository _personRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPersonCache _personCache;

    public RestorePersonHandler(
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

    public async Task HandleAsync(
        RestorePersonCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var person = await _personRepository.GetByIdIncludingDeletedAsync(
            command.Id,
            cancellationToken);
        if (person is null)
        {
            throw new ApplicationValidationException(
                "PersonNotFound",
                $"Person with id '{command.Id}' was not found.");
        }

        var nowUtc = _dateTimeProvider.UtcNow;
        person.Restore(nowUtc);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _personCache.InvalidateAsync(command.Id, cancellationToken);
    }
}
