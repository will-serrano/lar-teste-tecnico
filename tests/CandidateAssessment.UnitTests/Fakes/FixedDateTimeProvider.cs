using CandidateAssessment.Application.Abstractions.Time;

namespace CandidateAssessment.UnitTests.Fakes;

internal sealed class FixedDateTimeProvider : IDateTimeProvider
{
    public FixedDateTimeProvider(DateTime utcNow)
    {
        UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }

    public DateTime UtcNow { get; }
}
