using Asp.Versioning;

namespace CandidateAssessment.Api.Extensions;

internal static class ApiVersioningExtensions
{
    /// <summary>
    /// Configures API versioning that reads the version from the <c>X-Version</c>
    /// header (or query string), with a default of <c>1.0</c> when the caller
    /// does not specify one. Reporting is enabled so the response carries an
    /// <c>api-supported-versions</c> header.
    /// </summary>
    /// <remarks>
    /// We intentionally keep URL paths as <c>/api/v1/...</c> for stability with
    /// existing clients. The header-based reader is the seam for a future v2
    /// rollout without breaking the v1 surface.
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
