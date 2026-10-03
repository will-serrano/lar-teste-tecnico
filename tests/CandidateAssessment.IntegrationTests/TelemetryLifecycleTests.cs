using System.Diagnostics.Metrics;
using CandidateAssessment.Api.Diagnostics;
using CandidateAssessment.Api.Extensions;
using CandidateAssessment.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace CandidateAssessment.IntegrationTests;

[Collection("Observability")]
public sealed class TelemetryLifecycleTests
{
    [Fact]
    public async Task StartupIsLoggedOnceBeforeRunAsyncDisposesTheHost()
    {
        var logs = new ObservabilityTests.CapturingSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(logs).CreateLogger();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "LifecycleTest" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.LogApiStartup(logger);
        app.Lifetime.ApplicationStarted.Register(app.Lifetime.StopApplication);
        Assert.Empty(logs.Events);

        await app.RunAsync();

        var started = Assert.Single(logs.Events);
        Assert.Equal("Candidate Assessment API started on environment {Environment}", started.MessageTemplate.Text);
        Assert.Equal("LifecycleTest", ((ScalarValue)started.Properties["Environment"]).Value);
        Assert.Throws<ObjectDisposedException>(() => app.Environment);
    }

    [Fact]
    public void RealApiStartupWithoutOtlpDoesNotReportExportFailureOrUnexpectedSdkWarnings()
    {
        using var capture = new ObservabilityTests.TelemetryCapture();
        var logs = new ObservabilityTests.CapturingSink();
        using var logger = new LoggerConfiguration().MinimumLevel.Debug()
            .WriteTo.Sink(new SanitizingLogSink(new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(logs).CreateLogger()))
            .CreateLogger();
        using var loggerFactory = LoggerFactory.Create(logging => logging.ClearProviders().AddSerilog(logger));
        using var diagnostics = new ExporterDiagnosticsService(loggerFactory.CreateLogger<ExporterDiagnosticsService>());
        using var factory = new CandidateAssessmentWebApplicationFactory();
        factory.EnsureDatabaseCreated();
        using var unsubscribedMeter = new Meter("Telemetry.Test.Unsubscribed");
        _ = unsubscribedMeter.CreateCounter<long>("test.unsubscribed");

        Assert.DoesNotContain(capture.Measurements, m => m.Name == "candidate.telemetry.export.failure.count");
        Assert.Contains(logs.Events, e =>
            e.Properties.TryGetValue("DiagnosticEventName", out var eventName)
            && eventName is ScalarValue { Value: "MetricInstrumentIgnored" }
            && Equals(((ScalarValue)e.Properties["DiagnosticEventId"]).Value, 33)
            && Equals(((ScalarValue)e.Properties["DiagnosticCategory"]).Value, "ExpectedSdkDiagnostic"));
        var warnings = logs.Events.Where(e => e.Level >= LogEventLevel.Warning).ToArray();
        Assert.True(warnings.Length == 0, string.Join(Environment.NewLine, warnings.Select(e => e.RenderMessage())));
    }
}
