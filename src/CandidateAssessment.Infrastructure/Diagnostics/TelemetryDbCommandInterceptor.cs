using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CandidateAssessment.Infrastructure.Diagnostics;

public sealed class TelemetryDbCommandInterceptor : DbCommandInterceptor
{
    public const string SourceName = "CandidateAssessment.Infrastructure.Database";
    public const string MeterName = SourceName;
    private static readonly ActivitySource Source = new(SourceName);
    private static readonly Meter Meter = new(MeterName);
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("candidate.db.command.duration", "s");
    private static readonly Counter<long> Errors = Meter.CreateCounter<long>("candidate.db.command.error.count");
    private readonly ConcurrentDictionary<Guid, CommandState> _commands = new();

    private void Begin(CommandEventData eventData, string operation)
    {
        var activity = Source.StartActivity($"sqlite.{operation}", ActivityKind.Client);
        activity?.SetTag("db.system", "sqlite");
        activity?.SetTag("db.operation", operation);
        _commands[eventData.CommandId] = new CommandState(operation, Stopwatch.GetTimestamp(), activity);
    }

    private void End(CommandEndEventData eventData, Exception? exception = null)
    {
        if (!_commands.TryRemove(eventData.CommandId, out var state))
        {
            return;
        }

        var outcome = exception is null ? "success" : "failed";
        Duration.Record((Stopwatch.GetTimestamp() - state.Started) / (double)Stopwatch.Frequency,
            new("operation", state.Operation), new("outcome", outcome));
        state.Activity?.SetTag("outcome", outcome);
        if (exception is not null)
        {
            Errors.Add(1, new KeyValuePair<string, object?>("operation", state.Operation));
            state.Activity?.SetTag("error.type", exception.GetType().Name);
            state.Activity?.SetStatus(ActivityStatusCode.Error);
        }
        else
        {
            state.Activity?.SetStatus(ActivityStatusCode.Ok);
        }

        state.Activity?.Dispose();
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Begin(eventData, "reader");
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Begin(eventData, "reader");
        return new(result);
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        End(eventData);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        End(eventData);
        return new(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Begin(eventData, "nonquery");
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Begin(eventData, "nonquery");
        return new(result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        End(eventData);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        End(eventData);
        return new(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Begin(eventData, "scalar");
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Begin(eventData, "scalar");
        return new(result);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        End(eventData);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, object? result,
        CancellationToken cancellationToken = default)
    {
        End(eventData);
        return new(result);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) => End(eventData, eventData.Exception);

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        End(eventData, eventData.Exception);
        return Task.CompletedTask;
    }

    private sealed record CommandState(string Operation, long Started, Activity? Activity);
}
