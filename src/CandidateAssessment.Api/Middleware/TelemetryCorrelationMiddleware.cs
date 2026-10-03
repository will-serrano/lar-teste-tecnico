using System.Diagnostics;
using CandidateAssessment.Api.Diagnostics;
using Serilog.Context;

namespace CandidateAssessment.Api.Middleware;

public sealed class TelemetryCorrelationMiddleware
{
    private readonly RequestDelegate _next;

    public TelemetryCorrelationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var excluded = ApiDiagnostics.IsNoise(context.Request.Path);
        var route = ApiDiagnostics.Route(context);
        var method = ApiDiagnostics.Method(context.Request.Method);
        var hostingActivity = Activity.Current;
        ActivityContext.TryParse(context.Request.Headers["traceparent"], context.Request.Headers["tracestate"], true, out var parent);
        Activity? activity = null;
        try
        {
            // Hosting's uninstrumented .NET 6 Activity is not a sampling decision.
            // Extract a remote parent explicitly; otherwise let the SDK sample a root.
            if (!excluded)
            {
                Activity.Current = null;
                activity = ApiDiagnostics.ActivitySource.StartActivity($"{method} {route}", ActivityKind.Server, parent);
            }
        }
        finally
        {
            Activity.Current = activity ?? hostingActivity;
        }

        activity?.SetTag("http.request.method", method);
        activity?.SetTag("http.route", route);

        RequestCorrelation.Capture(context);
        var current = Activity.Current;
        using var trace = LogContext.PushProperty("TraceId", RequestCorrelation.GetId(context));
        using var otelTrace = LogContext.PushProperty("OtelTraceId", current?.TraceId.ToHexString());
        using var span = LogContext.PushProperty("SpanId", current?.SpanId.ToHexString());
        using var request = LogContext.PushProperty("RequestId", context.TraceIdentifier);
        using var template = LogContext.PushProperty("Route", route);
        var started = Stopwatch.GetTimestamp();
        var failed = false;
        try
        {
            await _next(context);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            if (!excluded)
            {
                var status = failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
                activity?.SetTag("http.response.status_code", status);
                activity?.SetStatus(status >= 500 ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
                var tags = new TagList
                {
                    { "http.request.method", method },
                    { "http.route", route },
                    { "http.response.status_code", status },
                };
                ApiDiagnostics.Requests.Add(1, tags);
                ApiDiagnostics.Duration.Record(
                    (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency, tags);
            }

            activity?.Dispose();
            Activity.Current = hostingActivity;
        }
    }
}
