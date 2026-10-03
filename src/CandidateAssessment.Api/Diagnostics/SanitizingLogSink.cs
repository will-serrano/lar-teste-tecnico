using Serilog.Core;
using Serilog.Events;

namespace CandidateAssessment.Api.Diagnostics;

// The existing exception/EF loggers may contain SQL or exception messages. Sanitize at
// the destination boundary so console and rolling files share the same privacy policy.
public sealed class SanitizingLogSink : ILogEventSink, IDisposable
{
    private static readonly HashSet<string> AllowedProperties = new(StringComparer.Ordinal)
    {
        "Application", "Environment", "MachineName", "SourceContext", "TraceId", "OtelTraceId",
        "SpanId", "RequestId", "Route", "RequestMethod", "Method", "StatusCode", "Elapsed",
        "ExceptionType", "ErrorType", "Operation", "Outcome", "RemovedCount", "Count",
        "DurationMs", "WaitMs", "Signal", "EventId", "ServiceName",
        "MessageType", "DiagnosticSource", "DiagnosticEventName", "DiagnosticEventId", "DiagnosticLevel", "DiagnosticCategory",
    };
    private readonly Serilog.ILogger _destination;

    public SanitizingLogSink(Serilog.ILogger destination) => _destination = destination;

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var properties = logEvent.Properties.Select(property => new LogEventProperty(
            property.Key,
            AllowedProperties.Contains(property.Key) ? property.Value : new ScalarValue("[redacted]"))).ToList();
        if (logEvent.Exception is not null)
        {
            properties.Add(new LogEventProperty("ErrorType", new ScalarValue(logEvent.Exception.GetType().Name)));
        }

        _destination.Write(new LogEvent(logEvent.Timestamp, logEvent.Level, null, logEvent.MessageTemplate, properties));
    }

    public void Dispose() => (_destination as IDisposable)?.Dispose();
}
