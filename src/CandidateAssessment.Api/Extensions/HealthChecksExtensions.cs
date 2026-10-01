using CandidateAssessment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateAssessment.Api.Extensions;

internal static class HealthChecksExtensions
{
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
                tags: new[] { "ready", "db" });

        return services;
    }
}
