using System.Diagnostics;
using System.ComponentModel.DataAnnotations;
using CandidateAssessment.Api.Configuration;
using CandidateAssessment.Api.Diagnostics;
using CandidateAssessment.Application.Diagnostics;
using CandidateAssessment.Infrastructure.Diagnostics;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CandidateAssessment.Api.Extensions;

public static class ObservabilityExtensions
{
    private static readonly string[] ExporterValidationErrors = { "Invalid sampling, batching or OTLP endpoint/protocol configuration." };

    public static IServiceCollection AddApiObservability(this IServiceCollection services, IConfiguration configuration)
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
        services.AddOptions<ObservabilityOptions>()
            .Bind(configuration.GetSection(ObservabilityOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.HasValidExporterConfiguration(), "Invalid sampling, batching or OTLP endpoint/protocol configuration.")
            .ValidateOnStart();
        services.AddHostedService<ExporterDiagnosticsService>();
        var options = configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>() ?? new ObservabilityOptions();
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        if (!options.HasValidExporterConfiguration())
        {
            throw new OptionsValidationException(ObservabilityOptions.SectionName, typeof(ObservabilityOptions),
                ExporterValidationErrors);
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    configuration.GetValue<string>("Observability:ServiceName") ?? "CandidateAssessment.Api",
                    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString(),
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment", configuration["environment"] ?? configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production"),
                }))
            .WithTracing(tracing =>
            {
                tracing.AddSource(ApiDiagnostics.SourceName, ApplicationDiagnostics.SourceName, TelemetryDbCommandInterceptor.SourceName,
                        "CandidateAssessment.Idempotency", "CandidateAssessment.Cache")
                    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(options.SamplingRatio)));
                if (options.OtlpEnabled)
                {
                    tracing.AddOtlpExporter(exporter =>
                    {
                        ConfigureExporter(exporter, options, "traces");
                        exporter.BatchExportProcessorOptions.MaxQueueSize = options.MaxQueueSize;
                        exporter.BatchExportProcessorOptions.MaxExportBatchSize = options.MaxExportBatchSize;
                        exporter.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 5000;
                        exporter.BatchExportProcessorOptions.ExporterTimeoutMilliseconds = options.ExportTimeoutMilliseconds;
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(ApiDiagnostics.MeterName, ApplicationDiagnostics.MeterName, TelemetryDbCommandInterceptor.MeterName,
                        "CandidateAssessment.Idempotency", "CandidateAssessment.Cache")
                    .AddRuntimeInstrumentation()
                    .AddView("candidate.http.request.duration", new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = new double[] { 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10 },
                    });
                if (options.OtlpEnabled)
                {
                    metrics.AddOtlpExporter((exporter, reader) =>
                    {
                        ConfigureExporter(exporter, options, "metrics");
                        reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = options.MetricExportIntervalMilliseconds;
                        reader.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds = options.ExportTimeoutMilliseconds;
                    });
                }
            });
        return services;
    }

    private static void ConfigureExporter(OtlpExporterOptions exporter, ObservabilityOptions options, string signal)
    {
        exporter.Endpoint = new Uri(options.OtlpProtocol == "grpc"
            ? options.OtlpEndpoint
            : $"{options.OtlpEndpoint.TrimEnd('/')}/v1/{signal}");
        exporter.Protocol = options.OtlpProtocol == "grpc" ? OtlpExportProtocol.Grpc : OtlpExportProtocol.HttpProtobuf;
        exporter.TimeoutMilliseconds = options.ExportTimeoutMilliseconds;
        // Do not accept exporter credentials/headers implicitly from OTEL environment variables.
        exporter.Headers = null;
    }
}
