using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CandidateAssessment.Api.Configuration;
using CandidateAssessment.Api.Diagnostics;
using CandidateAssessment.Api.Extensions;
using CandidateAssessment.Api.Middleware;
using CandidateAssessment.Application.Diagnostics;
using CandidateAssessment.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace CandidateAssessment.IntegrationTests;

[CollectionDefinition("Observability", DisableParallelization = true)]
public sealed class ObservabilityCollection
{
}

[Collection("Observability")]
public sealed class ObservabilityTests
{
    private const string TraceId = "1234567890abcdef1234567890abcdef";
    private const string Sensitive = "private-cpf-token-password";
    private static readonly string[] NoisePaths = { "/health", "/health/ready", "/health/live", "/swagger/v1/swagger.json" };

    [Theory]
    [InlineData(200)]
    [InlineData(204)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(429)]
    [InlineData(503)]
    [InlineData(500)]
    public async Task RecordsBoundedHttpMetricsAndCorrelatedLogsWithoutSensitiveValues(int status)
    {
        using var capture = new TelemetryCapture();
        var logs = new CapturingSink();
        using var logger = CreateLogger(logs);
        using var server = CreateServer(logger, status);
        using var client = server.CreateClient();
        using var message = new HttpRequestMessage(HttpMethod.Get, $"/observed/{Sensitive}?cpf={Sensitive}");
        message.Headers.Add("traceparent", $"00-{TraceId}-1234567890abcdef-01");
        message.Headers.Add("Authorization", $"Bearer {Sensitive}");
        message.Headers.Add("Idempotency-Key", Sensitive);
        message.Headers.Add("baggage", $"cpf={Sensitive}");
        using var response = await client.SendAsync(message);

        Assert.Equal(status, (int)response.StatusCode);
        var activity = Assert.Single(capture.Activities.Where(a => a.Source.Name == ApiDiagnostics.SourceName));
        Assert.Equal(TraceId, activity.TraceId.ToHexString());
        Assert.Equal("GET /observed/{id}", activity.DisplayName);
        Assert.Equal(status >= 500 ? ActivityStatusCode.Error : ActivityStatusCode.Ok, activity.Status);
        var requestMetric = Assert.Single(capture.Measurements.Where(m => m.Name == "candidate.http.request.count"));
        Assert.Equal(1, requestMetric.Value);
        Assert.Equal("/observed/{id}", requestMetric.Tags["http.route"]);
        Assert.Equal(status, requestMetric.Tags["http.response.status_code"]);
        Assert.Equal(3, requestMetric.Tags.Count);
        Assert.Single(capture.Measurements.Where(m => m.Name == "candidate.http.request.duration"));
        var observedLog = logs.Events.Single(e => e.MessageTemplate.Text.StartsWith("Observed", StringComparison.Ordinal));
        Assert.Equal(TraceId, ((ScalarValue)observedLog.Properties["OtelTraceId"]).Value);
        Assert.Equal(activity.Id, ((ScalarValue)observedLog.Properties["TraceId"]).Value);
        Assert.Equal(activity.SpanId.ToHexString(), ((ScalarValue)observedLog.Properties["SpanId"]).Value);
        Assert.False(string.IsNullOrEmpty(((ScalarValue)observedLog.Properties["RequestId"]).Value as string));
        Assert.All(capture.Activities, a => Assert.DoesNotContain(Sensitive, SerializeActivity(a)));
        Assert.All(logs.Events, e =>
        {
            Assert.Null(e.Exception);
            Assert.DoesNotContain(Sensitive, e.RenderMessage() + string.Join(" ", e.Properties.Values));
        });
        Assert.All(capture.Measurements, m => Assert.DoesNotContain(Sensitive, string.Join(" ", m.Tags.Values)));
        if (status == 500)
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(activity.Id, problem.GetProperty("traceId").GetString());
        }
    }

    [Fact]
    public async Task EachAttemptHasItsOwnSpanAndUnmatchedPathsCollapse()
    {
        using var capture = new TelemetryCapture();
        using var logger = CreateLogger(new CapturingSink());
        using var server = CreateServer(logger, 204);
        using var client = server.CreateClient();
        using var first = await client.GetAsync($"/observed/{Guid.NewGuid()}");
        using var second = await client.GetAsync($"/observed/{Guid.NewGuid()}");
        using var unknown = await client.GetAsync($"/unknown-{Sensitive}");
        var attempts = capture.Activities.Where(a => a.Source.Name == ApiDiagnostics.SourceName).ToArray();
        Assert.Equal(3, attempts.Length);
        Assert.NotEqual(attempts[0].SpanId, attempts[1].SpanId);
        Assert.Contains(attempts, a => a.DisplayName == "GET unmatched");
        Assert.DoesNotContain(capture.Measurements, m => string.Join(" ", m.Tags.Values).Contains(Sensitive, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RealApiReplayEmitsAnAttemptSpanWithoutRepeatingTheHandler()
    {
        using var capture = new TelemetryCapture();
        // A factory and connection dedicated to this test; requests are deliberately sequential.
        using var factory = new CandidateAssessmentWebApplicationFactory();
        factory.EnsureDatabaseCreated();
        using var client = await factory.CreateAuthenticatedAdminClientAsync();
        async Task<HttpResponseMessage> SendAsync(string parentSpan)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/persons")
            {
                Content = JsonContent.Create(new { name = Sensitive, cpf = "12345678909", birthDate = "1990-01-01" }),
            };
            message.Headers.Add("traceparent", $"00-{TraceId}-{parentSpan}-01");
            message.Headers.Add("Idempotency-Key", "telemetry-replay-private-key");
            return await client.SendAsync(message);
        }

        using var first = await SendAsync("1111111111111111");
        using var replay = await SendAsync("2222222222222222");
        Assert.Equal(201, (int)first.StatusCode);
        Assert.Equal(201, (int)replay.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
        var spans = capture.Activities.Where(a => a.TraceId.ToHexString() == TraceId).ToArray();
        var requests = spans.Where(a => a.Source.Name == ApiDiagnostics.SourceName).ToArray();
        Assert.Equal(2, requests.Length);
        Assert.NotEqual(requests[0].SpanId, requests[1].SpanId);
        Assert.Single(spans.Where(a => a.DisplayName == "persons.create"));
        Assert.Equal(2, spans.Count(a => a.Source.Name == "CandidateAssessment.Idempotency"));
        Assert.Contains(capture.Measurements, m => m.Name == "candidateassessment.idempotency.operations" && Equals(m.Tags["outcome"], "replayed"));
        Assert.All(spans, activity =>
        {
            var serialized = SerializeActivity(activity);
            Assert.DoesNotContain("12345678909", serialized);
            Assert.DoesNotContain(Sensitive, serialized);
            Assert.DoesNotContain("telemetry-replay-private-key", serialized);
            Assert.DoesNotContain("SELECT", serialized);
        });
    }

    [Theory]
    [InlineData("json")]
    [InlineData("validation")]
    [InlineData("key")]
    public async Task IdempotentRejectionsUseTheExactRequestActivityIdForProblemDetails(string rejection)
    {
        using var capture = new TelemetryCapture();
        using var factory = new CandidateAssessmentWebApplicationFactory();
        factory.EnsureDatabaseCreated();
        using var client = await factory.CreateAuthenticatedAdminClientAsync();
        var body = rejection switch
        {
            "json" => "{",
            "validation" => "{\"name\":\"\",\"cpf\":\"12345678909\",\"birthDate\":\"1990-01-01\"}",
            _ => "{\"name\":\"Valid\",\"cpf\":\"12345678909\",\"birthDate\":\"1990-01-01\"}",
        };
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/persons")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        message.Headers.Add("traceparent", $"00-{TraceId}-3333333333333333-01");
        message.Headers.Add("Idempotency-Key", rejection == "key" ? "invalid key" : $"correlation-{rejection}");
        using var response = await client.SendAsync(message);
        Assert.Equal(400, (int)response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var server = Assert.Single(capture.Activities.Where(a =>
            a.Source.Name == ApiDiagnostics.SourceName && a.TraceId.ToHexString() == TraceId));
        Assert.Equal(server.Id, problem.GetProperty("traceId").GetString());
        if (rejection == "validation")
        {
            Assert.Contains(capture.Activities, a => a.Source.Name == "CandidateAssessment.Idempotency"
                && a.TraceId == server.TraceId && a.Id != server.Id);
        }
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/health/live")]
    [InlineData("/swagger/v1/swagger.json")]
    public async Task HealthAndSwaggerRemainFunctionalWithoutNoisyHttpSignals(string path)
    {
        using var capture = new TelemetryCapture();
        using var logger = CreateLogger(new CapturingSink());
        using var server = CreateServer(logger, 200);
        using var client = server.CreateClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(200, (int)response.StatusCode);
        Assert.DoesNotContain(capture.Activities, a => a.Source.Name == ApiDiagnostics.SourceName);
        Assert.DoesNotContain(capture.Measurements, m => m.Name.StartsWith("candidate.http.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SamplingRatio", "-0.1")]
    [InlineData("SamplingRatio", "NaN")]
    [InlineData("OtlpEndpoint", "http://user:password@collector:4317")]
    [InlineData("OtlpEndpoint", "http://collector:4317?cpf=private")]
    [InlineData("OtlpEndpoint", "file:///collector")]
    [InlineData("OtlpProtocol", "invalid")]
    [InlineData("MaxExportBatchSize", "4096")]
    [InlineData("ExportTimeoutMilliseconds", "0")]
    public void InvalidConfigurationIsRejectedEvenWithoutExport(string property, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"Observability:{property}"] = value,
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        Assert.ThrowsAny<Exception>(() =>
        {
            services.AddApiObservability(configuration);
            using var provider = services.BuildServiceProvider();
            _ = provider.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        });
    }

    [Fact]
    public async Task UnavailableCollectorDoesNotFailRequestsAndReportsSanitizedExportFailure()
    {
        using var capture = new TelemetryCapture();
        var logs = new CapturingSink();
        using var logger = CreateLogger(logs);
        using var server = CreateServer(logger, 204, new Dictionary<string, string?>
        {
            ["Observability:OtlpEnabled"] = "true",
            ["Observability:OtlpProtocol"] = "http/protobuf",
            ["Observability:OtlpEndpoint"] = "http://127.0.0.1:1",
            ["Observability:ExportTimeoutMilliseconds"] = "100",
            ["Observability:MetricExportIntervalMilliseconds"] = "1000",
        });
        using var diagnostics = new ExporterDiagnosticsService(server.Services.GetRequiredService<ILogger<ExporterDiagnosticsService>>());
        using var client = server.CreateClient();
        using var response = await client.GetAsync("/observed/one");
        Assert.Equal(204, (int)response.StatusCode);
        server.Services.GetRequiredService<OpenTelemetry.Trace.TracerProvider>().ForceFlush(1000);
        server.Services.GetRequiredService<OpenTelemetry.Metrics.MeterProvider>().ForceFlush(1000);
        Assert.Contains(capture.Measurements, m => m.Name == "candidate.telemetry.export.failure.count");
        Assert.Contains(logs.Events, e =>
            e.Properties.TryGetValue("DiagnosticCategory", out var category)
            && category is ScalarValue { Value: "ExportFailure" }
            && Equals(((ScalarValue)e.Properties["DiagnosticSource"]).Value, "OpenTelemetry-Exporter-OpenTelemetryProtocol"));
    }

    [Fact]
    public void CustomLabelsAreBounded()
    {
        using var capture = new TelemetryCapture();
        using (var operation = ApplicationDiagnostics.StartOperation(Sensitive))
        {
            operation.Complete();
        }
        Assert.Contains(capture.Measurements, m => m.Name == "candidate.operation.count" && Equals(m.Tags["operation"], "other"));
        Assert.All(capture.Measurements, m => Assert.DoesNotContain(Sensitive, string.Join(" ", m.Tags.Values)));
    }

    [Fact]
    public async Task UnsampledRequestsStillCountInMetrics()
    {
        var measurements = new ConcurrentQueue<string>();
        using var meter = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ApiDiagnostics.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        meter.SetMeasurementEventCallback<long>((instrument, _, _, _) => measurements.Enqueue(instrument.Name));
        meter.Start();
        using var logger = CreateLogger(new CapturingSink());
        using var server = CreateServer(logger, 204, new Dictionary<string, string?>
        {
            ["Observability:SamplingRatio"] = "0",
        });
        _ = server.Services.GetRequiredService<TracerProvider>();
        _ = server.Services.GetRequiredService<MeterProvider>();
        using var client = server.CreateClient();
        using var response = await client.GetAsync("/observed/unsampled");
        Assert.Equal(204, (int)response.StatusCode);
        Assert.Equal("False", Assert.Single(response.Headers.GetValues("Observed-Sampled")));
        Assert.Contains("candidate.http.request.count", measurements);
    }

    [Fact]
    public async Task HttpProtobufExportsToTheCorrectSignalPaths()
    {
        var paths = new ConcurrentQueue<string>();
        using var collector = new WebHostBuilder()
            .UseKestrel()
            .UseUrls("http://127.0.0.1:0")
            .Configure(app => app.Run(context =>
            {
                paths.Enqueue(context.Request.Path.Value!);
                context.Response.StatusCode = 200;
                return Task.CompletedTask;
            }))
            .Build();
        await collector.StartAsync();
        var address = collector.ServerFeatures.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var logger = CreateLogger(new CapturingSink());
        using var server = CreateServer(logger, 204, new Dictionary<string, string?>
        {
            ["Observability:OtlpEnabled"] = "true",
            ["Observability:OtlpProtocol"] = "http/protobuf",
            ["Observability:OtlpEndpoint"] = address,
            ["Observability:ExportTimeoutMilliseconds"] = "1000",
            ["Observability:MetricExportIntervalMilliseconds"] = "1000",
        });
        _ = server.Services.GetRequiredService<TracerProvider>();
        _ = server.Services.GetRequiredService<MeterProvider>();
        using var client = server.CreateClient();
        using var response = await client.GetAsync("/observed/export");
        Assert.Equal(204, (int)response.StatusCode);
        Assert.True(server.Services.GetRequiredService<TracerProvider>().ForceFlush(3000));
        Assert.True(server.Services.GetRequiredService<MeterProvider>().ForceFlush(3000));
        Assert.Contains("/v1/traces", paths);
        Assert.Contains("/v1/metrics", paths);
        await collector.StopAsync();
    }

    private static Serilog.Core.Logger CreateLogger(CapturingSink sink) => new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .Enrich.FromLogContext()
        .WriteTo.Sink(new SanitizingLogSink(new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger()))
        .CreateLogger();

    private static TestServer CreateServer(Serilog.ILogger logger, int status, Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? new()).Build();
        return new TestServer(new WebHostBuilder()
            .ConfigureLogging(logging => logging.ClearProviders().AddSerilog(logger))
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddControllers();
                services.AddApiObservability(configuration);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseMiddleware<TelemetryCorrelationMiddleware>();
                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/observed/{id}", async context =>
                    {
                        context.Response.Headers["Observed-Sampled"] = Activity.Current?.Recorded.ToString();
                        context.RequestServices.GetRequiredService<ILogger<ObservabilityTests>>()
                            .LogInformation("Observed {Path} {Message} {Token}", context.Request.Path, Sensitive, Sensitive);
                        if (status == 500)
                        {
                            throw new InvalidOperationException(Sensitive);
                        }

                        context.Response.StatusCode = status;
                        await Task.CompletedTask;
                    });
                    foreach (var path in NoisePaths)
                    {
                        endpoints.MapGet(path, context =>
                        {
                            context.Response.StatusCode = 200;
                            return Task.CompletedTask;
                        });
                    }
                });
            }));
    }

    internal static string SerializeActivity(Activity activity) =>
        activity.DisplayName + " " + activity.StatusDescription + " " + string.Join(" ", activity.TagObjects)
        + " " + string.Join(" ", activity.Events.SelectMany(e => e.Tags)) + " " + string.Join(" ", activity.Baggage);

    internal sealed class CapturingSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }

    internal sealed class TelemetryCapture : IDisposable
    {
        private readonly ActivityListener _activities;
        private readonly MeterListener _metrics;
        public ConcurrentQueue<Activity> Activities { get; } = new();
        public ConcurrentQueue<Measurement> Measurements { get; } = new();

        public TelemetryCapture()
        {
            _activities = new ActivityListener
            {
                ShouldListenTo = source => source.Name.StartsWith("CandidateAssessment.", StringComparison.Ordinal),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => Activities.Enqueue(activity),
            };
            ActivitySource.AddActivityListener(_activities);
            _metrics = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name.StartsWith("CandidateAssessment.", StringComparison.Ordinal))
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };
            _metrics.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Add(instrument, value, tags));
            _metrics.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Add(instrument, value, tags));
            _metrics.Start();
        }

        private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
            Measurements.Enqueue(new Measurement(instrument.Name, value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));

        public void Dispose()
        {
            _activities.Dispose();
            _metrics.Dispose();
        }
    }

    internal sealed record Measurement(string Name, double Value, Dictionary<string, object?> Tags);
}
