using System.Diagnostics;

namespace WildsDeck.Memory;

public sealed class WildsProcess : IDisposable
{
    private readonly Process _process;

    public int ProcessId => _process.Id;
    public string Version { get; }
    public string MapPath { get; }
    public ProcessMemoryReader Memory { get; }
    public AddressMap AddressMap { get; }
    public MemoryAddressResolver Resolver { get; }
    public bool HasExited => _process.HasExited;

    private WildsProcess(Process process, string version, string mapPath, ProcessMemoryReader memory, AddressMap addressMap, nint moduleBase)
    {
        _process = process;
        Version = version;
        MapPath = mapPath;
        Memory = memory;
        AddressMap = addressMap;
        Resolver = new MemoryAddressResolver(memory, addressMap, moduleBase);
    }

    public static WildsAttachResult TryAttach(string processName, string mapDirectory)
    {
        if (!OperatingSystem.IsWindows())
            return WildsAttachResult.Failed("platformUnsupported", "Real process telemetry is supported on Windows only.");

        Process? process = Process.GetProcessesByName(processName).OrderBy(static process => process.Id).FirstOrDefault();
        if (process is null)
            return WildsAttachResult.NotRunning();

        try
        {
            ProcessModule? module = process.MainModule;
            if (module is null)
                return WildsAttachResult.Failed("moduleUnavailable", "The game main module could not be inspected.");

            string? version = FileVersionInfo.GetVersionInfo(module.FileName).FileVersion;
            if (string.IsNullOrWhiteSpace(version))
                return WildsAttachResult.Failed("versionUnavailable", "The game executable version could not be detected.");

            string requiredName = $"MonsterHunterWilds.{version}.map";
            string mapPath = Path.Combine(Path.GetFullPath(mapDirectory), requiredName);
            if (!File.Exists(mapPath))
                return WildsAttachResult.MissingMap(version, requiredName);

            AddressMap addressMap = AddressMap.Load(mapPath);
            ProcessMemoryReader memory = ProcessMemoryReader.Open(process.Id);
            var attached = new WildsProcess(process, version, mapPath, memory, addressMap, module.BaseAddress);
            return WildsAttachResult.Success(attached);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            process.Dispose();
            return WildsAttachResult.Failed("attachFailed", exception.Message);
        }
    }

    public void Dispose()
    {
        Memory.Dispose();
        _process.Dispose();
    }
}

public sealed record WildsAttachResult
{
    public WildsProcess? Process { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Version { get; init; }
    public string? RequiredMapFile { get; init; }
    public bool IsNotRunning { get; init; }

    public static WildsAttachResult Success(WildsProcess process) => new() { Process = process, Version = process.Version };
    public static WildsAttachResult NotRunning() => new() { IsNotRunning = true };
    public static WildsAttachResult MissingMap(string version, string filename) => new()
    {
        ErrorCode = "mapMissing",
        ErrorMessage = $"No exact address map exists for Monster Hunter Wilds {version}.",
        Version = version,
        RequiredMapFile = filename
    };
    public static WildsAttachResult Failed(string code, string message) => new() { ErrorCode = code, ErrorMessage = message };
}
