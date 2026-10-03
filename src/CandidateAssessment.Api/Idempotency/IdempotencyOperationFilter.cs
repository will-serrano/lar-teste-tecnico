using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CandidateAssessment.Api.Idempotency;

public sealed class IdempotencyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!context.MethodInfo.IsDefined(typeof(IdempotentAttribute), inherit: true))
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Idempotency-Key",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Optional user-scoped key (24h by default). Repeat a successful request without another mutation. Reusing with different input returns 409.",
            Schema = new OpenApiSchema { Type = "string", MinLength = 1, MaxLength = 128, Pattern = "^[A-Za-z0-9._:-]+$" },
        });
        foreach (var code in new[] { "400", "409", "413", "503" })
        {
            operation.Responses.TryAdd(code, new OpenApiResponse { Description = code == "503" ? "Database busy; retry with the same key after Retry-After." : "Idempotency request rejected." });
        }

        foreach (var code in new[] { "201", "204" })
        {
            if (operation.Responses.TryGetValue(code, out var response))
            {
                response.Headers["Idempotency-Replayed"] = new OpenApiHeader
                {
                    Description = "true when the stored successful response is replayed.",
                    Schema = new OpenApiSchema { Type = "boolean" },
                };
            }
        }
    }
}
