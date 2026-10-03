namespace CandidateAssessment.Application.Abstractions.Time;

/// <summary>
/// Abstração para obter a data e hora UTC atuais.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
