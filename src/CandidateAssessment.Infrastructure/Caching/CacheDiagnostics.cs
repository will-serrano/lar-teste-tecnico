using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CandidateAssessment.Infrastructure.Caching;

internal static class CacheDiagnostics
{
    private const string Name = "CandidateAssessment.Cache";
    internal static readonly ActivitySource Source = new(Name);
    private static readonly Meter Meter = new(Name);
    private static readonly Counter<long> Operations = Meter.CreateCounter<long>("candidateassessment.cache.operations");

    internal static void Record(string operation, string outcome)
        => Operations.Add(1, new("operation", operation), new("outcome", outcome));
}
