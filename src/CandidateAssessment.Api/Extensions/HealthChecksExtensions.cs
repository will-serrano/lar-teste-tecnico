using CandidateAssessment.Infrastructure.Persistence;

namespace CandidateAssessment.Api.Extensions;

internal static class HealthChecksExtensions
{
    // Hoisted to a static readonly array so the health-check registration does
    // not allocate a new tag array on every host start (CA1861).
    private static readonly string[] _databaseHealthTags = ["ready", "db"];

    /// <summary>
    /// Registers liveness and readiness checks. The readiness probe verifies
    /// that the SQLite database can be reached; the liveness probe only
    /// confirms the process is responsive.
    /// </summary>
    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(
                name: "database",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: _databaseHealthTags);

        return services;
    }
}
