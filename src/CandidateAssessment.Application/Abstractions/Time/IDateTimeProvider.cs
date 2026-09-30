namespace CandidateAssessment.Application.Abstractions.Time;

/// <summary>
/// Abstraction for retrieving the current UTC date and time.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
