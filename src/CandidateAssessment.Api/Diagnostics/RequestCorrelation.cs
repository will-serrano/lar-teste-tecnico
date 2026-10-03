using System.Diagnostics;

namespace CandidateAssessment.Api.Diagnostics;

public static class RequestCorrelation
{
    private static readonly object Key = new();

    public static void Capture(HttpContext context)
        => context.Items[Key] = Activity.Current?.Id ?? context.TraceIdentifier;

    public static string GetId(HttpContext context)
        => context.Items.TryGetValue(Key, out var value) && value is string id
            ? id
            : Activity.Current?.Id ?? context.TraceIdentifier;
}
