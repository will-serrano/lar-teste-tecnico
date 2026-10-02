using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Domain.Entities;
using CandidateAssessment.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CandidateAssessment.Infrastructure.Persistence.Repositories;

public class EfPersonRepository : IPersonRepository
{
    private readonly ApplicationDbContext _dbContext;

    public EfPersonRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Persons
            .Include(p => p.Phones)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Person?> GetByIdIncludingDeletedAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => _dbContext.Persons
            .IgnoreQueryFilters()
            .Include(p => p.Phones)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> CpfExistsAsync(string cpf, CancellationToken cancellationToken = default)
        => _dbContext.Persons
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Cpf == cpf, cancellationToken);

    public Task<bool> CpfExistsForOtherPersonAsync(
        string cpf,
        Guid excludingPersonId,
        CancellationToken cancellationToken = default)
        => _dbContext.Persons
            .IgnoreQueryFilters()
            .AnyAsync(
                p => p.Cpf == cpf && p.Id != excludingPersonId,
                cancellationToken);

    public async Task<IReadOnlyList<Person>> SearchAsync(
        string? nameContains,
        string? cpfEquals,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildSearchQuery(nameContains, cpfEquals);

        return await query
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountSearchAsync(
        string? nameContains,
        string? cpfEquals,
        CancellationToken cancellationToken = default)
        => BuildSearchQuery(nameContains, cpfEquals).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Person>> GetDeletedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Persons
            .IgnoreQueryFilters()
            .Where(p => !p.IsActive)
            .OrderByDescending(p => p.DeletedAtUtc)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountDeletedAsync(CancellationToken cancellationToken = default)
        => _dbContext.Persons
            .IgnoreQueryFilters()
            .CountAsync(p => !p.IsActive, cancellationToken);

    public async Task AddAsync(Person person, CancellationToken cancellationToken = default)
        => await _dbContext.Persons.AddAsync(person, cancellationToken);

    private IQueryable<Person> BuildSearchQuery(string? nameContains, string? cpfEquals)
    {
        var query = _dbContext.Persons.AsQueryable();

        if (!string.IsNullOrWhiteSpace(nameContains))
        {
            var trimmed = nameContains.Trim();
            query = query.Where(p => EF.Functions.Like(p.Name, $"%{trimmed}%"));
        }

        if (!string.IsNullOrWhiteSpace(cpfEquals))
        {
            var digits = Cpf.Normalize(cpfEquals);
            if (digits.Length == Cpf.Length)
            {
                query = query.Where(p => p.Cpf == digits);
            }
            else
            {
                // Filter that can never match — keeps semantics predictable when caller
                // passes malformed CPF at the repository layer.
                query = query.Where(p => false);
            }
        }

        return query;
    }
}
