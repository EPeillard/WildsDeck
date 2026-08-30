using System.Text.Json;
using WildsDeck.Core;

namespace WildsDeck.Tests;

public sealed class TelemetryTests
{
    [Fact]
    public void CalculatesHealthPercentage()
    {
        var health = new GaugeState { Current = 620, Max = 1000 };
        Assert.Equal(62, health.Percent);
    }

    [Fact]
    public void InvalidMaximumProducesUnknownPercentage()
    {
        Assert.Null(new GaugeState { Current = 100, Max = 0 }.Percent);
        Assert.Null(new GaugeState { Current = null, Max = 100 }.Percent);
    }

    [Fact]
    public void CalculatesDamageShareFromKnownValues()
    {
        Assert.Equal(60, TelemetryMath.Share(600, [600, 400]));
        Assert.Null(TelemetryMath.Share(null, [600, 400]));
        Assert.Null(TelemetryMath.Share(0, [0, null]));
    }

    [Fact]
    public void SerializationOmitsUnknownFieldsInsteadOfFabricatingZeroes()
    {
        var state = new WildsState
        {
            Connected = true,
            Mode = GameMode.Hunt,
            Timestamp = DateTimeOffset.Parse("2026-08-30T12:00:00Z"),
            Monster = new MonsterState { Id = 23, CaptureReady = null }
        };

        string json = JsonSerializer.Serialize(state, TelemetryJson.Options);
        Assert.Contains("\"mode\":\"hunt\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("captureReady", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"health\":", json, StringComparison.Ordinal);
    }

    [Fact]
    public void StateEnvelopeHasExplicitProtocolVersion()
    {
        var envelope = new ProtocolEnvelope<WildsState>
        {
            Type = "state",
            Data = WildsState.Disconnected()
        };
        string json = JsonSerializer.Serialize(envelope, TelemetryJson.Options);
        Assert.Contains("\"protocolVersion\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"state\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ModeChangedEnvelopeSerializesCamelCaseModes()
    {
        var envelope = new ProtocolEnvelope<ModeChangedData>
        {
            Type = "modeChanged",
            Data = new ModeChangedData(GameMode.Town, GameMode.Hunt, DateTimeOffset.UtcNow)
        };
        string json = JsonSerializer.Serialize(envelope, TelemetryJson.Options);
        Assert.Contains("\"previous\":\"town\"", json, StringComparison.Ordinal);
        Assert.Contains("\"current\":\"hunt\"", json, StringComparison.Ordinal);
    }
}

