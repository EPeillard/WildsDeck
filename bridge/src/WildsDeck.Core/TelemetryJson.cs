using System.Text.Json;
using System.Text.Json.Serialization;

namespace WildsDeck.Core;

public static class TelemetryJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

public sealed record ProtocolEnvelope<T>
{
    public int ProtocolVersion { get; init; } = 1;
    public required string Type { get; init; }
    public required T Data { get; init; }
}

public sealed record HelloData(string BridgeVersion, int StateRateHz, string Endpoint);
public sealed record ModeChangedData(GameMode Previous, GameMode Current, DateTimeOffset Timestamp);

