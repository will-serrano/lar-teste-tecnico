using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CandidateAssessment.Application.Diagnostics;

public static class ApplicationDiagnostics
{
    public const string SourceName = "CandidateAssessment.Application";
    public const string MeterName = SourceName;
    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Operations = Meter.CreateCounter<long>("candidate.operation.count");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("candidate.operation.duration", "s");
    public static Operation StartOperation(string operation) => new(NormalizeOperation(operation));

    private static string NormalizeOperation(string operation) => operation switch
    {
        "persons.create" or "persons.update" or "persons.delete" or "persons.restore" or "persons.get"
            or "persons.search" or "persons.deleted" or "phones.create" or "phones.update"
            or "phones.delete" or "phones.get" or "phones.list" or "auth.login" => operation,
        _ => "other",
    };

    public sealed class Operation : IDisposable
    {
        private readonly string _operation;
        private readonly Activity? _activity;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private bool _completed;
        private bool _disposed;

        internal Operation(string operation)
        {
            _operation = operation;
            _activity = ActivitySource.StartActivity(operation);
            _activity?.SetTag("operation", operation);
        }

        public void Complete() => _completed = true;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var outcome = _completed ? "success" : "failed";
            _activity?.SetTag("outcome", outcome);
            _activity?.SetStatus(_completed ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            Operations.Add(1, new("operation", _operation), new("outcome", outcome));
            Duration.Record(_stopwatch.Elapsed.TotalSeconds, new("operation", _operation), new("outcome", outcome));
            _activity?.Dispose();
        }
    }
}
