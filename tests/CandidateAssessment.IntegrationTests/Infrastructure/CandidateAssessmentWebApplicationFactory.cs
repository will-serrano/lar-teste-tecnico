using System.Data.Common;
using System.Net.Http.Json;
using CandidateAssessment.Domain.Roles;
using CandidateAssessment.Infrastructure.Authentication;
using CandidateAssessment.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CandidateAssessment.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory that swaps the production SQLite database for an isolated
/// SQLite in-memory connection that lives for the duration of the factory.
/// The connection is kept open to preserve the in-memory database.
/// Schema is created via <see cref="DatabaseFacade.EnsureCreated"/> during
/// <see cref="EnsureDatabaseCreated"/> (called once per test class fixture).
/// </summary>
public sealed class CandidateAssessmentWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string InMemoryConnectionString = "Data Source=:memory:;";

    public const string TestSigningKey = "test-signing-key-with-at-least-32-bytes-of-length-for-hmacsha256";

    public const string TestIssuer = "TestIssuer";

    public const string TestAudience = "TestAudience";

    public DbConnection Connection { get; private set; } = default!;

    private bool _schemaEnsured;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = InMemoryConnectionString,
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["Jwt:SigningKey"] = TestSigningKey,
                ["Jwt:ExpiresInMinutes"] = "60",
                ["SeedUsers:Admin:Username"] = "admin",
                ["SeedUsers:Admin:Password"] = "Admin@123",
                ["SeedUsers:User:Username"] = "user",
                ["SeedUsers:User:Password"] = "User@123",
            });
        });

        builder.ConfigureServices(services =>
        {
            RemoveDbContextRegistrations(services);
            AddTestDbContext(services);
        });
    }

    /// <summary>
    /// Forces the host to materialize and ensures the SQLite schema is created.
    /// Subsequent calls are no-ops.
    /// </summary>
    public void EnsureDatabaseCreated()
    {
        if (_schemaEnsured)
        {
            return;
        }

        _ = Server;

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();

        // Seed Identity immediately after schema creation. The lazy IdentitySeedMiddleware
        // used at runtime would also handle this, but we run it explicitly here so that
        // any subsequent test (which doesn't go through middleware before login) finds
        // the seed users in place.
        IdentityUserSeeder.SeedAsync(scope.ServiceProvider).GetAwaiter().GetResult();

        _schemaEnsured = true;
    }

    /// <summary>
    /// Returns a fresh HttpClient with an Authorization header for the configured Admin user.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedAdminClientAsync()
    {
        var client = CreateClient();
        var token = await LoginAsync(client, "admin", "Admin@123");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Returns a fresh HttpClient with an Authorization header for the configured User role.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedUserClientAsync()
    {
        var client = CreateClient();
        var token = await LoginAsync(client, "user", "User@123");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username,
            password,
        });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<LoginResponsePayload>();
        return payload!.AccessToken;
    }

    private sealed class LoginResponsePayload
    {
        public string AccessToken { get; init; } = default!;

        public DateTime ExpiresAtUtc { get; init; }

        public string Username { get; init; } = default!;

        public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    }

    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        services.RemoveAll<DbContextOptions>();
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.RemoveAll<ApplicationDbContext>();

        // Remove any EF Core internal option configuration registrations for the context.
        // The interface lives in Microsoft.EntityFrameworkCore.Infrastructure.
        var internalDescriptors = services
            .Where(d => d.ServiceType.IsGenericType
                && d.ServiceType.FullName != null
                && d.ServiceType.FullName.StartsWith(
                    "Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration",
                    StringComparison.Ordinal))
            .ToList();

        foreach (var descriptor in internalDescriptors)
        {
            services.Remove(descriptor);
        }
    }

    private void AddTestDbContext(IServiceCollection services)
    {
        var keepAliveConnection = new SqliteConnection(InMemoryConnectionString);
        keepAliveConnection.Open();

        Connection = keepAliveConnection;

        services.AddSingleton<DbConnection>(keepAliveConnection);

        services.AddDbContext<ApplicationDbContext>(
            (sp, options) =>
            {
                var connection = sp.GetRequiredService<DbConnection>();
                options.UseSqlite(connection);
            });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Connection?.Dispose();
        }

        base.Dispose(disposing);
    }
}
