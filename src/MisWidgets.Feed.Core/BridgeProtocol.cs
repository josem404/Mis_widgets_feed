using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MisWidgets.Feed.Core;

public sealed record DiagnosticsPingRequest(Guid RequestId, DateTimeOffset SentAtUtc);

public sealed record BridgeParseResult(
    DiagnosticsPingRequest? Request,
    string? ErrorResponse)
{
    public bool IsSuccess => Request is not null;
}

/// <summary>
/// The complete version-one web/native protocol. It deliberately accepts one command only.
/// </summary>
public static class BridgeProtocol
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly HashSet<string> RequestProperties =
        new(StringComparer.Ordinal) { "version", "type", "requestId", "payload" };

    private static readonly HashSet<string> PingPayloadProperties =
        new(StringComparer.Ordinal) { "sentAtUtc" };

    public static BridgeParseResult ParseRequest(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Failure(null, "invalid_json", "El mensaje debe contener un objeto JSON.");
        }

        if (message.Length > FeedIdentifiers.MaximumMessageCharacters)
        {
            return Failure(null, "message_too_large", "El mensaje supera el limite de 8 KiB.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(message, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8,
            });

            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || HasUnknownProperties(root, RequestProperties))
            {
                return Failure(null, "invalid_request", "La solicitud contiene una forma no admitida.");
            }

            string? requestIdText = ReadString(root, "requestId");
            Guid? requestId = Guid.TryParseExact(requestIdText, "D", out Guid parsedRequestId)
                ? parsedRequestId
                : null;

            if (!root.TryGetProperty("version", out JsonElement versionElement) ||
                !versionElement.TryGetInt32(out int version) ||
                version != FeedIdentifiers.BridgeVersion)
            {
                return Failure(requestId, "unsupported_version", "La version del protocolo no es compatible.");
            }

            if (requestId is null)
            {
                return Failure(null, "invalid_request_id", "requestId debe ser un UUID canonico.");
            }

            string? type = ReadString(root, "type");
            if (!string.Equals(type, "diagnostics.ping", StringComparison.Ordinal))
            {
                return Failure(requestId, "unsupported_type", "El tipo de mensaje no esta permitido.");
            }

            if (!root.TryGetProperty("payload", out JsonElement payload) ||
                payload.ValueKind != JsonValueKind.Object ||
                HasUnknownProperties(payload, PingPayloadProperties))
            {
                return Failure(requestId, "invalid_payload", "El payload de diagnostics.ping no es valido.");
            }

            string? sentAtText = ReadString(payload, "sentAtUtc");
            if (!DateTimeOffset.TryParse(
                    sentAtText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset sentAtUtc) ||
                (!sentAtText.EndsWith('Z') && !HasExplicitOffset(sentAtText)))
            {
                return Failure(requestId, "invalid_payload", "sentAtUtc debe usar ISO-8601 con zona horaria.");
            }

            return new BridgeParseResult(
                new DiagnosticsPingRequest(requestId.Value, sentAtUtc),
                null);
        }
        catch (JsonException)
        {
            return Failure(null, "invalid_json", "El mensaje no es JSON valido.");
        }
    }

    public static string CreatePong(Guid requestId, DateTimeOffset providerUtc)
    {
        return JsonSerializer.Serialize(new
        {
            version = FeedIdentifiers.BridgeVersion,
            type = "diagnostics.pong",
            requestId = requestId.ToString("D"),
            ok = true,
            payload = new
            {
                providerUtc = providerUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            },
        }, SerializerOptions);
    }

    public static string CreateError(Guid? requestId, string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return JsonSerializer.Serialize(new
        {
            version = FeedIdentifiers.BridgeVersion,
            type = "diagnostics.error",
            requestId = requestId?.ToString("D"),
            ok = false,
            error = new { code, message },
        }, SerializerOptions);
    }

    private static BridgeParseResult Failure(Guid? requestId, string code, string message) =>
        new(null, CreateError(requestId, code, message));

    private static bool HasUnknownProperties(JsonElement element, HashSet<string> acceptedProperties)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!acceptedProperties.Contains(property.Name))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool HasExplicitOffset(string value)
    {
        int timeSeparator = value.IndexOf('T');
        if (timeSeparator < 0)
        {
            return false;
        }

        return value.IndexOf('+', timeSeparator) >= 0 || value.IndexOf('-', timeSeparator) >= 0;
    }
}
