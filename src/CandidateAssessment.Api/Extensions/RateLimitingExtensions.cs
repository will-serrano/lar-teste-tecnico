using AspNetCoreRateLimit;

namespace CandidateAssessment.Api.Extensions;

internal static class RateLimitingExtensions
{
    /// <summary>
    /// Configura AspNetCoreRateLimit com armazenamento em memória e políticas baseadas em IP,
    /// definidas pela seção de configuração <c>IpRateLimiting</c>.
    /// Os endpoints de verificação de integridade são permitidos pela configuração para que
    /// verificações externas possam acessá-los sem consumir a cota de requisições.
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions();
        services.AddMemoryCache();
        services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
        services.Configure<IpRateLimitPolicies>(configuration.GetSection("IpRateLimitPolicies"));

        services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
        services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
        services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
        services.AddInMemoryRateLimiting();

        return services;
    }

    /// <summary>
    /// Adiciona o middleware de limitação de taxa ao pipeline de requisições.
    /// </summary>
    public static IApplicationBuilder UseApiRateLimiting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseIpRateLimiting();
    }
}
