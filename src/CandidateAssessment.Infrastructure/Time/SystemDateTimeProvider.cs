using CandidateAssessment.Application.Abstractions.Time;

namespace CandidateAssessment.Infrastructure.Time;

public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
