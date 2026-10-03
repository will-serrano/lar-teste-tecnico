using System.Diagnostics.Tracing;
using CandidateAssessment.Api.Diagnostics;

namespace CandidateAssessment.UnitTests.Diagnostics;

public sealed class TelemetryDiagnosticsTests
{
    [Theory]
    [InlineData("OpenTelemetry-Exporter-OpenTelemetryProtocol", "FailedToReachCollector", EventLevel.Error, null, (int)ExporterDiagnosticsService.DiagnosticKind.ExportFailure)]
    [InlineData("OpenTelemetry-Exporter-OpenTelemetryProtocol", "ExportMethodException", EventLevel.Error, null, (int)ExporterDiagnosticsService.DiagnosticKind.ExportFailure)]
    [InlineData("OpenTelemetry-Exporter-OpenTelemetryProtocol", "UnsupportedAttributeType", EventLevel.Warning, null, (int)ExporterDiagnosticsService.DiagnosticKind.SdkDiagnostic)]
    [InlineData("OpenTelemetry-Sdk", "MetricReaderException", EventLevel.Error, null, (int)ExporterDiagnosticsService.DiagnosticKind.SdkDiagnostic)]
    [InlineData("OpenTelemetry-Sdk", "MetricInstrumentIgnored", EventLevel.Warning, "Instrument belongs to a Meter not subscribed by the provider.", (int)ExporterDiagnosticsService.DiagnosticKind.ExpectedSdkDiagnostic)]
    [InlineData("OpenTelemetry-Sdk", "MetricInstrumentIgnored", EventLevel.Warning, "SDK internal error occurred.", (int)ExporterDiagnosticsService.DiagnosticKind.SdkDiagnostic)]
    [InlineData("OpenTelemetry-Sdk", "ActivityStarted", EventLevel.Verbose, null, (int)ExporterDiagnosticsService.DiagnosticKind.Ignored)]
    public void DiagnosticsAreClassifiedWithoutTreatingEverySdkWarningAsAnExportFailure(
        string source, string eventName, EventLevel level, string? reason, int expected)
    {
        Assert.Equal(expected, (int)ExporterDiagnosticsService.Classify(source, eventName, level, reason));
    }
}
