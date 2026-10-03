using System.Diagnostics;
using System.Diagnostics.Metrics;
using CandidateAssessment.Application.Diagnostics;
using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Application.Persons.Create;
using CandidateAssessment.UnitTests.Fakes;

namespace CandidateAssessment.UnitTests.Diagnostics;

public sealed class TelemetryHandlerTests
{
    [Fact]
    public async Task HandlerReportsBoundedSuccessAndFailureWithoutPersonalData()
    {
        var stopped = new List<Activity>();
        var metrics = new List<(string Name, string Tags)>();
        using var parent = new Activity("test").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ApplicationDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                options.TraceId == parent.TraceId ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == parent.TraceId)
                {
                    stopped.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, meter) =>
            {
                if (instrument.Meter.Name == ApplicationDiagnostics.MeterName)
                {
                    meter.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (Activity.Current?.TraceId == parent.TraceId)
            {
                metrics.Add((instrument.Name, string.Join(" ", tags.ToArray())));
            }
        });
        meterListener.Start();
        var repository = new FakePersonRepository();
        var (_, cache) = TestCacheFactory.Create();
        var handler = new CreatePersonHandler(repository, new FakeUnitOfWork(),
            new FixedDateTimeProvider(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)), cache);
        var command = new CreatePersonCommand("PrivatePerson", "12345678909", new DateOnly(1990, 1, 1));
        _ = await handler.HandleAsync(command);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => handler.HandleAsync(command));
        Assert.Equal(2, stopped.Count);
        Assert.All(stopped, activity =>
        {
            Assert.Equal("persons.create", activity.DisplayName);
            Assert.DoesNotContain(command.Cpf, string.Join(" ", activity.TagObjects));
            Assert.DoesNotContain(command.Name, string.Join(" ", activity.TagObjects));
            Assert.Empty(activity.Events);
        });
        Assert.Equal(ActivityStatusCode.Ok, stopped[0].Status);
        Assert.Equal(ActivityStatusCode.Error, stopped[1].Status);
        Assert.Contains(metrics, metric => metric.Name == "candidate.operation.count" && metric.Tags.Contains("success", StringComparison.Ordinal));
        Assert.Contains(metrics, metric => metric.Name == "candidate.operation.count" && metric.Tags.Contains("failed", StringComparison.Ordinal));
    }
}
