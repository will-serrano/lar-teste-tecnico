using CandidateAssessment.Domain.Enums;

namespace CandidateAssessment.Api.Contracts.Phones;

/// <summary>
/// Request body for creating a phone. Number accepts formatted or digits-only input.
/// </summary>
public sealed class CreatePhoneRequest
{
    public PhoneType Type { get; init; }

    public string Number { get; init; } = default!;
}
