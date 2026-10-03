namespace CandidateAssessment.Application.Persons.Create;

/// <summary>
/// Comando para criar uma pessoa.
/// O CPF é aceito como entrada bruta (formatado ou apenas com dígitos) e validado pelo
/// objeto de valor Domain; as regras estruturais (tamanho e expressão regular) são
/// validadas pelo validador.
/// </summary>
public sealed record CreatePersonCommand(
    string Name,
    string Cpf,
    DateOnly BirthDate);
