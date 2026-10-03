using CandidateAssessment.Domain.Entities;

namespace CandidateAssessment.Application.Abstractions.Persistence;

/// <summary>
/// Contrato de repositório para o agregado Person.
/// Os métodos abrangem intencionalmente apenas os casos de uso necessários à camada Application —
/// não há uma interface CRUD genérica.
/// </summary>
public interface IPersonRepository
{
    /// <summary>
    /// Retorna uma pessoa ativa (isto é, não excluída logicamente) pelo ID, incluindo os telefones.
    /// Retorna null quando não houver pessoa ativa correspondente.
    /// </summary>
    Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna uma pessoa pelo ID, independentemente do estado de exclusão lógica, incluindo os telefones.
    /// Usado nos fluxos de restauração e consulta de excluídos.
    /// </summary>
    Task<Person?> GetByIdIncludingDeletedAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna true quando o CPF já estiver registrado para qualquer pessoa
    /// (ativa ou excluída logicamente — os CPFs permanecem reservados).
    /// </summary>
    Task<bool> CpfExistsAsync(string cpf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna true quando outra pessoa (exceto a pessoa com o ID informado) já possuir o CPF.
    /// </summary>
    Task<bool> CpfExistsForOtherPersonAsync(
        string cpf,
        Guid excludingPersonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pesquisa pessoas ativas com filtros opcionais e paginação, ordenadas por
    /// Name ascendente e Id ascendente para garantir paginação estável.
    /// </summary>
    Task<IReadOnlyList<Person>> SearchAsync(
        string? nameContains,
        string? cpfEquals,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna o total de pessoas ativas que correspondem aos filtros opcionais.
    /// </summary>
    Task<int> CountSearchAsync(
        string? nameContains,
        string? cpfEquals,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna pessoas excluídas logicamente, ordenadas por DeletedAtUtc decrescente e paginadas.
    /// </summary>
    Task<IReadOnlyList<Person>> GetDeletedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna o total de pessoas excluídas logicamente.
    /// </summary>
    Task<int> CountDeletedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona uma nova pessoa. Quem chama é responsável por executar SaveChanges via IUnitOfWork.
    /// </summary>
    Task AddAsync(Person person, CancellationToken cancellationToken = default);
}
