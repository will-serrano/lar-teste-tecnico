using CandidateAssessment.Infrastructure.Persistence;

namespace CandidateAssessment.Api.Extensions;

internal static class HealthChecksExtensions
{
    // Armazenado em um array static readonly para que o registro das verificações de
    // integridade não aloque um novo array de tags a cada inicialização do host (CA1861).
    private static readonly string[] _databaseHealthTags = ["ready", "db"];

    /// <summary>
    /// Registra verificações de atividade e prontidão. A verificação de prontidão confirma
    /// que o banco SQLite está acessível; a verificação de atividade apenas confirma
    /// que o processo está respondendo.
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
