using System.Collections.Concurrent;
using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Domain.Entities;

namespace CandidateAssessment.UnitTests.Fakes;

internal sealed class FakePhoneRepository : IPhoneRepository
{
    private readonly ConcurrentDictionary<Guid, Phone> _byId = new();

    public Task<Phone?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _byId.TryGetValue(id, out var phone);
        return Task.FromResult<Phone?>(phone);
    }

    public Task<bool> ExistsForPersonAsync(
        Guid personId,
        string number,
        CancellationToken cancellationToken = default)
    {
        var result = _byId.Values.Any(p => p.PersonId == personId && p.Number == number);
        return Task.FromResult(result);
    }

    public Task AddAsync(Phone phone, CancellationToken cancellationToken = default)
    {
        _byId[phone.Id] = phone;
        return Task.CompletedTask;
    }
}
