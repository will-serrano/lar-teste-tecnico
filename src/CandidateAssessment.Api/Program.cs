using System.Diagnostics;
using CandidateAssessment.Api;
using CandidateAssessment.Api.Configuration;
using CandidateAssessment.Api.Extensions;
using CandidateAssessment.Api.Middleware;
using CandidateAssessment.Application;
using CandidateAssessment.Infrastructure;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

// Inicializa um logger Serilog mínimo para também capturar falhas durante a construção
// do host (evitando que erros de inicialização sejam ignorados silenciosamente pelo
// pipeline padrão de ILogger).
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Substitui o logger padrão do host pelo pipeline estruturado do Serilog.
    // Lê a configuração de níveis e destinos da seção "Serilog".
    builder.Host.UseSerilog((ctx, services, lc) =>
    {
        var section = ctx.Configuration.GetSection(SerilogOptions.SectionName);
        var options = section.Get<SerilogOptions>() ?? new SerilogOptions();

        lc.MinimumLevel.Is(Enum.TryParse<LogEventLevel>(options.MinimumLevel, true, out var lvl)
            ? lvl
            : LogEventLevel.Information)
          .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
          .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
          .Enrich.FromLogContext()
          .Enrich.WithProperty("Application", "CandidateAssessment.Api")
          .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName)
          .Enrich.WithProperty("MachineName", Environment.MachineName)
          .ReadFrom.Configuration(ctx.Configuration);

        if (options.WriteToConsole)
        {
            lc.WriteTo.Console(
                outputTemplate:
                    "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        }

        if (options.WriteToFile)
        {
            var path = options.FilePath;
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(builder.Environment.ContentRootPath, path);
            }

            lc.WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: path,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: options.RetainedFileCountLimit,
                shared: true);
        }
    });

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
        .AddPresentation(builder.Configuration);

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "v1"));
    }

    // Inclui o TraceIdentifier do ASP.NET Core (ou Activity.Id, quando disponível)
    // nas propriedades de cada evento de log, para que logs e ProblemDetails compartilhem
    // o mesmo identificador de correlação sem configuração manual.
    app.UseSerilogRequestLogging(opts => opts.EnrichDiagnosticContext = (diag, http) =>
        {
            var traceId = Activity.Current?.Id ?? http.TraceIdentifier;
            diag.Set("TraceId", traceId);
            diag.Set("RequestPath", http.Request.Path.Value);
            diag.Set("RequestMethod", http.Request.Method);
            diag.Set("ClientIp", http.Connection.RemoteIpAddress?.ToString());
            diag.Set("UserAgent", http.Request.Headers.UserAgent.ToString());
        });

    app.UseMiddleware<IdentitySeedMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseApiRateLimiting();

    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
    });
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        // Verificação de atividade: confirma que o processo está respondendo, sem verificar dependências.
        Predicate = _ => false,
    });

    await app.RunAsync();

    Log.Information("Candidate Assessment API started on environment {Environment}", app.Environment.EnvironmentName);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly during startup.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Necessário para usar WebApplicationFactory<Program> nos testes.
public partial class Program
{
}
