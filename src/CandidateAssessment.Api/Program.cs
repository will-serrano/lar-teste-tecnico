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

// Bootstrap a minimal Serilog logger so failures during host construction are
// also captured (and any startup errors are not silently swallowed by the
// default ILogger pipeline).
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Replace the host's default logger with the structured Serilog pipeline.
    // Reads level/sinks configuration from the "Serilog" section.
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

    // Promote ASP.NET Core's TraceIdentifier (or Activity.Id when present)
    // into every log event's properties, so logs and ProblemDetails share the
    // same correlation id without manual wiring.
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
        // Liveness probe: confirms the process is responsive, no dependency checks.
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

// Required for WebApplicationFactory<Program> in tests.
public partial class Program
{
}
