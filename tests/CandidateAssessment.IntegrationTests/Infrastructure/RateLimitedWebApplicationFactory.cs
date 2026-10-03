using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace CandidateAssessment.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory dedicada ao teste de limitação de taxa, configurada com
/// uma cota restrita por IP. Mantida fora da coleção compartilhada porque o contador
/// de requisições persiste entre chamadas — caso contrário, a suíte compartilhada
/// esgotaria a cota de testes sem relação com esse cenário.
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
