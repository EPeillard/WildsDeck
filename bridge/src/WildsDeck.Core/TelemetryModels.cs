using System.Text.Json.Serialization;

namespace WildsDeck.Core;

public sealed record WildsState
{
    public required bool Connected { get; init; }
    public string? GameVersion { get; init; }
    public required GameMode Mode { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public bool Mock { get; init; }
    public string? MapFile { get; init; }
    public TelemetryError? Error { get; init; }
    public PlayerState? Player { get; init; }
    public QuestState? Quest { get; init; }
    public MonsterState? Monster { get; init; }
    public IReadOnlyList<PartyMemberState> Party { get; init; } = [];
    public TownState? Town { get; init; }

    public static WildsState Disconnected(string? gameVersion = null, TelemetryError? error = null) => new()
    {
        Connected = false,
        GameVersion = gameVersion,
        Mode = GameMode.Unknown,
        Timestamp = DateTimeOffset.UtcNow,
        Error = error
    };
}

public sealed record TelemetryError(string Code, string Message, string? RequiredMapFile = null);

public sealed record QuestState
{
    public bool? Active { get; init; }
    public int? Id { get; init; }
    public float? ElapsedSeconds { get; init; }
    public float? MaxSeconds { get; init; }
    public int? SuccessState { get; init; }
    public int? FailureState { get; init; }
}

public sealed record PlayerState
{
    public string? Name { get; init; }
    public string? WeaponType { get; init; }
    public float? DamageTotal { get; init; }
    public float? DamagePartySharePercent { get; init; }
    public float? Attack { get; init; }
    public float? Affinity { get; init; }
}

public sealed record PartyMemberState
{
    public string? Name { get; init; }
    public string? WeaponType { get; init; }
    public float? Damage { get; init; }
    public float? DamageSharePercent { get; init; }
    public bool IsLocalPlayer { get; init; }
}

public sealed record MonsterState
{
    public int? Id { get; init; }
    public string? Name { get; init; }
    public string? Selection { get; init; }
    public GaugeState? Health { get; init; }
    public EnrageState? Enrage { get; init; }
    public GaugeState? Stamina { get; init; }
    public bool? CaptureReady { get; init; }
    public float? CaptureThreshold { get; init; }
    public IReadOnlyList<MonsterPartState> Parts { get; init; } = [];
    public IReadOnlyList<AilmentState> Ailments { get; init; } = [];
}

public sealed record GaugeState
{
    public float? Current { get; init; }
    public float? Max { get; init; }
    public float? Percent => TelemetryMath.Percentage(Current, Max);
}

public sealed record EnrageState
{
    public bool? Active { get; init; }
    public float? Value { get; init; }
    public float? Max { get; init; }
    public float? Timer { get; init; }
    public float? MaxTimer { get; init; }
    public float? Percent => TelemetryMath.Percentage(Value, Max);
}

public sealed record MonsterPartState
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public string? Type { get; init; }

    // Compatibility gauge: break/sever when available, otherwise flinch.
    public float? Current { get; init; }
    public float? Max { get; init; }
    public float? Percent => TelemetryMath.Percentage(Current, Max);

    // HunterPie exposes these independently. Every part has a flinch gauge; a
    // breakable or severable part additionally has the corresponding special gauge.
    public GaugeState? Flinch { get; init; }
    public GaugeState? Break { get; init; }
    public GaugeState? Sever { get; init; }

    public bool? Breakable { get; init; }
    public bool? Severable { get; init; }
    public bool? Broken { get; init; }
    public int? BreakCount { get; init; }
    public int? MaxBreaks { get; init; }
    public int? ResetCount { get; init; }
    public int? BreakMultiplier { get; init; }
}

public sealed record AilmentState
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public bool? Active { get; init; }
    public float? Current { get; init; }
    public float? Max { get; init; }
    public float? Percent => TelemetryMath.Percentage(Current, Max);
    public float? Timer { get; init; }
    public float? MaxTimer { get; init; }
}

public sealed record TownState
{
    public int? HunterRank { get; init; }
    public ActivityState? SupportShip { get; init; }
    public ActivityState? IngredientsCenter { get; init; }
    public IReadOnlyList<MaterialCollectorState> MaterialCollectors { get; init; } = [];

    // Kept for protocol/backward compatibility with older profiles. New Town profiles
    // use MaterialCollectors instead of this aggregate value.
    public ActivityState? MaterialRetrieval { get; init; }
    public bool? NpcNotification { get; init; }
    public IReadOnlyList<NpcState> Npcs { get; init; } = [];
}

public sealed record MaterialCollectorState
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Current { get; init; }
    public int Max { get; init; } = 16;
    public float? Percent => TelemetryMath.Percentage(Current, Max);
}

public sealed record ActivityState
{
    public bool? Available { get; init; }
    public string? Status { get; init; }
    public bool? Ready { get; init; }
    public float? Current { get; init; }
    public float? Max { get; init; }
    public float? Timer { get; init; }
    public SupportStatus Support { get; init; } = SupportStatus.Experimental;
}

public sealed record NpcState
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public bool? HasNotification { get; init; }
}

public static class TelemetryMath
{
    public static float? Percentage(float? current, float? maximum)
    {
        if (current is null || maximum is null || maximum <= 0 || !float.IsFinite(current.Value) || !float.IsFinite(maximum.Value))
            return null;

        return NormalizePercent((double)current.Value / maximum.Value * 100d);
    }

    public static float? Share(float? value, IEnumerable<float?> values)
    {
        if (value is null || !float.IsFinite(value.Value))
            return null;

        float total = values.Where(static item => item is >= 0 && float.IsFinite(item.Value)).Sum(static item => item!.Value);
        return total > 0 ? NormalizePercent((double)value.Value / total * 100d) : null;
    }

    private static float NormalizePercent(double value) =>
        MathF.Round(Math.Clamp((float)value, 0f, 100f), 4, MidpointRounding.AwayFromZero);
}
