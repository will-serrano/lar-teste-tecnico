namespace CandidateAssessment.Application.Persons.Create;

/// <summary>
/// Command to create a new person.
/// CPF is accepted as raw input (formatted or digits) and validated by the
/// Domain value object; structural rules (length, regex) are validated by
/// the validator.
/// </summary>
public sealed record CreatePersonCommand(
    string Name,
    string Cpf,
    DateOnly BirthDate);
