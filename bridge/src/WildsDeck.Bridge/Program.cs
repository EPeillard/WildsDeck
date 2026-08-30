using WildsDeck.Bridge;

BridgeOptions options = BridgeOptions.Load(args);
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
Console.WriteLine();

await app.RunAsync();

public partial class Program;

