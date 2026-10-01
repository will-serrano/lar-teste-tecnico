using CandidateAssessment.Infrastructure.Authentication;

namespace CandidateAssessment.Api.Middleware;

/// <summary>
/// Runs the Identity seeder lazily on the first request, ensuring the database
/// schema already exists when the seeder runs. Using a request-based trigger
/// avoids the ordering problem between host startup and migration/EnsureCreated.
/// </summary>
public sealed class IdentitySeedMiddleware
{
    private readonly RequestDelegate _next;

    public IdentitySeedMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await IdentitySeedGate.EnsureSeededAsync(context.RequestServices);
        await _next(context);
    }
}

/// <summary>
/// Process-wide gate so the seeder runs only once per application lifetime.
/// </summary>
public static class IdentitySeedGate
{
    private static int _seeded;

    public static async Task EnsureSeededAsync(IServiceProvider serviceProvider)
    {
        if (Interlocked.CompareExchange(ref _seeded, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await IdentityUserSeeder.SeedAsync(serviceProvider);
        }
        catch
        {
            // If the schema is not ready yet (very first request in production before
            // migration completes), allow another request to retry.
            Interlocked.Exchange(ref _seeded, 0);
            throw;
        }
    }
}
