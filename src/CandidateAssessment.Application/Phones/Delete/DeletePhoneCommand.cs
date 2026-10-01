namespace CandidateAssessment.Application.Phones.Delete;

public sealed record DeletePhoneCommand(
    Guid PersonId,
    Guid PhoneId);
