using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CandidateAssessment.Application.Abstractions.Authentication;
using CandidateAssessment.Application.Abstractions.Caching;
using CandidateAssessment.Application.Abstractions.Idempotency;
using CandidateAssessment.Domain.Roles;
using CandidateAssessment.Infrastructure.Persistence;
using CandidateAssessment.IntegrationTests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateAssessment.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class IdempotencyApiTests
{
    private static readonly string[] MultipleKeys = { "one", "two" };

    [Fact]
    public async Task Create_ShouldReplayCanonicalJsonAndLocation_AfterHostRestart()
    {
        using var file = new DatabaseFile();
        string body;
        Uri? location;
        using (var factory = new FileSqliteWebApplicationFactory(file.Path))
        {
            factory.EnsureDatabaseCreated();
            using var client = await factory.CreateAuthenticatedAdminClientAsync();
            using var first = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "create", PersonJson());
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            Assert.Equal("false", Assert.Single(first.Headers.GetValues("Idempotency-Replayed")));
            body = await first.Content.ReadAsStringAsync();
            location = first.Headers.Location;

            using var replay = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "create",
                "{ \"birthDate\":\"1990-01-01\", \"cpf\":\"52998224725\", \"name\":\"Idempotent Person\" }");
            Assert.Equal(body, await replay.Content.ReadAsStringAsync());
            Assert.Equal(location, replay.Headers.Location);
            Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
            using var scope = factory.Services.CreateScope();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Persons.CountAsync());
        }

        using var restarted = new FileSqliteWebApplicationFactory(file.Path);
        restarted.EnsureDatabaseCreated();
        using var newClient = await restarted.CreateAuthenticatedAdminClientAsync();
        using var afterRestart = await SendAsync(newClient, HttpMethod.Post, "/api/v1/persons", "create", PersonJson());
        Assert.Equal(HttpStatusCode.Created, afterRestart.StatusCode);
        Assert.Equal(body, await afterRestart.Content.ReadAsStringAsync());
        Assert.Equal(location, afterRestart.Headers.Location);
    }

    [Fact]
    public async Task AllSevenWrites_ShouldReplayWithoutRepeatingMutation()
    {
        using var file = new DatabaseFile();
        using var factory = new FileSqliteWebApplicationFactory(file.Path);
        factory.EnsureDatabaseCreated();
        using var client = await factory.CreateAuthenticatedAdminClientAsync();
        using var person = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "person", PersonJson());
        var id = await IdAsync(person);
        var personPath = $"/api/v1/persons/{id}";
        using var phone = await SendAsync(client, HttpMethod.Post, $"{personPath}/phones", "phone",
            "{\"type\":\"Mobile\",\"number\":\"11999999999\"}");
        var phoneBody = await phone.Content.ReadAsStringAsync();
        using var phoneReplay = await SendAsync(client, HttpMethod.Post, $"{personPath}/phones", "phone",
            "{\"number\":\"11999999999\",\"type\":\"Mobile\"}");
        Assert.Equal(phoneBody, await phoneReplay.Content.ReadAsStringAsync());
        var phoneId = await IdAsync(phone);
        var phonePath = $"{personPath}/phones/{phoneId}";

        await Repeat204Async(client, HttpMethod.Put, personPath, "person-update", "{\"name\":\"Updated\",\"birthDate\":\"1990-01-01\"}");
        await Repeat204Async(client, HttpMethod.Put, phonePath, "phone-update", "{\"type\":\"Mobile\",\"number\":\"11888888888\"}");
        var snapshot = await client.GetStringAsync(personPath);
        factory.Clock.UtcNow = factory.Clock.UtcNow.AddHours(1);
        using var updateReplay = await SendAsync(client, HttpMethod.Put, personPath, "person-update", "{\"name\":\"Updated\",\"birthDate\":\"1990-01-01\"}");
        Assert.Equal(HttpStatusCode.NoContent, updateReplay.StatusCode);
        Assert.Equal(snapshot, await client.GetStringAsync(personPath));
        await Repeat204Async(client, HttpMethod.Delete, phonePath, "phone-delete");
        await Repeat204Async(client, HttpMethod.Delete, personPath, "person-delete");
        await Repeat204Async(client, HttpMethod.Post, $"{personPath}/restore", "restore");

        using var createReplay = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "person", PersonJson());
        Assert.Equal(HttpStatusCode.Created, createReplay.StatusCode);
        Assert.Equal(id, await IdAsync(createReplay));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(7, await db.IdempotencyRecords.CountAsync());
        Assert.Equal(0, await db.Phones.CountAsync());
    }

    [Fact]
    public async Task ReuseWithDifferentInputOrOperation_ShouldConflict()
    {
        using var file = new DatabaseFile();
        using var factory = new FileSqliteWebApplicationFactory(file.Path);
        factory.EnsureDatabaseCreated();
        using var client = await factory.CreateAuthenticatedAdminClientAsync();
        using var created = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "shared", PersonJson());
        var id = await IdAsync(created);
        using var changed = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "shared", PersonJson("Other"));
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using var problem = JsonDocument.Parse(await changed.Content.ReadAsStringAsync());
        Assert.Equal("IdempotencyKeyReuse", problem.RootElement.GetProperty("code").GetString());
        Assert.NotEmpty(problem.RootElement.GetProperty("traceId").GetString()!);
        using var otherOperation = await SendAsync(client, HttpMethod.Delete, $"/api/v1/persons/{id}", "shared");
        Assert.Equal(HttpStatusCode.Conflict, otherOperation.StatusCode);
        using var anotherResource = await SendAsync(client, HttpMethod.Delete, $"/api/v1/persons/{Guid.NewGuid()}", "shared");
        Assert.Equal(HttpStatusCode.Conflict, anotherResource.StatusCode);
    }

    [Fact]
    public async Task SameKey_ShouldBeScopedByUser_AndNeverBypassAuthorization()
    {
        using var file = new DatabaseFile();
        using var factory = new FileSqliteWebApplicationFactory(file.Path);
        factory.EnsureDatabaseCreated();
        using var client = await factory.CreateAuthenticatedAdminClientAsync();
        using var created = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "scoped", PersonJson());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var second = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var token = scope.ServiceProvider.GetRequiredService<ITokenService>().IssueToken(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "another-admin"), new Claim(ClaimTypes.Role, ApplicationRoles.Admin),
            });
            second.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }

        using var other = await SendAsync(second, HttpMethod.Post, "/api/v1/persons", "scoped", PersonJson(cpf: "11144477735"));
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
        using var user = await factory.CreateAuthenticatedUserClientAsync();
        using var forbidden = await SendAsync(user, HttpMethod.Post, "/api/v1/persons", "scoped", PersonJson());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var anonymous = factory.CreateClient();
        using var unauthorized = await SendAsync(anonymous, HttpMethod.Post, "/api/v1/persons", "scoped", PersonJson());
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
    }

    [Theory]
    [InlineData("spaces not allowed")]
    [InlineData("a,b")]
    public async Task InvalidKey_ShouldReturn400WithoutWriting(string key)
    {
        using var file = new DatabaseFile();
        using var factory = new FileSqliteWebApplicationFactory(file.Path);
        factory.EnsureDatabaseCreated();
        using var client = await factory.CreateAuthenticatedAdminClientAsync();
        using var response = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", key, PersonJson());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().IdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task MultipleOrOverlongKeys_ShouldReturn400_AndUtf16JsonShouldReplay()
    {
        using var file = new DatabaseFile();
        using var factory = new FileSqliteWebApplicationFactory(file.Path);
        factory.EnsureDatabaseCreated();
        using var client = await factory.CreateAuthenticatedAdminClientAsync();
        using var multiple = new HttpRequestMessage(HttpMethod.Post, "/api/v1/persons")
        {
            Content = new StringContent(PersonJson(), Encoding.UTF8, "application/json"),
        };
        multiple.Headers.TryAddWithoutValidation("Idempotency-Key", MultipleKeys);
        using var rejected = await client.SendAsync(multiple);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var tooLong = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", new string('a', 129), PersonJson());
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        using var first = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "encoding", PersonJson());
        using var utf16 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/persons")
        {
            Content = new StringContent(PersonJson(), Encoding.Unicode, "application/json"),
        };
        utf16.Headers.Add("Idempotency-Key", "encoding");
        utf16.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json; charset=\"utf-16\"");
        using var replay = await client.SendAsync(utf16);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
    }

    [Fact]
    public async Task OversizedRequest_ShouldReturn413_ButNotLimitRequestsWithoutKey()
    {
        using var file = new DatabaseFile();
        using var factory = new FileSqliteWebApplicationFactory(file.Path) { MaxRequestBytes = 8 };
        factory.EnsureDatabaseCreated();
        using var client = await factory.CreateAuthenticatedAdminClientAsync();
        using var tooLarge = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "size", PersonJson());
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
        using var withoutKey = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", null, PersonJson());
        Assert.Equal(HttpStatusCode.Created, withoutKey.StatusCode);
    }

    [Fact]
    public async Task InvalidJsonAndBusinessFailures_ShouldNotConsumeKey()
    {
        using var file = new DatabaseFile();
        using var factory = new FileSqliteWebApplicationFactory(file.Path);
        factory.EnsureDatabaseCreated();
        using var client = await factory.CreateAuthenticatedAdminClientAsync();
        using var invalid = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "retry", "{");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var conflict = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "retry", PersonJson(cpf: "00000000000"));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var corrected = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "retry", PersonJson());
        Assert.Equal(HttpStatusCode.Created, corrected.StatusCode);
    }

    [Theory]
    [InlineData("completion")]
    [InlineData("serialization")]
    [InlineData("response-limit")]
    public async Task FailureBeforeCommit_ShouldRollbackResourceKeyAndCache(string failure)
    {
        using var file = new DatabaseFile();
        using var factory = new FileSqliteWebApplicationFactory(file.Path)
        {
            MaxResponseBytes = failure == "response-limit" ? 1 : 1024 * 1024,
        };
        factory.EnsureDatabaseCreated();
        using var client = await factory.CreateAuthenticatedAdminClientAsync();
        factory.Failure = failure;
        using var failed = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "fault", PersonJson());
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.Persons.ToListAsync());
        Assert.Empty(await db.IdempotencyRecords.ToListAsync());
        factory.Failure = null;
        if (failure != "response-limit")
        {
            using var retried = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "fault", PersonJson());
            Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
        }
    }

    [Fact]
    public async Task FailureAfterCommit_ShouldPreserveDurableReplay()
    {
        using var file = new DatabaseFile();
        using var factory = new FileSqliteWebApplicationFactory(file.Path);
        factory.EnsureDatabaseCreated();
        using var client = await factory.CreateAuthenticatedAdminClientAsync();
        factory.Failure = "after-commit";
        using var failedDelivery = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "committed", PersonJson());
        Assert.Equal(HttpStatusCode.InternalServerError, failedDelivery.StatusCode);
        factory.Failure = null;
        using var replay = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "committed", PersonJson());
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Persons.CountAsync());
    }

    [Fact]
    public async Task Expiry_ShouldPermitReuseAtExactBoundary_AndCleanupOnlyExpiredRecords()
    {
        using var file = new DatabaseFile();
        using var factory = new FileSqliteWebApplicationFactory(file.Path);
        factory.EnsureDatabaseCreated();
        using var client = await factory.CreateAuthenticatedAdminClientAsync();
        using var first = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "expires", PersonJson());
        var firstId = await IdAsync(first);
        factory.Clock.UtcNow = factory.Clock.UtcNow.AddHours(24).AddTicks(-1);
        using var before = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "expires", PersonJson());
        Assert.Equal(firstId, await IdAsync(before));
        factory.Clock.UtcNow = factory.Clock.UtcNow.AddTicks(1);
        using var after = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "expires", PersonJson(cpf: "11144477735"));
        Assert.Equal(HttpStatusCode.Created, after.StatusCode);
        Assert.NotEqual(firstId, await IdAsync(after));
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        Assert.Equal(0, await store.DeleteExpiredAsync(100, 2, CancellationToken.None));
        factory.Clock.UtcNow = factory.Clock.UtcNow.AddHours(24);
        Assert.Equal(1, await store.DeleteExpiredAsync(100, 2, CancellationToken.None));
    }

    [Fact]
    public async Task ConcurrentRequests_WithIndependentConnections_ShouldMutateOnce()
    {
        using var file = new DatabaseFile();
        using var factory = new FileSqliteWebApplicationFactory(file.Path);
        factory.EnsureDatabaseCreated();
        using var client = await factory.CreateAuthenticatedAdminClientAsync();
        var responses = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => SendAsync(client, HttpMethod.Post, "/api/v1/persons", "concurrent", PersonJson())));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
            var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
            Assert.All(bodies, body => Assert.Equal(bodies[0], body));
            Assert.Equal(1, responses.Count(r => r.Headers.GetValues("Idempotency-Replayed").Single() == "false"));
            using var scope = factory.Services.CreateScope();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Persons.CountAsync());
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task BusyDatabase_ShouldReturn503WithinConfiguredWait_AndAllowRetry()
    {
        using var file = new DatabaseFile();
        using var factory = new FileSqliteWebApplicationFactory(file.Path);
        factory.EnsureDatabaseCreated();
        using var client = await factory.CreateAuthenticatedAdminClientAsync();
        await using var connection = new SqliteConnection($"Data Source={file.Path}");
        await connection.OpenAsync();
        using (var held = connection.BeginTransaction(deferred: false))
        {
            var timer = Stopwatch.StartNew();
            using var busy = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "busy", PersonJson());
            Assert.Equal(HttpStatusCode.ServiceUnavailable, busy.StatusCode);
            Assert.Equal(TimeSpan.FromSeconds(2), busy.Headers.RetryAfter?.Delta);
            // One second of scheduler tolerance, not the provider's default 30-second wait.
            Assert.InRange(timer.Elapsed.TotalSeconds, 1.8, 3.5);
        }

        using var retry = await SendAsync(client, HttpMethod.Post, "/api/v1/persons", "busy", PersonJson());
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    [Fact]
    public async Task UpgradeMigration_ShouldPreserveExistingBusinessData()
    {
        using var file = new DatabaseFile();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite($"Data Source={file.Path}").Options;
        await using var db = new ApplicationDbContext(options);
        var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        await migrator.MigrateAsync("20260930225754_InitialCreate");
        var person = CandidateAssessment.Domain.Entities.Person.Create(
            "Existing", CandidateAssessment.Domain.ValueObjects.Cpf.Create("52998224725"),
            new DateOnly(1990, 1, 1), DateTime.UtcNow);
        db.Persons.Add(person);
        await db.SaveChangesAsync();
        await migrator.MigrateAsync();
        Assert.Equal(person.Id, (await db.Persons.SingleAsync()).Id);
        Assert.Empty(await db.IdempotencyRecords.ToListAsync());
    }

    private static async Task Repeat204Async(HttpClient client, HttpMethod method, string path, string key, string? body = null)
    {
        using var first = await SendAsync(client, method, path, key, body);
        using var second = await SendAsync(client, method, path, key, body);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Empty(await second.Content.ReadAsByteArrayAsync());
        Assert.Equal("true", Assert.Single(second.Headers.GetValues("Idempotency-Replayed")));
    }

    private static string PersonJson(string name = "Idempotent Person", string cpf = "52998224725")
        => JsonSerializer.Serialize(new { name, cpf, birthDate = "1990-01-01" });

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? key, string? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (key is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        }

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request);
    }

    private sealed class DatabaseFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"candidate-idempotency-{Guid.NewGuid():N}.db");

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                File.Delete(Path + suffix);
            }
        }
    }
}
