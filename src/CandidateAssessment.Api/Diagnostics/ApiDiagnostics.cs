using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Routing;

namespace CandidateAssessment.Api.Diagnostics;

public static class ApiDiagnostics
{
    public const string SourceName = "CandidateAssessment.Api";
    public const string MeterName = SourceName;
    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> Requests = Meter.CreateCounter<long>("candidate.http.request.count");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("candidate.http.request.duration", "s");
    public static readonly Counter<long> ExportFailures = Meter.CreateCounter<long>("candidate.telemetry.export.failure.count");
    public static readonly Counter<long> SdkDiagnostics = Meter.CreateCounter<long>("candidate.telemetry.sdk.diagnostic.count");

    public static string Route(HttpContext context) =>
        (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";

    public static string Method(string method) => method switch
    {
        "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "HEAD" or "OPTIONS" => method,
        _ => "OTHER",
    };

    public static bool IsNoise(PathString path) =>
        path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);
}
