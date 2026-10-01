namespace CandidateAssessment.Application.Phones.GetById;

public sealed record GetPhoneByIdQuery(
    Guid PersonId,
    Guid PhoneId);
