# WildsDeck

[![CI](https://github.com/EPeillard/WildsDeck/actions/workflows/ci.yml/badge.svg)](https://github.com/EPeillard/WildsDeck/actions/workflows/ci.yml)

WildsDeck puts selected Monster Hunter Wilds telemetry on a classic 5×3 Elgato Stream Deck. It replaces screen-space overlay widgets with readable physical keys and automatically switches between two pages of one bundled, editable **WildsDeck** profile:

- **Page 0 — Town** for Support Ship, Ingredients Center, Material Retrieval, Hunter Rank, player status, and any available NPC state.
- **Page 1 — Hunt** for target monster, HP, rage, stamina, capture readiness, parts, ailments, damage, and party share.

The project is an early MVP. Use it at your own risk: game updates change memory layouts, and a matching address map is mandatory.

## Architecture

```text
MonsterHunterWilds.exe
        │  ReadProcessMemory only
        ▼
WildsDeck.Bridge (.NET 10, bundled Windows executable)
        │  ws://127.0.0.1:47653/ws
        ▼
WildsDeck Stream Deck plugin (TypeScript, Node 24)
        └── WildsDeck profile
            ├── Page 0: Town
            └── Page 1: Hunt
```

The bridge owns game/memory semantics. The plugin only consumes the stable versioned telemetry protocol and renders SVG key images. The plugin health-checks the local bridge, starts the bundled executable when needed, retries after bridge failures, and launches it with a parent-process guard so it exits when the plugin exits. See [architecture](docs/architecture.md) and [telemetry](docs/telemetry.md).

## Requirements

To run an already built/installed plugin:

- Windows 10 or later
- Elgato Stream Deck software 7.1 or later
- Standard Stream Deck, DeviceType `0`, 5 columns × 3 rows for the bundled profile
- Monster Hunter Wilds for real telemetry; it is not needed for mock mode

To build from source, also install:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 24 or later](https://nodejs.org/)

The packaged bridge is published self-contained for Windows x64, so running the built plugin does not require a separate .NET installation.

## Quick start

From PowerShell at the repository root:

```powershell
.\scripts\build.ps1
.\scripts\install-plugin.ps1 -SkipBuild
```

That is enough. `build.ps1` publishes the bridge, its memory maps, and `wildsdeck.json` into the plugin bundle. Once the plugin is loaded, Stream Deck starts the bridge automatically; no separate terminal or `dotnet run` command is required. The bridge can start before Monster Hunter Wilds and waits for the game process.

`install-plugin.ps1` uses the current Elgato CLI development workflow: it enables developer mode, links the `.sdPlugin` directory, bootstraps both bundled profiles through a short mock bridge session, and restarts the plugin. It does not require administrator privileges.

## Mock demo

The fastest end-to-end demonstration is:

```powershell
.\scripts\dev.ps1
```

This builds and links the plugin, then runs a repeating Town → Hunt → falling HP → rage → capture-ready → Town sequence using the same packaged bridge executable. Fixed modes are also available from source:

```powershell
dotnet run --project .\bridge\src\WildsDeck.Bridge\WildsDeck.Bridge.csproj -- --mock-town
dotnet run --project .\bridge\src\WildsDeck.Bridge\WildsDeck.Bridge.csproj -- --mock-hunt
```

Mock messages are explicitly marked with `"mock": true`. Fake values and NPC names exist only in mock mode.

## Configuration

The bridge loads `wildsdeck.json` from its working directory when present. Defaults are sufficient:

```json
{
  "processName": "MonsterHunterWilds",
  "pollIntervalMs": 150,
  "modeDebounceMs": 1000,
  "webSocketPort": 47653,
  "mapDirectory": "maps"
}
```

The server binds only to `127.0.0.1`. Use `--config <path>`, `--port <port>`, or `--map-directory <path>` for local overrides. The plugin additionally uses `--parent-pid` internally so an automatically launched bridge terminates with its Stream Deck plugin process.

## Memory maps

WildsDeck detects `MonsterHunterWilds.exe`'s file version and requires an exact:

```text
maps/MonsterHunterWilds.<version>.map
```

The repository includes HunterPie maps for `1.41.3.0`, `1.42.0.0`, `1.42.0.1`, and `1.42.0.2`. To support a later game patch, copy the exact new Wilds map from HunterPie's `HunterPie/Address/` directory; never rename an older map or guess offsets. A missing map is reported through telemetry and displayed as `MAP / MISSING` on status keys.

## Safety

WildsDeck's memory assembly exposes only:

- `OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION)`
- `ReadProcessMemory`
- `CloseHandle`

There is no memory write API, DLL injection, instruction patching, input simulation, save manipulation, or anti-cheat bypass. Closing or restarting the game is treated as a normal reconnect event.

## Bundled profile

`WildsDeck.streamDeckProfile` is generated from the two versioned 5×3 JSON page specs during `npm run build`, registered with `DeviceType = 0`, `Readonly = false`, and `AutoInstall = true`. Town is page 0 and Hunt is page 1; the plugin selects the page with the SDK's `switchToProfile(..., page)` parameter. Elgato's CLI validates the plugin and CI verifies the archive. See [layout documentation](docs/streamdeck-layouts.md) for the manual fallback procedure if a future Stream Deck release changes the undocumented archive internals.

## Known limitations

- Real hardware/game validation is still required for each game patch. Automated tests never attach to a game process.
- Party damage is read for local and synchronized remote slots, but remote player names/weapons are currently null.
- Monster part readings are experimental and currently use stable numeric labels (`Part 1`, etc.) when semantic localization is unavailable.
- Generic real NPC notifications are not implemented: HunterPie's current Wilds integration exposes NPC party members but no reliable town-notification model. The API remains extensible and real values remain null.
- Material Retrieval is an experimental aggregate. Support Ship, Ingredients Center, Hunter Rank, quest mode, HP, enrage, stamina, capture threshold, player name/weapon/status and local damage have direct HunterPie references.
- `Ingredients Center.ready` means its documented 10-slot counter is full; the raw count/timer are also exposed.
- The fallback target is the first valid large monster when the camera target cannot be matched.

See [HunterPie reference and confidence table](docs/hunterpie-reference.md) for exact source evidence.

## Development and tests

```powershell
dotnet test .\bridge\WildsDeck.Bridge.slnx
.\scripts\publish-bridge.ps1
cd .\streamdeck
npm ci
npm run check
npm test
npm run build
npm run validate
```

CI repeats these checks, including a Windows smoke test of the self-contained bridge package. More details are in [development.md](docs/development.md).

## License and attribution

WildsDeck is MIT licensed. HunterPie-derived logic and map data are used under Apache-2.0; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
