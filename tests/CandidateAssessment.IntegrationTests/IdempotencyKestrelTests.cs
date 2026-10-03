using System.Net;
using System.Security.Claims;
using System.Collections.Concurrent;
using CandidateAssessment.Api.Configuration;
using CandidateAssessment.Api.Idempotency;
using CandidateAssessment.Api.Middleware;
using CandidateAssessment.Application.Abstractions.Idempotency;
using CandidateAssessment.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CandidateAssessment.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class IdempotencyKestrelTests
{
    [Theory]
    [InlineData("POST", HttpStatusCode.Created)]
    [InlineData("PUT", HttpStatusCode.NoContent)]
    public async Task FirstResponseAndReplay_ShouldPreserveHeadersAndBytes_OnRealKestrel(string method, HttpStatusCode status)
    {
        var failures = new ConcurrentQueue<Exception>();
        using var completed = new SemaphoreSlim(0);
        using var host = new WebHostBuilder()
            .UseKestrel(options => options.Listen(IPAddress.Loopback, 0))
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(IdempotencyHeaderTestController).Assembly);
                services.AddSingleton<IOptions<IdempotencyOptions>>(Options.Create(new IdempotencyOptions()));
                services.AddSingleton<IOptions<JwtOptions>>(Options.Create(new JwtOptions()));
                services.AddSingleton<IIdempotencyStore, HeaderStore>();
            })
            .Configure(app =>
            {
                app.Use(async (context, next) =>
                {
                    try
                    {
                        await next();
                    }
                    catch (Exception ex)
                    {
                        failures.Enqueue(ex);
                        throw;
                    }
                    finally
                    {
                        completed.Release();
                    }
                });
                app.UseRouting();
                app.Use((context, next) =>
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "header-test")], "test"));
                    return next();
                });
                app.UseMiddleware<IdempotencyMiddleware>();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            })
            .Build();
        await host.StartAsync();
        try
        {
            var address = Assert.Single(host.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses);
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            client.DefaultRequestHeaders.Add("Idempotency-Key", "headers");
            using var firstRequest = new HttpRequestMessage(new HttpMethod(method), "/idempotency-header-test");
            using var replayRequest = new HttpRequestMessage(new HttpMethod(method), "/idempotency-header-test");
            using var first = await client.SendAsync(firstRequest);
            using var replay = await client.SendAsync(replayRequest);
            Assert.Equal(status, first.StatusCode);
            Assert.Equal(status, replay.StatusCode);
            Assert.Equal(status == HttpStatusCode.Created ? "/idempotency-header-test/created" : null, first.Headers.Location?.ToString());
            Assert.Equal(first.Headers.Location, replay.Headers.Location);
            Assert.Equal(first.Content.Headers.ContentType, replay.Content.Headers.ContentType);
            Assert.Equal(await first.Content.ReadAsByteArrayAsync(), await replay.Content.ReadAsByteArrayAsync());
            Assert.Equal("false", Assert.Single(first.Headers.GetValues("Idempotency-Replayed")));
            Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
            Assert.True(await completed.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(await completed.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Empty(failures);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private sealed class HeaderStore : IIdempotencyStore
    {
        private IdempotencyEntry? _entry;

        public Task<IdempotencyEntry?> FindAsync(string scopeHash, string keyHash, int lockTimeoutSeconds, CancellationToken cancellationToken)
            => Task.FromResult(_entry);

        public Task<IIdempotencySession> BeginAsync(string scopeHash, string keyHash, string fingerprint, int lockTimeoutSeconds, CancellationToken cancellationToken)
            => Task.FromResult<IIdempotencySession>(new Session(this, fingerprint));

        public Task<int> DeleteExpiredAsync(int batchSize, int lockTimeoutSeconds, CancellationToken cancellationToken)
            => Task.FromResult(0);

        private sealed class Session : IIdempotencySession
        {
            private readonly HeaderStore _store;
            private readonly string _fingerprint;

            public Session(HeaderStore store, string fingerprint)
            {
                _store = store;
                _fingerprint = fingerprint;
            }

            public IdempotencyEntry? Existing => null;

            public Task CompleteAsync(IdempotencyResponse response, TimeSpan lifetime, CancellationToken cancellationToken)
            {
                _store._entry = new IdempotencyEntry(_fingerprint, response);
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}

[ApiController]
[Route("idempotency-header-test")]
public sealed class IdempotencyHeaderTestController : ControllerBase
{
    [HttpPost]
    [Idempotent]
    public IActionResult Create() => Created("/idempotency-header-test/created", new { id = 1 });

    [HttpPut]
    [Idempotent]
    public IActionResult Update() => NoContent();
}
