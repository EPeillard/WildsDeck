# Development

## Toolchain

- .NET 10 LTS with nullable references and warnings as errors
- Node.js 24, TypeScript 5.9, `@elgato/streamdeck` 2.1
- Stream Deck software 7.1+ and `@elgato/cli` 1.9

`scripts/build.ps1` restores/builds/tests .NET, publishes a self-contained Windows x64 bridge into the plugin bundle, installs locked npm dependencies, type-checks/tests/builds the plugin, regenerates the bundled two-page profile, and validates the `.sdPlugin`.

The bundled runtime is generated at:

```text
streamdeck/com.wildsdeck.streamdeck.sdPlugin/bin/bridge/
```

It contains `WildsDeck.Bridge.exe`, `wildsdeck.json`, and the `maps/` directory. The directory is build output and remains gitignored.

## Bridge commands

Source/development commands remain available:

```powershell
dotnet run --project bridge/src/WildsDeck.Bridge/WildsDeck.Bridge.csproj
dotnet run --project bridge/src/WildsDeck.Bridge/WildsDeck.Bridge.csproj -- --mock
dotnet run --project bridge/src/WildsDeck.Bridge/WildsDeck.Bridge.csproj -- --mock-town
dotnet run --project bridge/src/WildsDeck.Bridge/WildsDeck.Bridge.csproj -- --mock-hunt
```

`GET http://127.0.0.1:47653/health` reports bridge health and WebSocket client count.

The Stream Deck plugin launches the packaged bridge with `--parent-pid <plugin pid>`. The bridge watches that process and shuts itself down when the plugin exits, preventing orphaned bridge instances from blocking the port after plugin reloads.

To publish only the packaged runtime:

```powershell
.\scripts\publish-bridge.ps1
```

## Plugin workflow

For a complete development link, prefer the root scripts:

```powershell
.\scripts\build.ps1
.\scripts\install-plugin.ps1 -SkipBuild
```

After that, Stream Deck starts the bundled bridge automatically. A manual bridge terminal is not required.

For TypeScript-only work:

```powershell
cd streamdeck
npm ci
npm run check
npm test
npm run build
npx streamdeck dev
npx streamdeck link .\com.wildsdeck.streamdeck.sdPlugin
npx streamdeck restart com.wildsdeck.streamdeck
```

A TypeScript-only build does not create `bin/bridge/`; run `scripts/publish-bridge.ps1` or the full root build before testing automatic startup.

Use `npm run watch` after the first link. Plugin logs are written by Stream Deck under the plugin's normal log directory.

`npm run profiles` generates `com.wildsdeck.streamdeck.sdPlugin/WildsDeck.streamDeckProfile`. Its page order is part of the runtime contract: Town is page 0 and Hunt is page 1.

## Mock development

`scripts/dev.ps1` starts the packaged bridge in mock mode before linking/restarting the plugin. Because the health endpoint is already available, the plugin reuses that process instead of starting a real-mode bridge.

If port 47653 is already occupied, close Stream Deck or stop the existing bridge before starting a mock session.

## Adding a map

1. Confirm the executable's exact `FileVersion` in bridge logs.
2. Obtain the matching, reviewed `MonsterHunterWilds.<version>.map` from HunterPie.
3. Copy it into `maps/` without modifying or renaming values.
4. Update `THIRD_PARTY_NOTICES.md`/the map list if the distributed set changes.
5. Test mock mode and then validate real reads on that exact game version.

Do not copy an older map under a new filename and do not infer offsets from neighboring versions.
