using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CandidateAssessment.Infrastructure.Persistence.Repositories;

public class EfPhoneRepository : IPhoneRepository
{
    private readonly ApplicationDbContext _dbContext;

    public EfPhoneRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Phone?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Phones.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> ExistsForPersonAsync(
        Guid personId,
        string number,
        CancellationToken cancellationToken = default)
        => _dbContext.Phones
            .AnyAsync(p => p.PersonId == personId && p.Number == number, cancellationToken);

    public async Task AddAsync(Phone phone, CancellationToken cancellationToken = default)
        => await _dbContext.Phones.AddAsync(phone, cancellationToken);
}
