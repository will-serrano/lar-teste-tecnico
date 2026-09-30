using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Abstractions.Time;
using CandidateAssessment.Application.Exceptions;

namespace CandidateAssessment.Application.Persons.Update;

public sealed class UpdatePersonHandler
{
    private readonly IPersonRepository _personRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdatePersonHandler(
        IPersonRepository personRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _personRepository = personRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task HandleAsync(
        UpdatePersonCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var person = await _personRepository.GetByIdAsync(command.Id, cancellationToken);
        if (person is null)
        {
            throw new ApplicationValidationException(
                "PersonNotFound",
                $"Person with id '{command.Id}' was not found.");
        }

        var nowUtc = _dateTimeProvider.UtcNow;
        person.Update(command.Name, command.BirthDate, nowUtc);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
