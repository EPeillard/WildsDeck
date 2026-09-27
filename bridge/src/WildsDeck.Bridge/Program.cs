using System.Diagnostics;
using WildsDeck.Bridge;
using WildsDeck.Memory;

BridgeOptions options = BridgeOptions.Load(args);

if (args.Contains("--diagnose-hunt", StringComparer.OrdinalIgnoreCase))
{
    WildsAttachResult attach = WildsProcess.TryAttach(options.ProcessName, options.MapDirectory);
    if (attach.Process is null)
    {
        Console.Error.WriteLine(attach.ErrorMessage ?? "Could not attach to Monster Hunter Wilds.");
        Environment.ExitCode = 1;
        return;
    }

    using WildsProcess process = attach.Process;
    Console.WriteLine($"WildsDeck Hunt Diagnostics - Wilds {process.Version}");
    Console.WriteLine($"Map: {Path.GetFileName(process.MapPath)}");
    Console.WriteLine();
    foreach (string line in HuntDiagnostics.Probe(process))
        Console.WriteLine(line);
    return;
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://127.0.0.1:{options.WebSocketPort}");
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(console =>
{
    console.TimestampFormat = "HH:mm:ss ";
    console.SingleLine = true;
});

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<TelemetryHub>();
builder.Services.AddSingleton<ITelemetrySource>(services => options.MockMode == MockMode.None
    ? new RealTelemetrySource(options, services.GetRequiredService<ILogger<RealTelemetrySource>>())
    : new MockTelemetrySource(options.MockMode));
builder.Services.AddHostedService<StatePump>();

WebApplication app = builder.Build();
TelemetryHub telemetryHub = app.Services.GetRequiredService<TelemetryHub>();
app.Lifetime.ApplicationStopping.Register(telemetryHub.AbortAll);

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
app.MapGet("/health", (TelemetryHub hub) => Results.Ok(new { status = "ok", clients = hub.ClientCount }));
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    TelemetryHub hub = context.RequestServices.GetRequiredService<TelemetryHub>();
    await hub.AcceptAsync(await context.WebSockets.AcceptWebSocketAsync(), context.RequestAborted);
});

Console.WriteLine("WildsDeck Bridge");
Console.WriteLine("----------------");
Console.WriteLine(options.MockMode == MockMode.None ? "Waiting for MonsterHunterWilds.exe..." : $"Mock mode: {options.MockMode.ToString().ToUpperInvariant()}");
Console.WriteLine($"WebSocket: ws://127.0.0.1:{options.WebSocketPort}/ws");
Console.WriteLine($"Maps: {options.MapDirectory}");
if (options.ParentPid is int parentPid)
    Console.WriteLine($"Parent process: {parentPid}");
Console.WriteLine();

if (options.ParentPid is int monitoredParentPid)
    _ = MonitorParentProcessAsync(monitoredParentPid, app);

await app.RunAsync();

static async Task MonitorParentProcessAsync(int parentPid, WebApplication app)
{
    if (parentPid == Environment.ProcessId)
        return;

    try
    {
        using Process parent = Process.GetProcessById(parentPid);
        await parent.WaitForExitAsync();
        app.Logger.LogInformation("Parent process {ParentPid} exited; stopping bridge.", parentPid);
    }
    catch (ArgumentException)
    {
        app.Logger.LogInformation("Parent process {ParentPid} is not running; stopping bridge.", parentPid);
    }
    catch (InvalidOperationException)
    {
        app.Logger.LogInformation("Parent process {ParentPid} could not be monitored; stopping bridge.", parentPid);
    }
    finally
    {
        app.Lifetime.StopApplication();
    }
}

public partial class Program;
