using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace CandidateAssessment.Api.Idempotency;

internal static class RequestFingerprint
{
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string? Create(HttpContext context, byte[] body)
    {
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        var expectsJson = action?.Parameters.Any(p => p.BindingInfo?.BindingSource == BindingSource.Body) == true;
        byte[] canonical = body;
        if (body.Length > 0)
        {
            try
            {
                var isUtf16 = MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var mediaType)
                    && string.Equals(mediaType.CharSet?.Trim('"'), "utf-16", StringComparison.OrdinalIgnoreCase);
                var encoding = isUtf16 ? (Encoding)new UnicodeEncoding(false, true, true) : new UTF8Encoding(false, true);
                var text = encoding.GetString(body);
                using var document = JsonDocument.Parse(text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text);
                using var output = new MemoryStream();
                using (var writer = new Utf8JsonWriter(output))
                {
                    WriteCanonical(writer, document.RootElement);
                }

                canonical = output.ToArray();
            }
            catch (JsonException) when (expectsJson)
            {
                return null;
            }
            catch (JsonException)
            {
                // Endpoints without a body argument retain their existing acceptance of arbitrary bodies.
            }
            catch (DecoderFallbackException) when (expectsJson)
            {
                return null;
            }
            catch (DecoderFallbackException)
            {
                // Non-JSON actions fingerprint the original bytes without changing model binding.
            }
        }

        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? action?.AttributeRouteInfo?.Template;
        var routeValues = context.Request.RouteValues.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new { x.Key, Value = Guid.TryParse(x.Value?.ToString(), out var id) ? id.ToString("D") : x.Value?.ToString() });
        var query = context.Request.Query.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new { x.Key, Values = x.Value.ToArray() });
        return Hash(JsonSerializer.Serialize(new
        {
            context.Request.Method,
            Route = route,
            RouteValues = routeValues,
            Query = query,
            Payload = Convert.ToBase64String(canonical),
        }));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
