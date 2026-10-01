using CandidateAssessment.Domain.Enums;

namespace CandidateAssessment.Application.Phones.Update;

public sealed record UpdatePhoneCommand(
    Guid PersonId,
    Guid PhoneId,
    PhoneType Type,
    string Number);
