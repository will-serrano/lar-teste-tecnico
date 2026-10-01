using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace CandidateAssessment.IntegrationTests.Infrastructure;

/// <summary>
/// A dedicated WebApplicationFactory for the rate-limiting test, configured with
/// a strict per-IP quota. Kept outside the shared collection because the rate
/// limit counter store persists across requests — the shared suite would
/// otherwise exhaust the budget for unrelated tests.
/// </summary>
public sealed class RateLimitedWebApplicationFactory : CandidateAssessmentWebApplicationFactory
{
    private const string TightLimit = "5";

    private const string TightPeriod = "15s";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IpRateLimiting:GeneralRules:0:Endpoint"] = "*",
                ["IpRateLimiting:GeneralRules:0:Period"] = TightPeriod,
                ["IpRateLimiting:GeneralRules:0:Limit"] = TightLimit,
            });
        });
    }
}
