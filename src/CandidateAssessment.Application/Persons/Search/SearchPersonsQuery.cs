namespace CandidateAssessment.Application.Persons.Search;

/// <summary>
/// Consulta para pesquisar pessoas ativas.
/// Os filtros são opcionais e combinados com AND.
/// O handler aplica os valores padrão de paginação.
/// </summary>
public sealed record SearchPersonsQuery(
    string? Name,
    string? Cpf,
    int? Page,
    int? PageSize);
