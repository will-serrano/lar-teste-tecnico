namespace CandidateAssessment.Api.Contracts.Persons;

public sealed class UpdatePersonRequest
{
    public string Name { get; init; } = default!;

    public DateOnly BirthDate { get; init; }
}
