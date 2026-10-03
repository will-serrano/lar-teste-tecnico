using Asp.Versioning;

namespace CandidateAssessment.Api.Extensions;

internal static class ApiVersioningExtensions
{
    /// <summary>
    /// Configura o versionamento da API para ler a versão do cabeçalho <c>X-Version</c>
    /// (ou da query string), usando <c>1.0</c> como padrão quando o cliente não
    /// informar uma versão. O relatório fica habilitado para que a resposta inclua
    /// o cabeçalho <c>api-supported-versions</c>.
    /// </summary>
    /// <remarks>
    /// Mantemos intencionalmente os caminhos de URL como <c>/api/v1/...</c> para
    /// preservar a estabilidade para os clientes existentes. A leitura pelo
    /// cabeçalho permite lançar uma versão v2 futuramente sem quebrar a interface v1.
    /// </remarks>
    public static IServiceCollection AddApiVersioningWithExplorer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = ApiVersionReader.Combine(
                    new HeaderApiVersionReader("X-Version"),
                    new QueryStringApiVersionReader("api-version"));
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        return services;
    }
}
