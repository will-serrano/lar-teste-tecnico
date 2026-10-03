using CandidateAssessment.Api;
using CandidateAssessment.Api.Configuration;
using CandidateAssessment.Api.Diagnostics;
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
          .Enrich.WithProperty("MachineName", Environment.MachineName);

        foreach (var (source, level) in options.Overrides)
        {
            if (Enum.TryParse<LogEventLevel>(level, true, out var overrideLevel))
            {
                lc.MinimumLevel.Override(source, overrideLevel);
            }
        }

        var destinations = new LoggerConfiguration().MinimumLevel.Verbose();
        if (options.WriteToConsole)
        {
            if (options.ConsoleJson)
            {
                destinations.WriteTo.Console(new CompactJsonFormatter());
            }
            else
            {
                destinations.WriteTo.Console(
                    outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}");
            }
        }

        if (options.WriteToFile)
        {
            var path = options.FilePath;
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(builder.Environment.ContentRootPath, path);
            }

            destinations.WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: path,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: options.RetainedFileCountLimit,
                shared: true);
        }
        lc.WriteTo.Sink(new SanitizingLogSink(destinations.CreateLogger()));
    });

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
        .AddPresentation(builder.Configuration);

    var app = builder.Build();

    app.UseRouting();
    app.UseMiddleware<TelemetryCorrelationMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "v1"));
    }

    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate = "HTTP {RequestMethod} {Route} responded {StatusCode} in {Elapsed:0.0000} ms";
        opts.EnrichDiagnosticContext = (diag, http) =>
        {
            diag.Set("Route", ApiDiagnostics.Route(http));
            diag.Set("RequestMethod", ApiDiagnostics.Method(http.Request.Method));
        };
    });

    app.UseMiddleware<IdentitySeedMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseApiRateLimiting();
    app.UseMiddleware<IdempotencyMiddleware>();

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

    app.LogApiStartup(Log.Logger);
    await app.RunAsync();
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
