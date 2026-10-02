using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CandidateAssessment.Api.Serialization;

/// <summary>
/// System.Text.Json converter for <see cref="DateOnly"/>.
/// .NET 6 ships without native DateOnly support; this converter accepts and emits
/// the ISO-8601 calendar date ("yyyy-MM-dd") format.
/// </summary>
public sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private const string DateFormat = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
        {
            return default;
        }

        if (DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed))
        {
            return parsed;
        }

        // Fallback for tolerant parsing (e.g. "1990-5-1").
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            return parsed;
        }

        throw new JsonException($"Invalid DateOnly value: '{value}'. Expected '{DateFormat}'.");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(DateFormat, CultureInfo.InvariantCulture));
}
