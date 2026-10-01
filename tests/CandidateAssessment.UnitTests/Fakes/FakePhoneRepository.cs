using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Domain.Entities;

namespace CandidateAssessment.UnitTests.Fakes;

/// <summary>
/// Fake phone repository that mirrors EF Core behaviour: phones added through
/// <c>Person.AddPhone</c> are visible via the Person aggregate's navigation
/// property. This fake queries the companion <see cref="FakePersonRepository"/>
/// to resolve phones, just as EF Core would resolve them through the DbContext.
/// </summary>
internal sealed class FakePhoneRepository : IPhoneRepository
{
    private readonly FakePersonRepository _personRepository;

    public FakePhoneRepository(FakePersonRepository personRepository)
    {
        _personRepository = personRepository;
    }

    /// <summary>
    /// Compatibility constructor for tests that don't need cross-aggregate phone
    /// lookups. Phones will only be findable if <see cref="AddAsync"/> is called
    /// explicitly.
    /// </summary>
    public FakePhoneRepository()
        : this(new FakePersonRepository())
    {
    }

    public Task<Phone?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var phone = _personRepository.AllPhones().FirstOrDefault(p => p.Id == id);
        return Task.FromResult(phone);
    }

    public Task<bool> ExistsForPersonAsync(
        Guid personId,
        string number,
        CancellationToken cancellationToken = default)
    {
        var result = _personRepository.AllPhones()
            .Any(p => p.PersonId == personId && p.Number == number);
        return Task.FromResult(result);
    }

    public Task AddAsync(Phone phone, CancellationToken cancellationToken = default)
    {
        // In the current flow, phones are tracked through Person.AddPhone() and
        // the Person aggregate's navigation property. This method is kept for
        // interface compliance but is a no-op when phones come through the aggregate.
        return Task.CompletedTask;
    }
}
