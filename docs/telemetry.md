# Telemetry protocol

The bridge accepts WebSocket clients only on loopback. Every message has an explicit protocol version:

```json
{
  "protocolVersion": 1,
  "type": "state",
  "data": {
    "connected": true,
    "gameVersion": "1.42.0.2",
    "mode": "hunt",
    "timestamp": "2026-08-30T12:00:00Z",
    "mock": false,
    "monster": {
      "id": 23,
      "name": "Rey Dau",
      "selection": "cameraTarget",
      "health": { "current": 7440, "max": 12000, "percent": 62 },
      "captureReady": false
    }
  }
}
```

Message types are:

| Type | Data | Purpose |
|---|---|---|
| `hello` | bridge version, rate, endpoint | Sent once on connection |
| `state` | `WildsState` | Snapshot at the configured polling rate |
| `modeChanged` | previous/current/timestamp | Immediate after debounce stabilizes |
| `error` | code/message/required map | Edge-triggered diagnostic |

Nullable fields are omitted by the bridge. Absence means unknown or unsupported; it never means zero or false. The top-level `connected` field refers to the game process, not the WebSocket. Thus the plugin distinguishes bridge offline, bridge online/game closed, unsupported map, and normal telemetry.

## Support status

| Field group | Status | Notes |
|---|---|---|
| Quest mode | Supported | HunterPie quest validity/state semantics |
| Monster ID/name/HP | Supported | Large-monster validation and encrypted floats |
| Camera selection | Supported | Falls back to first valid large monster |
| Enrage/stamina/capture | Supported | HunterPie ailment/build-up and threshold structures |
| Parts/ailments | Experimental | Generic collections; part labels may be numeric |
| Player name/weapon/attack/affinity | Supported | Direct player contexts/encrypted status |
| Damage/share | Experimental | Remote synchronized damage names are unresolved |
| HR/Support Ship/Ingredients | Supported | Direct save/activity contexts |
| Material Retrieval | Experimental | Aggregated collector item slots |
| Town NPC alerts | Unsupported | Null in real mode; present only in mock mode |

