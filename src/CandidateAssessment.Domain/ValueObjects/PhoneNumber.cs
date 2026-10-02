using CandidateAssessment.Domain.Enums;
using CandidateAssessment.Domain.Exceptions;

namespace CandidateAssessment.Domain.ValueObjects;

/// <summary>
/// Brazilian phone number value object. Stores only digits.
/// Length is 10 (landline) or 11 (mobile / commercial with area code +9).
/// </summary>
public sealed record class PhoneNumber
{
    public string Value { get; }

    private PhoneNumber(string value)
    {
        Value = value;
    }

    public static PhoneNumber Create(string? input, PhoneType type)
    {
        if (!TryCreate(input, type, out PhoneNumber? phone))
        {
            throw new DomainException($"Invalid phone number for type {type}.");
        }

        return phone;
    }

    public static bool TryCreate(string? input, PhoneType type, out PhoneNumber phoneNumber)
    {
        phoneNumber = null!;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var digits = Normalize(input);

        if (!IsLengthAllowed(type, digits.Length))
        {
            return false;
        }

        phoneNumber = new PhoneNumber(digits);
        return true;
    }

    public static string Normalize(string input)
    {
        var digits = new string(input.Where(char.IsDigit).ToArray());
        return digits.Length <= 11 ? digits : string.Empty;
    }

    private static bool IsLengthAllowed(PhoneType type, int length)
    {
        return type switch
        {
            PhoneType.Mobile => length == 11,
            PhoneType.Residential => length == 10,
            PhoneType.Commercial => length is 10 or 11,
            _ => false
        };
    }

    public override string ToString() => Value;
}
