using CandidateAssessment.Domain.Exceptions;

namespace CandidateAssessment.Domain.ValueObjects;

/// <summary>
/// Brazilian CPF (Cadastro de Pessoas Físicas) value object.
/// Stores only digits in a normalized 11-digit form.
/// </summary>
public sealed record class Cpf
{
    public const int Length = 11;

    public string Value { get; }

    private Cpf(string value)
    {
        Value = value;
    }

    public static Cpf Create(string? input)
    {
        if (!TryCreate(input, out Cpf? cpf))
        {
            throw new DomainException("Invalid CPF.");
        }

        return cpf;
    }

    public static bool TryCreate(string? input, out Cpf cpf)
    {
        cpf = null!;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var digits = Normalize(input);

        if (digits.Length != Length)
        {
            return false;
        }

        if (AllDigitsEqual(digits))
        {
            return false;
        }

        if (!HasValidCheckDigits(digits))
        {
            return false;
        }

        cpf = new Cpf(digits);
        return true;
    }

    public static string Normalize(string input)
    {
        var digits = new string(input.Where(char.IsDigit).ToArray());
        return digits.Length == Length ? digits : string.Empty;
    }

    public string Format()
        => $"{Value[..3]}.{Value[3..6]}.{Value[6..9]}-{Value[9..]}";

    private static bool AllDigitsEqual(string digits)
    {
        var first = digits[0];
        for (var i = 1; i < digits.Length; i++)
        {
            if (digits[i] != first)
            {
                return false;
            }
        }

        return true;
    }

    // Weight arrays are immutable lookup tables for the CPF check-digit
    // algorithm. Hoisting them to static readonly fields avoids allocating a
    // fresh array on every validation (CA1861).
    private static readonly int[] _firstCheckDigitWeights = [10, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] _secondCheckDigitWeights = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];

    private static bool HasValidCheckDigits(string digits)
    {
        var first = ComputeCheckDigit(digits, 9, _firstCheckDigitWeights);
        if (first != digits[9] - '0')
        {
            return false;
        }

        var second = ComputeCheckDigit(digits, 10, _secondCheckDigitWeights);
        return second == digits[10] - '0';
    }

    private static int ComputeCheckDigit(string digits, int length, int[] weights)
    {
        var sum = 0;
        for (var i = 0; i < length; i++)
        {
            sum += (digits[i] - '0') * weights[i];
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    public override string ToString() => Value;
}
