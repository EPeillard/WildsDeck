using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using WildsDeck.Core;

namespace WildsDeck.Bridge;

public sealed class TelemetryHub(BridgeOptions options, ILogger<TelemetryHub> logger)
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();
    private WildsState? _latest;
    private string? _lastErrorCode;

    public int ClientCount => _clients.Count;

    public async Task AcceptAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        Guid id = Guid.NewGuid();
        _clients[id] = socket;
        logger.LogInformation("Stream Deck client connected ({Count} total)", ClientCount);

        await SendAsync(socket, new ProtocolEnvelope<HelloData>
        {
            Type = "hello",
            Data = new HelloData(typeof(TelemetryHub).Assembly.GetName().Version?.ToString() ?? "0.1.0", Math.Max(1, 1000 / options.PollIntervalMs), $"ws://127.0.0.1:{options.WebSocketPort}/ws")
        }, cancellationToken);

        if (_latest is not null)
            await SendAsync(socket, StateEnvelope(_latest), cancellationToken);

        byte[] buffer = new byte[512];
        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException)
        {
        }
        finally
        {
            _clients.TryRemove(id, out _);

            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    using CancellationTokenSource closeTimeout = new(TimeSpan.FromMilliseconds(250));
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "WildsDeck closing", closeTimeout.Token);
                }
                catch (OperationCanceledException)
                {
                    socket.Abort();
                }
                catch (WebSocketException)
                {
                    socket.Abort();
                }
            }

            socket.Dispose();
            logger.LogInformation("Stream Deck client disconnected ({Count} total)", ClientCount);
        }
    }

    public async Task PublishStateAsync(WildsState state, CancellationToken cancellationToken)
    {
        _latest = state;
        await BroadcastAsync(StateEnvelope(state), cancellationToken);
        if (state.Error is not null && state.Error.Code != _lastErrorCode)
            await BroadcastAsync(new ProtocolEnvelope<TelemetryError> { Type = "error", Data = state.Error }, cancellationToken);
        _lastErrorCode = state.Error?.Code;
    }

    public Task PublishModeChangedAsync(GameMode previous, GameMode current, CancellationToken cancellationToken) =>
        BroadcastAsync(new ProtocolEnvelope<ModeChangedData>
        {
            Type = "modeChanged",
            Data = new ModeChangedData(previous, current, DateTimeOffset.UtcNow)
        }, cancellationToken);

    public static ProtocolEnvelope<WildsState> StateEnvelope(WildsState state) => new() { Type = "state", Data = state };

    private async Task BroadcastAsync<T>(ProtocolEnvelope<T> message, CancellationToken cancellationToken)
    {
        foreach ((Guid id, WebSocket socket) in _clients)
        {
            try
            {
                await SendAsync(socket, message, cancellationToken);
            }
            catch (Exception exception) when (exception is WebSocketException or ObjectDisposedException)
            {
                _clients.TryRemove(id, out _);
            }
        }
    }

    private static Task SendAsync<T>(WebSocket socket, ProtocolEnvelope<T> message, CancellationToken cancellationToken)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, TelemetryJson.Options);
        return socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellationToken);
    }
}
