namespace CandidateAssessment.Application.Persons.Update;

public sealed record UpdatePersonCommand(
    Guid Id,
    string Name,
    DateOnly BirthDate);
