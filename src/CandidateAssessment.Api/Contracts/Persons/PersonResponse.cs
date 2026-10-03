namespace CandidateAssessment.Api.Contracts.Persons;

/// <summary>
/// Representação padrão de pessoa retornada pela API.
/// Por padrão, o CPF é mascarado para evitar a exposição de identificadores sensíveis.
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
