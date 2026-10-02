using AspNetCoreRateLimit;

namespace CandidateAssessment.Api.Extensions;

internal static class RateLimitingExtensions
{
    /// <summary>
    /// Wires up AspNetCoreRateLimit with an in-memory backing store and IP-based
    /// policies configured through the <c>IpRateLimiting</c> configuration section.
    /// Health-check endpoints are whitelisted via configuration so external probes
    /// can hit them without consuming the request budget.
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
    /// Hooks the rate-limiting middleware into the request pipeline.
    /// </summary>
    public static IApplicationBuilder UseApiRateLimiting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseIpRateLimiting();
    }
}
