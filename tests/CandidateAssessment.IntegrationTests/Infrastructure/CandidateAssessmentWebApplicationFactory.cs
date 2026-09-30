using System.Data.Common;
using CandidateAssessment.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
        _schemaEnsured = true;
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
