using CandidateAssessment.Domain.Enums;

namespace CandidateAssessment.Application.Phones.Create;

/// <summary>
/// Command to attach a new phone to an existing person.
/// Number is accepted formatted or digits-only; the value object normalizes it.
/// </summary>
public sealed record CreatePhoneCommand(
    Guid PersonId,
    PhoneType Type,
    string Number);
