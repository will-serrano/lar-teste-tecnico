using CandidateAssessment.Domain.Entities;

namespace CandidateAssessment.Application.Abstractions.Persistence;

public interface IPersonRepository
{
    Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> CpfExistsAsync(string cpf, CancellationToken cancellationToken = default);

    Task AddAsync(Person person, CancellationToken cancellationToken = default);
}
