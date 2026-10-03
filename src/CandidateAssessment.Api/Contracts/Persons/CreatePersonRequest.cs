namespace CandidateAssessment.Api.Contracts.Persons;

/// <summary>
/// Corpo da requisição para criar uma pessoa. O CPF aceita entrada formatada ou somente com dígitos.
/// </summary>
public sealed class CreatePersonRequest
{
    public string Name { get; init; } = default!;

    public string Cpf { get; init; } = default!;

    public DateOnly BirthDate { get; init; }
}
