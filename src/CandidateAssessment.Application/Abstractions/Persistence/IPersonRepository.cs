using CandidateAssessment.Domain.Entities;

namespace CandidateAssessment.Application.Abstractions.Persistence;

/// <summary>
/// Repository contract for the Person aggregate.
/// Methods intentionally cover only the use cases required by the Application layer —
/// no generic CRUD surface.
/// </summary>
public interface IPersonRepository
{
    /// <summary>
    /// Returns an active person (i.e. not soft-deleted) by id, including phones.
    /// Returns null when no active person matches.
    /// </summary>
    Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a person by id regardless of soft-delete state, including phones.
    /// Used by Restore and GetDeleted flows.
    /// </summary>
    Task<Person?> GetByIdIncludingDeletedAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when the CPF is already registered for any person
    /// (active or soft-deleted — CPFs remain reserved).
    /// </summary>
    Task<bool> CpfExistsAsync(string cpf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when another person (excluding the supplied id) already owns the CPF.
    /// </summary>
    Task<bool> CpfExistsForOtherPersonAsync(
        string cpf,
        Guid excludingPersonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches active persons with optional filters and pagination, ordered by
    /// Name asc, Id asc for stable pagination.
    /// </summary>
    Task<IReadOnlyList<Person>> SearchAsync(
        string? nameContains,
        string? cpfEquals,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the total number of active persons matching the optional filters.
    /// </summary>
    Task<int> CountSearchAsync(
        string? nameContains,
        string? cpfEquals,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns soft-deleted persons ordered by DeletedAtUtc desc with pagination.
    /// </summary>
    Task<IReadOnlyList<Person>> GetDeletedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the total number of soft-deleted persons.
    /// </summary>
    Task<int> CountDeletedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new person. Caller is responsible for SaveChanges via IUnitOfWork.
    /// </summary>
    Task AddAsync(Person person, CancellationToken cancellationToken = default);
}
