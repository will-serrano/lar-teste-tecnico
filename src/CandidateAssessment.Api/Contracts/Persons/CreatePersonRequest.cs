namespace CandidateAssessment.Api.Contracts.Persons;

/// <summary>
/// Request body for creating a person. CPF accepts formatted or digits-only input.
/// </summary>
public sealed class CreatePersonRequest
{
    public string Name { get; init; } = default!;

    public string Cpf { get; init; } = default!;

    public DateOnly BirthDate { get; init; }
}
