using CandidateAssessment.Api.Configuration;
using CandidateAssessment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CandidateAssessment.Api.Idempotency;

public sealed class DatabaseInitializationService : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly DatabaseOptions _options;
    private readonly ILogger<DatabaseInitializationService> _logger;

    public DatabaseInitializationService(IServiceScopeFactory scopes, IOptions<DatabaseOptions> options,
        ILogger<DatabaseInitializationService> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.ApplyMigrationsOnStartup)
        {
            return;
        }

        _logger.LogInformation("Applying explicitly enabled database migrations before accepting requests.");
        using var scope = _scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
