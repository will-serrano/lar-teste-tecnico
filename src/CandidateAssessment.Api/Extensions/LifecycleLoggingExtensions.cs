namespace CandidateAssessment.Api.Extensions;

public static class LifecycleLoggingExtensions
{
    public static void LogApiStartup(this WebApplication app, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(logger);
        var environment = app.Environment.EnvironmentName;
        app.Lifetime.ApplicationStarted.Register(() =>
            logger.Information("Candidate Assessment API started on environment {Environment}", environment));
    }
}
