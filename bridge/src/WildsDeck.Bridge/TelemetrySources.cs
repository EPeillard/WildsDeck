using System.ComponentModel;
using System.Diagnostics;
using WildsDeck.Core;
using WildsDeck.Memory;

namespace WildsDeck.Bridge;

public interface ITelemetrySource : IDisposable
{
    WildsState Poll();
}

public sealed class MockTelemetrySource(MockMode mode) : ITelemetrySource
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public WildsState Poll()
    {
        double seconds = _clock.Elapsed.TotalSeconds;
        bool hunt = mode == MockMode.Hunt || mode == MockMode.Cycle && seconds % 50 >= 8 && seconds % 50 < 42;
        return hunt ? Hunt(seconds) : Town();
    }

    private static WildsState Town() => new()
    {
        Connected = true,
        GameVersion = "mock",
        MapFile = null,
        Mode = GameMode.Town,
        Timestamp = DateTimeOffset.UtcNow,
        Mock = true,
        Player = new PlayerState { Name = "Aster", WeaponType = "Insect Glaive", Attack = 342, Affinity = 15 },
        Town = new TownState
        {
            HunterRank = 123,
            SupportShip = new ActivityState { Available = true, Ready = true, Status = "In town", Support = SupportStatus.Supported },
            IngredientsCenter = new ActivityState { Available = true, Ready = false, Current = 7, Max = 10, Status = "7/10", Support = SupportStatus.Supported },
            MaterialCollectors =
            [
                new MaterialCollectorState { Id = "rysher", Name = "Rysher", Current = 6 },
                new MaterialCollectorState { Id = "murtabak", Name = "Murtabak", Current = 6 },
                new MaterialCollectorState { Id = "apar", Name = "Apar", Current = 6 },
                new MaterialCollectorState { Id = "plumpeach", Name = "Plumpeach", Current = 6 },
                new MaterialCollectorState { Id = "sabar", Name = "Sabar", Current = 6 }
            ],
            MaterialRetrieval = new ActivityState { Available = true, Ready = false, Current = 30, Max = 80, Status = "30/80", Support = SupportStatus.Experimental },
            NpcNotification = true,
            Npcs =
            [
                new NpcState { Id = "mock-gemma", Name = "Gemma", HasNotification = true },
                new NpcState { Id = "mock-alma", Name = "Alma", HasNotification = false }
            ]
        }
    };

    private static WildsState Hunt(double elapsed)
    {
        double phase = Math.Max(0, elapsed % 50 - 8);
        float percent = Math.Clamp(100f - (float)phase * 2.55f, 12f, 100f);
        float current = 12000f * percent / 100f;
        bool enraged = phase is >= 10 and < 22;
        float localDamage = Math.Max(0, (float)phase * 127f);
        float allyDamage = Math.Max(0, (float)phase * 73f);
        float total = localDamage + allyDamage;

        return new WildsState
        {
            Connected = true,
            GameVersion = "mock",
            Mode = GameMode.Hunt,
            Timestamp = DateTimeOffset.UtcNow,
            Mock = true,
            Quest = new QuestState { Active = true, Id = 1042, ElapsedSeconds = (float)phase, MaxSeconds = 3000 },
            Monster = new MonsterState
            {
                Id = 23,
                Name = "Rey Dau",
                Selection = "cameraTarget",
                Health = new GaugeState { Current = current, Max = 12000 },
                Enrage = new EnrageState { Active = enraged, Value = enraged ? 100 : 62, Max = 100, Timer = enraged ? 38 : 0, MaxTimer = 90 },
                Stamina = new GaugeState { Current = Math.Max(0, 800 - (float)phase * 9), Max = 800 },
                CaptureThreshold = 0.25f,
                CaptureReady = percent <= 25,
                Parts =
                [
                    new MonsterPartState { Id = "head", Name = "Head", Current = Math.Max(0, 2200 - (float)phase * 70), Max = 2200, Broken = phase > 31 },
                    new MonsterPartState { Id = "body", Name = "Body", Current = Math.Max(0, 3200 - (float)phase * 45), Max = 3200, Broken = false },
                    new MonsterPartState { Id = "tail", Name = "Tail", Current = Math.Max(0, 1800 - (float)phase * 55), Max = 1800, Broken = phase > 32, Severable = true }
                ],
                Ailments =
                [
                    new AilmentState { Id = "15", Name = "Stun", Active = false, Current = Math.Min(100, (float)phase * 4), Max = 100 },
                    new AilmentState { Id = "5", Name = "Paralysis", Active = phase is >= 26 and < 29, Current = Math.Min(100, (float)phase * 3), Max = 100 }
                ]
            },
            Player = new PlayerState
            {
                Name = "Aster",
                WeaponType = "Insect Glaive",
                DamageTotal = localDamage,
                DamagePartySharePercent = total > 0 ? localDamage / total * 100 : null,
                Attack = 342,
                Affinity = 15
            },
            Party =
            [
                new PartyMemberState { Name = "Aster", WeaponType = "Insect Glaive", Damage = localDamage, DamageSharePercent = total > 0 ? localDamage / total * 100 : null, IsLocalPlayer = true },
                new PartyMemberState { Name = "Nadia", WeaponType = "Hammer", Damage = allyDamage, DamageSharePercent = total > 0 ? allyDamage / total * 100 : null }
            ],
            Town = Town().Town
        };
    }

    public void Dispose() => _clock.Stop();
}

public sealed class RealTelemetrySource(BridgeOptions options, ILogger<RealTelemetrySource> logger) : ITelemetrySource
{
    private WildsProcess? _process;
    private WildsTelemetryReader? _reader;
    private string? _lastAttachError;

    public WildsState Poll()
    {
        if (_process?.HasExited == true)
        {
            logger.LogInformation("Monster Hunter Wilds exited; waiting for restart.");
            Detach();
        }

        if (_process is null)
        {
            WildsAttachResult attach = WildsProcess.TryAttach(options.ProcessName, options.MapDirectory);
            if (attach.Process is null)
            {
                LogAttachResultOnce(attach);
                return WildsState.Disconnected(attach.Version, attach.ErrorCode is null ? null : new TelemetryError(
                    attach.ErrorCode,
                    attach.ErrorMessage ?? "Could not attach to the game.",
                    attach.RequiredMapFile));
            }

            _process = attach.Process;
            _reader = new WildsTelemetryReader(_process);
            _lastAttachError = null;
            logger.LogInformation("Attached to MonsterHunterWilds.exe PID {Pid}", _process.ProcessId);
            logger.LogInformation("Detected version {Version}", _process.Version);
            logger.LogInformation("Loaded address map {Map}", Path.GetFileName(_process.MapPath));
        }

        try
        {
            return _reader!.ReadState();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidDataException or InvalidOperationException)
        {
            logger.LogWarning("Telemetry read failed: {Message}", exception.Message);
            if (_process.HasExited)
                Detach();
            return new WildsState
            {
                Connected = _process is not null,
                GameVersion = _process?.Version,
                MapFile = _process is null ? null : Path.GetFileName(_process.MapPath),
                Mode = GameMode.Unknown,
                Timestamp = DateTimeOffset.UtcNow,
                Error = new TelemetryError("memoryReadFailed", exception.Message)
            };
        }
    }

    private void LogAttachResultOnce(WildsAttachResult result)
    {
        string key = $"{result.ErrorCode}:{result.Version}:{result.RequiredMapFile}";
        if (_lastAttachError == key)
            return;
        _lastAttachError = key;

        if (result.IsNotRunning)
            logger.LogInformation("Waiting for MonsterHunterWilds.exe...");
        else if (result.ErrorCode == "mapMissing")
            logger.LogError("No address map for game version {Version}; add {Map}", result.Version, result.RequiredMapFile);
        else
            logger.LogWarning("Could not attach: {Message}", result.ErrorMessage);
    }

    private void Detach()
    {
        _reader = null;
        _process?.Dispose();
        _process = null;
    }

    public void Dispose() => Detach();
}
