using CandidateAssessment.Infrastructure.Authentication;

namespace CandidateAssessment.Api.Middleware;

/// <summary>
/// Executa o seeder do Identity de forma tardia na primeira requisição, garantindo que o
/// esquema do banco já exista quando o seeder for executado. O acionamento por requisição
/// evita problemas de ordenação entre a inicialização do host e a migração/EnsureCreated.
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
/// Controle global ao processo para que o seeder seja executado apenas uma vez durante
/// o ciclo de vida da aplicação.
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
            // Se o esquema ainda não estiver pronto (primeira requisição em produção antes de
            // a migração terminar), permite que outra requisição tente novamente.
            Interlocked.Exchange(ref _seeded, 0);
            throw;
        }
    }
}
