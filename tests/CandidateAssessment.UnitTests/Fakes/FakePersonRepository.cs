using System.Collections.Concurrent;
using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Domain.Entities;

namespace CandidateAssessment.UnitTests.Fakes;

internal sealed class FakePersonRepository : IPersonRepository
{
    private readonly ConcurrentDictionary<Guid, Person> _byId = new();

    public int SaveChangesCount { get; private set; }

    public Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _byId.TryGetValue(id, out var person);
        return Task.FromResult<Person?>(person is { IsActive: true } ? person : null);
    }

    public Task<Person?> GetByIdIncludingDeletedAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _byId.TryGetValue(id, out var person);
        return Task.FromResult(person);
    }

    public Task<bool> CpfExistsAsync(string cpf, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_byId.Values.Any(p => p.Cpf == cpf));
    }

    public Task<bool> CpfExistsForOtherPersonAsync(
        string cpf,
        Guid excludingPersonId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_byId.Values.Any(p => p.Cpf == cpf && p.Id != excludingPersonId));
    }

    public Task<IReadOnlyList<Person>> SearchAsync(
        string? nameContains,
        string? cpfEquals,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<Person> query = _byId.Values.Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(nameContains))
        {
            var term = nameContains.Trim();
            query = query.Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(cpfEquals))
        {
            var digits = ExtractDigits(cpfEquals);
            if (digits.Length == 11)
            {
                query = query.Where(p => p.Cpf == digits);
            }
            else
            {
                query = Array.Empty<Person>();
            }
        }

        var ordered = query
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Id)
            .ToList();

        var skip = Math.Max(0, (page - 1) * pageSize);
        return Task.FromResult<IReadOnlyList<Person>>(ordered.Skip(skip).Take(pageSize).ToList());
    }

    public Task<int> CountSearchAsync(
        string? nameContains,
        string? cpfEquals,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<Person> query = _byId.Values.Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(nameContains))
        {
            var term = nameContains.Trim();
            query = query.Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(cpfEquals))
        {
            var digits = ExtractDigits(cpfEquals);
            if (digits.Length == 11)
            {
                query = query.Where(p => p.Cpf == digits);
            }
            else
            {
                query = Array.Empty<Person>();
            }
        }

        return Task.FromResult(query.Count());
    }

    public Task<IReadOnlyList<Person>> GetDeletedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var deleted = _byId.Values
            .Where(p => !p.IsActive)
            .OrderByDescending(p => p.DeletedAtUtc)
            .ThenBy(p => p.Id)
            .ToList();

        var skip = Math.Max(0, (page - 1) * pageSize);
        return Task.FromResult<IReadOnlyList<Person>>(deleted.Skip(skip).Take(pageSize).ToList());
    }

    public Task<int> CountDeletedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.Values.Count(p => !p.IsActive));

    public Task AddAsync(Person person, CancellationToken cancellationToken = default)
    {
        _byId[person.Id] = person;
        return Task.CompletedTask;
    }

    public void RecordSaveChanges() => SaveChangesCount++;

    /// <summary>
    /// Returns all phones across all stored persons. Used by
    /// <see cref="FakePhoneRepository"/> to mirror EF Core's ability to query
    /// phones through the DbContext even when they were added via the Person
    /// aggregate's navigation property.
    /// </summary>
    public IEnumerable<Phone> AllPhones()
        => _byId.Values.SelectMany(p => p.Phones);

    private static string ExtractDigits(string input)
    {
        Span<char> buffer = stackalloc char[11];
        var index = 0;

        foreach (var ch in input)
        {
            if (char.IsDigit(ch))
            {
                if (index >= buffer.Length)
                {
                    return string.Empty;
                }

                buffer[index++] = ch;
            }
        }

        return new string(buffer[..index]);
    }
}
