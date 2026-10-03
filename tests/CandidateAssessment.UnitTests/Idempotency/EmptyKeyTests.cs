using CandidateAssessment.Api.Configuration;
using CandidateAssessment.Api.Idempotency;
using CandidateAssessment.Api.Middleware;
using CandidateAssessment.Application.Abstractions.Idempotency;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CandidateAssessment.UnitTests.Idempotency;

public sealed class EmptyKeyTests
{
    [Fact]
    public async Task PresentEmptyHeader_ShouldReturn400WithoutExecutingTheAction()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;
        context.Request.Headers["Idempotency-Key"] = string.Empty;
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new IdempotentAttribute()), "write"));
        var middleware = new IdempotencyMiddleware(
            _ => throw new InvalidOperationException("The action must not execute."),
            Options.Create(new IdempotencyOptions()), NullLogger<IdempotencyMiddleware>.Instance);
        await middleware.InvokeAsync(context, new UnusedStore(), provider.GetRequiredService<ProblemDetailsFactory>());
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        body.Position = 0;
        using var reader = new StreamReader(body);
        Assert.Contains("InvalidIdempotencyKey", await reader.ReadToEndAsync());
    }

    private sealed class UnusedStore : IIdempotencyStore
    {
        public Task<IdempotencyEntry?> FindAsync(string scopeHash, string keyHash, int lockTimeoutSeconds, CancellationToken cancellationToken)
            => throw new InvalidOperationException("The store must not be accessed.");

        public Task<IIdempotencySession> BeginAsync(string scopeHash, string keyHash, string fingerprint, int lockTimeoutSeconds, CancellationToken cancellationToken)
            => throw new InvalidOperationException("The store must not be accessed.");

        public Task<int> DeleteExpiredAsync(int batchSize, int lockTimeoutSeconds, CancellationToken cancellationToken)
            => throw new InvalidOperationException("The store must not be accessed.");
    }
}
