using CandidateAssessment.Infrastructure.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CandidateAssessment.IntegrationTests;

[Collection("Observability")]
public sealed class TelemetryDatabaseTests
{
    private static readonly string[] AllowedMetricTags = { "operation", "outcome" };
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SqliteCommandsReportSuccessAndFailureWithoutSqlOrParameters(bool asynchronous)
    {
        using var capture = new ObservabilityTests.TelemetryCapture();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TelemetryContext>()
            .UseSqlite(connection)
            .AddInterceptors(new TelemetryDbCommandInterceptor())
            .Options;
        await using var context = new TelemetryContext(options);
        if (asynchronous)
        {
            await context.Database.EnsureCreatedAsync();
            await context.Database.ExecuteSqlRawAsync("INSERT INTO Items (Id) VALUES (1)");
            _ = await context.Items.FromSqlRaw("SELECT Id FROM Items WHERE Id = {0}", 1).ToListAsync();
            await Assert.ThrowsAsync<SqliteException>(() =>
                context.Database.ExecuteSqlRawAsync("SELECT 'sensitive-sql-cpf-token' FROM missing_private_table"));
        }
        else
        {
            context.Database.EnsureCreated();
            context.Database.ExecuteSqlRaw("INSERT INTO Items (Id) VALUES (1)");
            _ = context.Items.FromSqlRaw("SELECT Id FROM Items WHERE Id = {0}", 1).ToList();
            Assert.Throws<SqliteException>(() =>
                context.Database.ExecuteSqlRaw("SELECT 'sensitive-sql-cpf-token' FROM missing_private_table"));
        }

        var spans = capture.Activities.Where(a => a.Source.Name == TelemetryDbCommandInterceptor.SourceName).ToArray();
        Assert.Contains(spans, a => a.DisplayName == "sqlite.reader");
        Assert.Contains(spans, a => a.DisplayName == "sqlite.nonquery");
        Assert.Contains(spans, a => a.DisplayName == "sqlite.scalar");
        Assert.Contains(spans, a => a.Status == System.Diagnostics.ActivityStatusCode.Error);
        Assert.Contains(capture.Measurements, m => m.Name == "candidate.db.command.error.count");
        Assert.All(spans, activity =>
        {
            var serialized = ObservabilityTests.SerializeActivity(activity);
            Assert.DoesNotContain("sensitive-sql", serialized);
            Assert.DoesNotContain("SELECT", serialized);
            Assert.DoesNotContain("Items", serialized);
            Assert.DoesNotContain("missing_private_table", serialized);
            Assert.Null(activity.StatusDescription);
            Assert.Empty(activity.Events);
        });
        Assert.All(capture.Measurements, m =>
            Assert.All(m.Tags.Keys, key => Assert.Contains(key, AllowedMetricTags)));
    }

    private sealed class TelemetryContext : DbContext
    {
        public TelemetryContext(DbContextOptions<TelemetryContext> options) : base(options)
        {
        }

        public DbSet<TelemetryItem> Items => Set<TelemetryItem>();
    }

    private sealed class TelemetryItem
    {
        public int Id { get; set; }
    }
}
