using CandidateAssessment.Domain.ValueObjects;

namespace CandidateAssessment.Api.Contracts;

/// <summary>
/// Auxiliar usado nas fronteiras dos controllers para formatar um CPF sem duplicar lógica.
/// </summary>
internal static class CpfFormatter
{
    public static string Format(string cpf)
    {
        if (Cpf.TryCreate(cpf, out Cpf? parsed))
        {
            return parsed.Format();
        }

        // Usa o valor de entrada como alternativa caso não seja possível interpretá-lo como CPF.
        // Isso não deve ocorrer com entradas validadas, mas evita propagar exceções.
        return cpf;
    }
}
