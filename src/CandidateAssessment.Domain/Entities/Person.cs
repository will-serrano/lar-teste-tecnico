using CandidateAssessment.Domain.Enums;
using CandidateAssessment.Domain.Exceptions;
using CandidateAssessment.Domain.ValueObjects;

namespace CandidateAssessment.Domain.Entities;

/// <summary>
/// Person aggregate root.
/// Supports soft delete (logical) and restoration.
/// </summary>
public sealed class Person
{
    public const int MaxNameLength = 100;
    public const int MaxActivePhonesPerPerson = Phone.MaxActivePhonesPerPerson;

    public Guid Id { get; private set; }

    public string Name { get; private set; } = default!;

    public string Cpf { get; private set; } = default!;

    public DateOnly BirthDate { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public DateTime? RestoredAtUtc { get; private set; }

    private readonly List<Phone> _phones = new();

    public IReadOnlyCollection<Phone> Phones => _phones.AsReadOnly();

    // EF Core parameterless constructor.
    private Person()
    {
    }

    private Person(Guid id, string name, string cpf, DateOnly birthDate, DateTime createdAtUtc)
    {
        Id = id;
        Name = name;
        Cpf = cpf;
        BirthDate = birthDate;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public static Person Create(string name, Cpf cpf, DateOnly birthDate, DateTime nowUtc)
    {
        var normalizedName = NormalizeName(name);
        ValidateBirthDate(birthDate, nowUtc);

        return new Person(Guid.NewGuid(), normalizedName, cpf.Value, birthDate, nowUtc);
    }

    public void Update(string name, DateOnly birthDate, DateTime nowUtc)
    {
        var normalizedName = NormalizeName(name);
        ValidateBirthDate(birthDate, nowUtc);

        Name = normalizedName;
        BirthDate = birthDate;
        UpdatedAtUtc = nowUtc;
    }

    public Phone AddPhone(PhoneType type, PhoneNumber number, DateTime nowUtc)
    {
        if (!IsActive)
        {
            throw new DomainException("Cannot add a phone to an inactive person.");
        }

        if (_phones.Count >= MaxActivePhonesPerPerson)
        {
            throw new DomainException(
                $"A person cannot have more than {MaxActivePhonesPerPerson} phones.");
        }

        var newPhone = Phone.Create(Id, type, number, nowUtc);
        _phones.Add(newPhone);
        UpdatedAtUtc = nowUtc;
        return newPhone;
    }

    public void RemovePhone(Guid phoneId, DateTime nowUtc)
    {
        var phone = _phones.Find(p => p.Id == phoneId);
        if (phone is null)
        {
            throw new DomainException("Phone not found for this person.");
        }

        _phones.Remove(phone);
        UpdatedAtUtc = nowUtc;
    }

    public void Delete(DateTime nowUtc)
    {
        if (!IsActive)
        {
            throw new DomainException("Person is already inactive.");
        }

        IsActive = false;
        DeletedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Restore(DateTime nowUtc)
    {
        if (IsActive)
        {
            throw new DomainException("Person is already active.");
        }

        IsActive = true;
        RestoredAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Name is required.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length > MaxNameLength)
        {
            throw new DomainException($"Name cannot exceed {MaxNameLength} characters.");
        }

        return trimmed;
    }

    private static void ValidateBirthDate(DateOnly birthDate, DateTime nowUtc)
    {
        var today = DateOnly.FromDateTime(nowUtc);
        if (birthDate > today)
        {
            throw new DomainException("BirthDate cannot be in the future.");
        }
    }
}
