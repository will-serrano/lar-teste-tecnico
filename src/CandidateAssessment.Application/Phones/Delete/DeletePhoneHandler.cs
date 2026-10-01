using CandidateAssessment.Application.Abstractions.Caching;
using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Abstractions.Time;
using CandidateAssessment.Application.Exceptions;

namespace CandidateAssessment.Application.Phones.Delete;

public sealed class DeletePhoneHandler
{
    private readonly IPersonRepository _personRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPersonCache _personCache;

    public DeletePhoneHandler(
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
        DeletePhoneCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var person = await _personRepository.GetByIdAsync(command.PersonId, cancellationToken);
        if (person is null)
        {
            throw new ApplicationValidationException(
                "PersonNotFound",
                $"Person with id '{command.PersonId}' was not found.");
        }

        if (person.Phones.All(p => p.Id != command.PhoneId))
        {
            throw new ApplicationValidationException(
                "PhoneNotFound",
                $"Phone with id '{command.PhoneId}' was not found for this person.");
        }

        person.RemovePhone(command.PhoneId, _dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _personCache.InvalidateAsync(command.PersonId, cancellationToken);
    }
}
