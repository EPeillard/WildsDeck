using System.Globalization;
using System.Text.RegularExpressions;

namespace WildsDeck.Memory;

public sealed class AddressMap
{
    private static readonly Regex LinePattern = new(
        @"^(?<kind>Address|Offset)\s+(?<name>[^\s=]+)\s*(?:=\s*)?(?<value>[^#]+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly Dictionary<string, long> _addresses;
    private readonly Dictionary<string, int[]> _offsets;

    private AddressMap(Dictionary<string, long> addresses, Dictionary<string, int[]> offsets)
    {
        _addresses = addresses;
        _offsets = offsets;
    }

    public static AddressMap Parse(string text)
    {
        var addresses = new Dictionary<string, long>(StringComparer.Ordinal);
        var offsets = new Dictionary<string, int[]>(StringComparer.Ordinal);

        foreach (string rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            Match match = LinePattern.Match(line);
            if (!match.Success)
                throw new FormatException($"Invalid address map line: '{rawLine}'.");

            string kind = match.Groups["kind"].Value;
            string name = match.Groups["name"].Value;
            string value = match.Groups["value"].Value.Trim();

            if (kind.Equals("Address", StringComparison.OrdinalIgnoreCase))
            {
                if (!addresses.TryAdd(name, ParseHex(value)))
                    throw new FormatException($"Duplicate address symbol '{name}'.");
            }
            else
            {
                int[] parsed = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(static token => checked((int)ParseHex(token)))
                    .ToArray();
                if (parsed.Length == 0)
                    throw new FormatException($"Offset symbol '{name}' has no values.");
                if (!offsets.TryAdd(name, parsed))
                    throw new FormatException($"Duplicate offset symbol '{name}'.");
            }
        }

        return new AddressMap(addresses, offsets);
    }

    public static AddressMap Load(string path) => Parse(File.ReadAllText(path));

    public long GetAddress(string name) => _addresses.TryGetValue(name, out long value)
        ? value
        : throw new KeyNotFoundException($"Address map symbol '{name}' was not found.");

    public int[] GetOffsets(string name) => _offsets.TryGetValue(name, out int[]? value)
        ? [.. value]
        : throw new KeyNotFoundException($"Address map symbol '{name}' was not found.");

    public bool HasAddress(string name) => _addresses.ContainsKey(name);
    public bool HasOffsets(string name) => _offsets.ContainsKey(name);

    public static long ParseHex(string value)
    {
        string normalized = value.Trim();
        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[2..];

        return long.Parse(normalized, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
    }
}

