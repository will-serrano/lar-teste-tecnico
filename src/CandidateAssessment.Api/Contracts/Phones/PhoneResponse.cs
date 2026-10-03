using CandidateAssessment.Domain.Enums;

namespace CandidateAssessment.Api.Contracts.Phones;

/// <summary>
/// Representação de telefone retornada pela API.
/// </summary>
public sealed class PhoneResponse
{
    public Guid Id { get; init; }

    public Guid PersonId { get; init; }

    public PhoneType Type { get; init; }

    public string Number { get; init; } = default!;

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
