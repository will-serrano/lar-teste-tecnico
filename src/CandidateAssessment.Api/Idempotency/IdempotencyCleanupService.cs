using CandidateAssessment.Api.Configuration;
using CandidateAssessment.Application.Abstractions.Idempotency;
using Microsoft.Extensions.Options;

namespace CandidateAssessment.Api.Idempotency;

public sealed class IdempotencyCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IdempotencyOptions _options;
    private readonly ILogger<IdempotencyCleanupService> _logger;

    public IdempotencyCleanupService(IServiceScopeFactory scopes, IOptions<IdempotencyOptions> options,
        ILogger<IdempotencyCleanupService> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.CleanupInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var removed = await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>()
                        .DeleteExpiredAsync(_options.CleanupBatchSize, _options.LockTimeoutSeconds, stoppingToken);
                    _logger.LogInformation("Idempotency cleanup removed {RemovedCount} expired records.", removed);
                    IdempotencyDiagnostics.RecordCleanup(removed, "removed");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Idempotency cleanup failed; it will retry at the next interval.");
                    IdempotencyDiagnostics.RecordCleanup(1, "failed");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Idempotency cleanup stopped.");
        }
    }
}
