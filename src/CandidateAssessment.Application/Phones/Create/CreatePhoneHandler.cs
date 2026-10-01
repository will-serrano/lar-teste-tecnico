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

    public CreatePhoneHandler(
        IPersonRepository personRepository,
        IPhoneRepository phoneRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _personRepository = personRepository;
        _phoneRepository = phoneRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Guid> HandleAsync(
        CreatePhoneCommand command,
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

        await _phoneRepository.AddAsync(phone, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return phone.Id;
    }
}
