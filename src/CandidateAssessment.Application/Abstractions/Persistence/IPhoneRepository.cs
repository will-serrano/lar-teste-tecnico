using CandidateAssessment.Domain.Entities;

namespace CandidateAssessment.Application.Abstractions.Persistence;

public interface IPhoneRepository
{
    Task<Phone?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsForPersonAsync(
        Guid personId,
        string number,
        CancellationToken cancellationToken = default);

    Task AddAsync(Phone phone, CancellationToken cancellationToken = default);
}
