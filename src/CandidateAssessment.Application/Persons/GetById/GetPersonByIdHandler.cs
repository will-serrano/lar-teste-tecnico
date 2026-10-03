using CandidateAssessment.Application.Abstractions.Caching;
using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Diagnostics;
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
        using var operation = ApplicationDiagnostics.StartOperation("persons.get");

        var cacheKey = PersonCacheKeys.ForDetail(query.Id);

        var cached = await _cache.GetAsync<CachedPerson>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Person cache HIT");
            operation.Complete();
            return cached;
        }

        _logger.LogDebug("Person cache MISS");

        var person = await _personRepository.GetByIdAsync(query.Id, cancellationToken) ?? throw new ApplicationValidationException(
                "PersonNotFound",
                $"Person with id '{query.Id}' was not found.");
        var entry = CachedPerson.From(person);
        await _cache.SetAsync(
            cacheKey,
            entry,
            _cacheOptions.PersonDetailAbsoluteExpiration,
            cancellationToken);

        operation.Complete();
        return entry;
    }
}

/// <summary>
/// Projeção de <see cref="Person"/> adequada para cache: contém apenas os campos de leitura
/// necessários para atender a <c>GET /api/v1/persons/{id}</c>. Evita incluir coleções
/// carregadas sob demanda pelo EF Core nas entradas em cache e permite projetar a resposta
/// sem reassociá-la a um DbContext.
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
