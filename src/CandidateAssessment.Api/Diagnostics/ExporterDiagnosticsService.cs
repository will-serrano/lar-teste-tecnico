using System.Diagnostics.Tracing;

namespace CandidateAssessment.Api.Diagnostics;

public sealed class ExporterDiagnosticsService : EventListener, IHostedService
{
    private readonly ILogger<ExporterDiagnosticsService> _logger;
    private long _nextExportLogAt;
    private long _nextSdkLogAt;

    public ExporterDiagnosticsService(ILogger<ExporterDiagnosticsService> logger) => _logger = logger;

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name is "OpenTelemetry-Sdk" or "OpenTelemetry-Exporter-OpenTelemetryProtocol"
            or "OpenTelemetry-Instrumentation-Runtime")
        {
            EnableEvents(eventSource, EventLevel.Warning);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        var kind = Classify(eventData.EventSource.Name, eventData.EventName, eventData.Level,
            eventData.Payload is { Count: > 2 } ? eventData.Payload[2] as string : null);
        if (kind == DiagnosticKind.Ignored)
        {
            return;
        }

        if (kind == DiagnosticKind.ExpectedSdkDiagnostic)
        {
            WriteDiagnostic(LogLevel.Debug, "Telemetry SDK expected diagnostic", eventData, kind);
            return;
        }

        var exporterFailure = kind == DiagnosticKind.ExportFailure;
        if (exporterFailure)
        {
            ApiDiagnostics.ExportFailures.Add(1);
        }
        else
        {
            ApiDiagnostics.SdkDiagnostics.Add(1);
        }

        var now = Environment.TickCount64;
        ref var nextLogAt = ref (exporterFailure ? ref _nextExportLogAt : ref _nextSdkLogAt);
        var next = Interlocked.Read(ref nextLogAt);
        if (_logger is not null && now >= next && Interlocked.CompareExchange(ref nextLogAt, now + 60000, next) == next)
        {
            WriteDiagnostic(LogLevel.Warning, exporterFailure
                ? "Telemetry export failure; check Collector connectivity and configuration"
                : "Telemetry SDK diagnostic; inspect the event identifier and SDK configuration", eventData, kind);
        }
    }

    private void WriteDiagnostic(LogLevel level, string message, EventWrittenEventArgs eventData, DiagnosticKind kind)
    {
        // Event metadata is technical; payloads may include URLs, exceptions or data.
        _logger?.Log(level, "{MessageType}: {DiagnosticSource}/{DiagnosticEventName} ({DiagnosticEventId}, {DiagnosticLevel}, {DiagnosticCategory})",
            message, eventData.EventSource.Name, eventData.EventName ?? "Unknown", eventData.EventId,
            eventData.Level.ToString(), kind.ToString());
    }

    internal static DiagnosticKind Classify(string source, string? eventName, EventLevel level, string? reason)
    {
        if (level is not (EventLevel.Warning or EventLevel.Error or EventLevel.Critical))
        {
            return DiagnosticKind.Ignored;
        }

        if (source == "OpenTelemetry-Sdk" && eventName == "MetricInstrumentIgnored"
            && reason == "Instrument belongs to a Meter not subscribed by the provider.")
        {
            // SDK 1.6 reports intentionally unsubscribed framework meters as warnings.
            return DiagnosticKind.ExpectedSdkDiagnostic;
        }

        return source == "OpenTelemetry-Exporter-OpenTelemetryProtocol"
            && eventName is "FailedToReachCollector" or "ExportMethodException"
            ? DiagnosticKind.ExportFailure
            : DiagnosticKind.SdkDiagnostic;
    }

    internal enum DiagnosticKind
    {
        Ignored,
        ExpectedSdkDiagnostic,
        SdkDiagnostic,
        ExportFailure,
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
