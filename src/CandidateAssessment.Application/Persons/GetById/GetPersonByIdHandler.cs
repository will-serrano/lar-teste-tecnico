using CandidateAssessment.Application.Abstractions.Caching;
using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Domain.Entities;
using CandidateAssessment.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CandidateAssessment.Application.Persons.GetById;

public sealed class GetPersonByIdHandler
{
    private readonly IPersonRepository _personRepository;
    private readonly ICacheService _cache;
    private readonly CacheOptions _cacheOptions;
    private readonly ILogger<GetPersonByIdHandler> _logger;

    public GetPersonByIdHandler(
        IPersonRepository personRepository,
        ICacheService cache,
        IOptions<CacheOptions> cacheOptions,
        ILogger<GetPersonByIdHandler> logger)
    {
        _personRepository = personRepository;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
        _logger = logger;
    }

    public async Task<CachedPerson> HandleAsync(
        GetPersonByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var cacheKey = PersonCacheKeys.ForDetail(query.Id);

        var cached = await _cache.GetAsync<CachedPerson>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Person cache HIT for {PersonId}", query.Id);
            return cached;
        }

        _logger.LogDebug("Person cache MISS for {PersonId}", query.Id);

        var person = await _personRepository.GetByIdAsync(query.Id, cancellationToken) ?? throw new ApplicationValidationException(
                "PersonNotFound",
                $"Person with id '{query.Id}' was not found.");
        var entry = CachedPerson.From(person);
        await _cache.SetAsync(
            cacheKey,
            entry,
            _cacheOptions.PersonDetailAbsoluteExpiration,
            cancellationToken);

        return entry;
    }
}

/// <summary>
/// Cache-friendly snapshot of a <see cref="Person"/>: only the read-side fields
/// needed to satisfy <c>GET /api/v1/persons/{id}</c>. Keeps EF Core's lazy-loaded
/// collections out of cached entries and lets the response be projected without
/// re-attaching to a DbContext.
/// </summary>
public sealed class CachedPerson
{
    public Guid Id { get; init; }

    public string Name { get; init; } = default!;

    public string Cpf { get; init; } = default!;

    public DateOnly BirthDate { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }

    public DateTime? DeletedAtUtc { get; init; }

    public DateTime? RestoredAtUtc { get; init; }

    public IReadOnlyList<CachedPhone> Phones { get; init; } = [];

    public static CachedPerson From(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        return new CachedPerson
        {
            Id = person.Id,
            Name = person.Name,
            Cpf = person.Cpf,
            BirthDate = person.BirthDate,
            IsActive = person.IsActive,
            CreatedAtUtc = person.CreatedAtUtc,
            UpdatedAtUtc = person.UpdatedAtUtc,
            DeletedAtUtc = person.DeletedAtUtc,
            RestoredAtUtc = person.RestoredAtUtc,
            Phones = [.. person.Phones.Select(CachedPhone.From)],
        };
    }
}

public sealed class CachedPhone
{
    public Guid Id { get; init; }

    public Guid PersonId { get; init; }

    public PhoneType Type { get; init; }

    public string Number { get; init; } = default!;

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }

    public static CachedPhone From(Phone phone)
    {
        ArgumentNullException.ThrowIfNull(phone);

        return new CachedPhone
        {
            Id = phone.Id,
            PersonId = phone.PersonId,
            Type = phone.Type,
            Number = phone.Number,
            CreatedAtUtc = phone.CreatedAtUtc,
            UpdatedAtUtc = phone.UpdatedAtUtc,
        };
    }
}
