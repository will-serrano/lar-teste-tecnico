using CandidateAssessment.Domain.Enums;
using CandidateAssessment.Domain.Exceptions;
using CandidateAssessment.Domain.ValueObjects;

namespace CandidateAssessment.Domain.Entities;

/// <summary>
/// Phone attached to a Person. Subjected to hard delete.
/// </summary>
public sealed class Phone
{
    public const int MaxActivePhonesPerPerson = 5;

    public Guid Id { get; private set; }

    public Guid PersonId { get; private set; }

    public PhoneType Type { get; private set; }

    public string Number { get; private set; } = default!;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    // EF Core parameterless constructor.
    private Phone()
    {
    }

    private Phone(Guid id, Guid personId, PhoneType type, string number, DateTime createdAtUtc)
    {
        Id = id;
        PersonId = personId;
        Type = type;
        Number = number;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public static Phone Create(Guid personId, PhoneType type, PhoneNumber number, DateTime nowUtc)
    {
        if (personId == Guid.Empty)
        {
            throw new DomainException("PersonId is required.");
        }

        return new Phone(Guid.NewGuid(), personId, type, number.Value, nowUtc);
    }

    public void Update(PhoneType type, PhoneNumber number, DateTime nowUtc)
    {
        Type = type;
        Number = number.Value;
        UpdatedAtUtc = nowUtc;
    }
}
