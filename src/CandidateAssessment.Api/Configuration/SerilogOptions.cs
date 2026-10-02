namespace CandidateAssessment.Api.Configuration;

/// <summary>
/// Strongly typed configuration for Serilog sinks and enrichment.
/// Bound from configuration section <c>Serilog</c>.
/// </summary>
public sealed class SerilogOptions
{
    public const string SectionName = "Serilog";

    /// <summary>
    /// Minimum level applied to the root logger.
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Override the minimum level for the given logger namespaces.
    /// </summary>
    public Dictionary<string, string> Overrides { get; set; } = [];

    /// <summary>
    /// When true, writes structured events to the console.
    /// </summary>
    public bool WriteToConsole { get; set; } = true;

    /// <summary>
    /// When true, writes rolling daily log files to the local file system.
    /// </summary>
    public bool WriteToFile { get; set; } = true;

    /// <summary>
    /// Folder where rolling log files are written. Relative paths resolve from
    /// the application's content root. Ignored when <see cref="WriteToFile"/> is false.
    /// </summary>
    public string FilePath { get; set; } = "Logs/app-.log";

    /// <summary>
    /// How many days of rolling log files to retain. Ignored when <see cref="WriteToFile"/> is false.
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 14;
}
