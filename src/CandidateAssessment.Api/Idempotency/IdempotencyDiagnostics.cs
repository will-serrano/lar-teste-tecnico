using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Routing;

namespace CandidateAssessment.Api.Idempotency;

internal static class IdempotencyDiagnostics
{
    internal const string Name = "CandidateAssessment.Idempotency";
    internal static readonly ActivitySource Source = new(Name);
    private static readonly Meter Meter = new(Name);
    private static readonly Counter<long> Operations = Meter.CreateCounter<long>("candidateassessment.idempotency.operations");
    private static readonly Counter<long> Cleanup = Meter.CreateCounter<long>("candidateassessment.idempotency.cleanup");
    private static readonly Histogram<double> Wait = Meter.CreateHistogram<double>("candidateassessment.idempotency.lock_wait", "s");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("candidateassessment.idempotency.duration", "s");

    internal static string Operation(HttpContext context)
        => $"{context.Request.Method} {(context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched"}";

    internal static void Record(HttpContext context, string outcome)
    {
        Operations.Add(1, new("operation", Operation(context)), new("outcome", outcome));
        Activity.Current?.SetTag("idempotency.outcome", outcome);
    }

    internal static void RecordWait(HttpContext context, TimeSpan elapsed)
        => Wait.Record(elapsed.TotalSeconds, new KeyValuePair<string, object?>("operation", Operation(context)));

    internal static void RecordDuration(HttpContext context, TimeSpan elapsed)
        => Duration.Record(elapsed.TotalSeconds, new KeyValuePair<string, object?>("operation", Operation(context)));

    internal static void RecordCleanup(int count, string outcome)
        => Cleanup.Add(count, new KeyValuePair<string, object?>("outcome", outcome));
}
