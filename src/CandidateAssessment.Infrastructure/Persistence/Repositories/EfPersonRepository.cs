using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Domain.Entities;
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

    public Task<bool> CpfExistsAsync(string cpf, CancellationToken cancellationToken = default)
        => _dbContext.Persons
            .AnyAsync(p => p.Cpf == cpf, cancellationToken);

    public async Task AddAsync(Person person, CancellationToken cancellationToken = default)
        => await _dbContext.Persons.AddAsync(person, cancellationToken);
}
