using System.ComponentModel.DataAnnotations;

namespace CandidateAssessment.Api.Configuration;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    [Required, StringLength(100, MinimumLength = 1)]
    public string ServiceName { get; set; } = "CandidateAssessment.Api";

    [Range(0, 1)]
    public double SamplingRatio { get; set; } = 1;

    public bool OtlpEnabled { get; set; }

    public string OtlpEndpoint { get; set; } = "http://localhost:4317";

    public string OtlpProtocol { get; set; } = "grpc";

    [Range(100, 30000)]
    public int ExportTimeoutMilliseconds { get; set; } = 5000;

    [Range(1000, 60000)]
    public int MetricExportIntervalMilliseconds { get; set; } = 15000;

    [Range(1, 8192)]
    public int MaxQueueSize { get; set; } = 2048;

    [Range(1, 1024)]
    public int MaxExportBatchSize { get; set; } = 512;

    public bool HasValidExporterConfiguration()
    {
        return !double.IsNaN(SamplingRatio)
            && MaxExportBatchSize <= MaxQueueSize
            && ExportTimeoutMilliseconds <= MetricExportIntervalMilliseconds
            && OtlpProtocol is "grpc" or "http/protobuf"
            && Uri.TryCreate(OtlpEndpoint, UriKind.Absolute, out var endpoint)
            && endpoint.Scheme is "http" or "https"
            && string.IsNullOrEmpty(endpoint.UserInfo)
            && string.IsNullOrEmpty(endpoint.Query)
            && string.IsNullOrEmpty(endpoint.Fragment)
            && (OtlpProtocol != "grpc" || endpoint.AbsolutePath == "/");
    }
}
