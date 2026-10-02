using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Domain.Entities;

namespace CandidateAssessment.Application.Phones.GetById;

public sealed class GetPhoneByIdHandler
{
    private readonly IPersonRepository _personRepository;
    private readonly IPhoneRepository _phoneRepository;

    public GetPhoneByIdHandler(
        IPersonRepository personRepository,
        IPhoneRepository phoneRepository)
    {
        _personRepository = personRepository;
        _phoneRepository = phoneRepository;
    }

    public async Task<Phone> HandleAsync(
        GetPhoneByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        _ = await _personRepository.GetByIdAsync(query.PersonId, cancellationToken) ?? throw new ApplicationValidationException(
                "PersonNotFound",
                $"Person with id '{query.PersonId}' was not found.");
        var phone = await _phoneRepository.GetByIdAsync(query.PhoneId, cancellationToken);
        if (phone is null || phone.PersonId != query.PersonId)
        {
            throw new ApplicationValidationException(
                "PhoneNotFound",
                $"Phone with id '{query.PhoneId}' was not found for this person.");
        }

        return phone;
    }
}
