using CandidateAssessment.Domain.ValueObjects;

namespace CandidateAssessment.Api.Contracts;

/// <summary>
/// Helper used at controller boundaries to format a CPF without duplicating logic.
/// </summary>
internal static class CpfFormatter
{
    public static string Format(string cpf)
    {
        if (Cpf.TryCreate(cpf, out Cpf? parsed))
        {
            return parsed.Format();
        }

        // Fall back to input if the value cannot be parsed as CPF.
        // This should not happen for validated inputs but avoids leaking exceptions.
        return cpf;
    }
}
