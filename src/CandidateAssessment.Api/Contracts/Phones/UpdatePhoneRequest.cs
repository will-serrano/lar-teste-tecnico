using CandidateAssessment.Domain.Enums;

namespace CandidateAssessment.Api.Contracts.Phones;

public sealed class UpdatePhoneRequest
{
    public PhoneType Type { get; init; }

    public string Number { get; init; } = default!;
}
