using CandidateAssessment.Application.Abstractions.Idempotency;
using CandidateAssessment.Application.Abstractions.Time;
using CandidateAssessment.Infrastructure.Diagnostics;
using CandidateAssessment.Infrastructure.Idempotency;
using CandidateAssessment.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateAssessment.IntegrationTests.Infrastructure;

public sealed class FileSqliteWebApplicationFactory : CandidateAssessmentWebApplicationFactory
{
    public FileSqliteWebApplicationFactory(string databasePath, TestClock? clock = null)
    {
        DatabasePath = databasePath;
        Clock = clock ?? new TestClock();
    }

    public string DatabasePath { get; }

    public TestClock Clock { get; }

    public string? Failure { get; set; }

    public int MaxResponseBytes { get; set; } = 1024 * 1024;

    public int MaxRequestBytes { get; set; } = 1024 * 1024;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Idempotency:MaxResponseBytes"] = MaxResponseBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Idempotency:MaxRequestBytes"] = MaxRequestBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IDateTimeProvider>(Clock);
            services.AddScoped<IIdempotencyStore>(sp => new FailureStore(
                ActivatorUtilities.CreateInstance<SqliteIdempotencyStore>(sp), this));
            services.Configure<MvcOptions>(options => options.Filters.Add(new FailureFilter(this)));
        });
    }

    protected override void AddTestDbContext(IServiceCollection services)
        => services.AddDbContext<ApplicationDbContext>((sp, options) =>
            options.UseSqlite($"Data Source={DatabasePath};Default Timeout=2")
                .AddInterceptors(sp.GetRequiredService<TelemetryDbCommandInterceptor>()));

    protected override void EnsureSchema(ApplicationDbContext dbContext) => dbContext.Database.Migrate();

    public sealed class TestClock : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = new(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FailureFilter : IAsyncResultFilter
    {
        private readonly FileSqliteWebApplicationFactory _factory;

        public FailureFilter(FileSqliteWebApplicationFactory factory) => _factory = factory;

        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            await next();
            if (_factory.Failure == "serialization")
            {
                throw new InvalidOperationException("Injected failure after result serialization.");
            }
        }
    }

    private sealed class FailureStore : IIdempotencyStore
    {
        private readonly IIdempotencyStore _inner;
        private readonly FileSqliteWebApplicationFactory _factory;

        public FailureStore(IIdempotencyStore inner, FileSqliteWebApplicationFactory factory)
        {
            _inner = inner;
            _factory = factory;
        }

        public Task<IdempotencyEntry?> FindAsync(string scopeHash, string keyHash, int lockTimeoutSeconds, CancellationToken cancellationToken)
            => _inner.FindAsync(scopeHash, keyHash, lockTimeoutSeconds, cancellationToken);

        public async Task<IIdempotencySession> BeginAsync(string scopeHash, string keyHash, string fingerprint,
            int lockTimeoutSeconds, CancellationToken cancellationToken)
            => new FailureSession(await _inner.BeginAsync(scopeHash, keyHash, fingerprint, lockTimeoutSeconds, cancellationToken), _factory);

        public Task<int> DeleteExpiredAsync(int batchSize, int lockTimeoutSeconds, CancellationToken cancellationToken)
            => _inner.DeleteExpiredAsync(batchSize, lockTimeoutSeconds, cancellationToken);
    }

    private sealed class FailureSession : IIdempotencySession
    {
        private readonly IIdempotencySession _inner;
        private readonly FileSqliteWebApplicationFactory _factory;

        public FailureSession(IIdempotencySession inner, FileSqliteWebApplicationFactory factory)
        {
            _inner = inner;
            _factory = factory;
        }

        public IdempotencyEntry? Existing => _inner.Existing;

        public async Task CompleteAsync(IdempotencyResponse response, TimeSpan lifetime, CancellationToken cancellationToken)
        {
            if (_factory.Failure == "completion")
            {
                throw new InvalidOperationException("Injected failure before commit.");
            }

            await _inner.CompleteAsync(response, lifetime, cancellationToken);
            if (_factory.Failure == "after-commit")
            {
                throw new InvalidOperationException("Injected failure after commit, before response delivery.");
            }
        }

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
