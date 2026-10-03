using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CandidateAssessment.Api.Serialization;

/// <summary>
/// Conversor de <see cref="DateOnly"/> para System.Text.Json.
/// O .NET 6 não oferece suporte nativo a DateOnly; este conversor aceita e emite
/// datas no formato de calendário ISO-8601 ("yyyy-MM-dd").
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

        // Alternativa para permitir análise mais flexível (por exemplo, "1990-5-1").
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            return parsed;
        }

        throw new JsonException($"Invalid DateOnly value: '{value}'. Expected '{DateFormat}'.");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(DateFormat, CultureInfo.InvariantCulture));
}
