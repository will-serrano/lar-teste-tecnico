using CandidateAssessment.Api.Configuration;
using CandidateAssessment.Api.Idempotency;
using CandidateAssessment.Api.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CandidateAssessment.Api.Extensions;

public static class IdempotencyExtensions
{
    public static IServiceCollection AddApiIdempotency(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IdempotencyOptions>()
            .Bind(configuration.GetSection(IdempotencyOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(x => x.Lifetime > TimeSpan.Zero && x.Lifetime <= TimeSpan.FromDays(30), "Lifetime must be between zero and 30 days.")
            .Validate(x => x.CleanupInterval > TimeSpan.Zero && x.CleanupInterval <= TimeSpan.FromDays(1), "CleanupInterval must be between zero and one day.")
            .ValidateOnStart();
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.PostConfigure<ApiBehaviorOptions>(options =>
        {
            var original = options.InvalidModelStateResponseFactory;
            options.InvalidModelStateResponseFactory = context =>
            {
                var result = original(context);
                if (result is ObjectResult { Value: ProblemDetails problem })
                {
                    problem.Extensions["traceId"] = RequestCorrelation.GetId(context.HttpContext);
                }

                return result;
            };
        });
        services.AddHostedService<DatabaseInitializationService>();
        services.AddHostedService<IdempotencyCleanupService>();
        services.Configure<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>(options =>
            options.OperationFilter<IdempotencyOperationFilter>());
        return services;
    }
}
