using CandidateAssessment.Application.Abstractions.Caching;
using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Abstractions.Time;
using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Domain.ValueObjects;

namespace CandidateAssessment.Application.Phones.Create;

public sealed class CreatePhoneHandler
{
    private readonly IPersonRepository _personRepository;
    private readonly IPhoneRepository _phoneRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPersonCache _personCache;

    public CreatePhoneHandler(
        IPersonRepository personRepository,
        IPhoneRepository phoneRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IPersonCache personCache)
    {
        _personRepository = personRepository;
        _phoneRepository = phoneRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _personCache = personCache;
    }

    public async Task<Guid> HandleAsync(
        CreatePhoneCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var person = await _personRepository.GetByIdAsync(command.PersonId, cancellationToken) ?? throw new ApplicationValidationException(
                "PersonNotFound",
                $"Person with id '{command.PersonId}' was not found.");
        var phoneNumber = PhoneNumber.Create(command.Number, command.Type);

        if (await _phoneRepository.ExistsForPersonAsync(
                command.PersonId,
                phoneNumber.Value,
                cancellationToken))
        {
            throw new ApplicationValidationException(
                "PhoneAlreadyExists",
                "This person already has a phone with the same number.");
        }

        var nowUtc = _dateTimeProvider.UtcNow;
        var phone = person.AddPhone(command.Type, phoneNumber, nowUtc);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Phone mutations alter the cached person snapshot.
        await _personCache.InvalidateAsync(command.PersonId, cancellationToken);

        return phone.Id;
    }
}
