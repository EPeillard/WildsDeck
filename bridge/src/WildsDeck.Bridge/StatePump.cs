using WildsDeck.Core;

namespace WildsDeck.Bridge;

public sealed class StatePump(
    BridgeOptions options,
    ITelemetrySource source,
    TelemetryHub hub,
    ILogger<StatePump> logger) : BackgroundService
{
    private readonly ModeDebouncer _debouncer = new(TimeSpan.FromMilliseconds(options.ModeDebounceMs));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(options.PollIntervalMs));
        while (!stoppingToken.IsCancellationRequested)
        {
            WildsState raw = source.Poll();
            GameMode previous = _debouncer.Current;
            bool changed = raw.Connected && _debouncer.Observe(raw.Mode, raw.Timestamp, out previous);
            GameMode publishedMode = raw.Connected ? _debouncer.Current : GameMode.Unknown;
            WildsState state = raw with { Mode = publishedMode };

            if (changed)
            {
                logger.LogInformation("Game mode changed {Previous} -> {Current}", previous.ToString().ToUpperInvariant(), publishedMode.ToString().ToUpperInvariant());
                if (state.Monster?.Name is { } target && publishedMode == GameMode.Hunt)
                    logger.LogInformation("Target: {Target}", target);
                await hub.PublishModeChangedAsync(previous, publishedMode, stoppingToken);
            }

            await hub.PublishStateAsync(state, stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    public override void Dispose()
    {
        source.Dispose();
        base.Dispose();
    }
}
