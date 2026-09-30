namespace CandidateAssessment.Api.Contracts.Persons;

/// <summary>
/// Default person representation returned by the API.
/// CPF is returned masked by default to avoid leaking sensitive identifiers.
/// </summary>
public sealed class PersonResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = default!;

    public string Cpf { get; init; } = default!;

    public DateOnly BirthDate { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }

    public DateTime? DeletedAtUtc { get; init; }

    public DateTime? RestoredAtUtc { get; init; }
}
