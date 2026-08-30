using System.Text.Json;

namespace WildsDeck.Bridge;

public sealed record BridgeOptions
{
    public string ProcessName { get; init; } = "MonsterHunterWilds";
    public int PollIntervalMs { get; init; } = 150;
    public int ModeDebounceMs { get; init; } = 1000;
    public int WebSocketPort { get; init; } = 47653;
    public string MapDirectory { get; init; } = "maps";
    public MockMode MockMode { get; init; }

    public static BridgeOptions Load(string[] args)
    {
        string? configPath = ValueAfter(args, "--config");
        configPath ??= File.Exists("wildsdeck.json") ? "wildsdeck.json" : null;
        BridgeOptions options = configPath is null
            ? new BridgeOptions()
            : JsonSerializer.Deserialize<BridgeOptions>(File.ReadAllText(configPath), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new BridgeOptions();

        MockMode mockMode = args.Contains("--mock-town", StringComparer.OrdinalIgnoreCase)
            ? MockMode.Town
            : args.Contains("--mock-hunt", StringComparer.OrdinalIgnoreCase)
                ? MockMode.Hunt
                : args.Contains("--mock", StringComparer.OrdinalIgnoreCase)
                    ? MockMode.Cycle
                    : options.MockMode;

        string? mapDirectory = ValueAfter(args, "--map-directory");
        string? portText = ValueAfter(args, "--port");
        int port = portText is not null && int.TryParse(portText, out int parsedPort) ? parsedPort : options.WebSocketPort;

        return options with
        {
            MockMode = mockMode,
            WebSocketPort = Math.Clamp(port, 1024, 65535),
            PollIntervalMs = Math.Clamp(options.PollIntervalMs, 50, 5000),
            ModeDebounceMs = Math.Clamp(options.ModeDebounceMs, 0, 10000),
            MapDirectory = ResolveMapDirectory(mapDirectory ?? options.MapDirectory)
        };
    }

    private static string ResolveMapDirectory(string configured)
    {
        if (Path.IsPathFullyQualified(configured))
            return configured;

        string fromCurrent = Path.GetFullPath(configured);
        if (Directory.Exists(fromCurrent))
            return fromCurrent;

        DirectoryInfo? cursor = new(AppContext.BaseDirectory);
        while (cursor is not null)
        {
            string candidate = Path.Combine(cursor.FullName, configured);
            if (Directory.Exists(candidate))
                return candidate;
            cursor = cursor.Parent;
        }

        return fromCurrent;
    }

    private static string? ValueAfter(string[] args, string name)
    {
        int index = Array.FindIndex(args, value => value.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}

public enum MockMode
{
    None,
    Cycle,
    Town,
    Hunt
}

