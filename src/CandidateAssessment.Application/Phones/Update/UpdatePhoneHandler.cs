using CandidateAssessment.Application.Abstractions.Caching;
using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Abstractions.Time;
using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Domain.ValueObjects;

namespace CandidateAssessment.Application.Phones.Update;

public sealed class UpdatePhoneHandler
{
    private readonly IPersonRepository _personRepository;
    private readonly IPhoneRepository _phoneRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPersonCache _personCache;

    public UpdatePhoneHandler(
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

    public async Task HandleAsync(
        UpdatePhoneCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var person = await _personRepository.GetByIdAsync(command.PersonId, cancellationToken) ?? throw new ApplicationValidationException(
                "PersonNotFound",
                $"Person with id '{command.PersonId}' was not found.");
        var phone = await _phoneRepository.GetByIdAsync(command.PhoneId, cancellationToken);
        if (phone is null || phone.PersonId != command.PersonId)
        {
            throw new ApplicationValidationException(
                "PhoneNotFound",
                $"Phone with id '{command.PhoneId}' was not found for this person.");
        }

        var phoneNumber = PhoneNumber.Create(command.Number, command.Type);

        // Allow keeping the same number/type without false-positive duplicate detection.
        var sameSlot = phone.Number == phoneNumber.Value && phone.Type == command.Type;
        if (!sameSlot
            && await _phoneRepository.ExistsForPersonAsync(
                command.PersonId,
                phoneNumber.Value,
                cancellationToken))
        {
            throw new ApplicationValidationException(
                "PhoneAlreadyExists",
                "This person already has a phone with the same number.");
        }

        var nowUtc = _dateTimeProvider.UtcNow;
        person.UpdatePhone(command.PhoneId, command.Type, phoneNumber, nowUtc);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _personCache.InvalidateAsync(command.PersonId, cancellationToken);
    }
}
