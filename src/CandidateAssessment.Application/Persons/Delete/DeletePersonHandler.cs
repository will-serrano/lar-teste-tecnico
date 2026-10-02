using CandidateAssessment.Application.Abstractions.Caching;
using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Abstractions.Time;
using CandidateAssessment.Application.Exceptions;

namespace CandidateAssessment.Application.Persons.Delete;

public sealed class DeletePersonHandler
{
    private readonly IPersonRepository _personRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPersonCache _personCache;

    public DeletePersonHandler(
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
        DeletePersonCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var person = await _personRepository.GetByIdAsync(command.Id, cancellationToken) ?? throw new ApplicationValidationException(
                "PersonNotFound",
                $"Person with id '{command.Id}' was not found.");
        var nowUtc = _dateTimeProvider.UtcNow;
        person.Delete(nowUtc);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _personCache.InvalidateAsync(command.Id, cancellationToken);
    }
}
