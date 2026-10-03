using System.Data.Common;
using System.Net.Http.Json;
using CandidateAssessment.Infrastructure.Authentication;
using CandidateAssessment.Infrastructure.Diagnostics;
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
/// WebApplicationFactory que substitui o banco SQLite de produção por uma conexão
/// SQLite em memória isolada, mantida durante todo o ciclo de vida da fábrica.
/// A conexão permanece aberta para preservar o banco em memória.
/// O esquema é criado por <see cref="DatabaseFacade.EnsureCreated"/> durante
/// <see cref="EnsureDatabaseCreated"/> (chamado uma vez por fixture de classe de teste).
/// </summary>
public class CandidateAssessmentWebApplicationFactory : WebApplicationFactory<Program>
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
                ["Serilog:MinimumLevel"] = "Warning",
                ["Serilog:WriteToConsole"] = "false",
                ["Serilog:WriteToFile"] = "false",
                // Limite permissivo para que a suíte existente nunca o ultrapasse. O
                // teste dedicado de limitação de taxa inicia sua própria fábrica com
                // uma cota restrita e isola o contador em memória.
                ["IpRateLimiting:GeneralRules:0:Endpoint"] = "*",
                ["IpRateLimiting:GeneralRules:0:Period"] = "1m",
                ["IpRateLimiting:GeneralRules:0:Limit"] = "10000",
            });
        });

        builder.ConfigureServices(services =>
        {
            RemoveDbContextRegistrations(services);
            AddTestDbContext(services);
        });
    }

    /// <summary>
    /// Força a inicialização do host e garante que o esquema SQLite seja criado.
    /// Chamadas seguintes não fazem nada.
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
        EnsureSchema(dbContext);

        // Cria os dados iniciais do Identity logo após a criação do esquema. O
        // IdentitySeedMiddleware, executado sob demanda em tempo de execução, também
        // faria isso, mas o executamos explicitamente para que os testes seguintes
        // (que não passam pelo middleware antes do login) encontrem os usuários iniciais.
        IdentityUserSeeder.SeedAsync(scope.ServiceProvider).GetAwaiter().GetResult();

        _schemaEnsured = true;
    }

    /// <summary>
    /// Retorna um HttpClient novo com o cabeçalho Authorization do usuário Admin configurado.
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
    /// Retorna um HttpClient novo com o cabeçalho Authorization do usuário com papel User configurado.
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

        public IReadOnlyList<string> Roles { get; init; } = [];
    }

    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        services.RemoveAll<DbContextOptions>();
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.RemoveAll<ApplicationDbContext>();

        // Remove todos os registros internos de configuração de opções do EF Core para o contexto.
        // A interface pertence a Microsoft.EntityFrameworkCore.Infrastructure.
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

    protected virtual void EnsureSchema(ApplicationDbContext dbContext) => dbContext.Database.EnsureCreated();

    protected virtual void AddTestDbContext(IServiceCollection services)
    {
        var keepAliveConnection = new SqliteConnection(InMemoryConnectionString);
        keepAliveConnection.Open();

        Connection = keepAliveConnection;

        services.AddSingleton<DbConnection>(keepAliveConnection);

        services.AddDbContext<ApplicationDbContext>(
            (sp, options) =>
            {
                var connection = sp.GetRequiredService<DbConnection>();
                options.UseSqlite(connection)
                    .AddInterceptors(sp.GetRequiredService<TelemetryDbCommandInterceptor>());
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
