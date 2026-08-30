# Development

## Toolchain

- .NET 10 LTS with nullable references and warnings as errors
- Node.js 24, TypeScript 5.9, `@elgato/streamdeck` 2.1
- Stream Deck software 7.1+ and `@elgato/cli` 1.9

`scripts/build.ps1` restores/builds/tests .NET, installs locked npm dependencies, type-checks/tests/builds the plugin, regenerates the profiles, and validates the `.sdPlugin`.

## Bridge commands

```powershell
dotnet run --project bridge/src/WildsDeck.Bridge/WildsDeck.Bridge.csproj
dotnet run --project bridge/src/WildsDeck.Bridge/WildsDeck.Bridge.csproj -- --mock
dotnet run --project bridge/src/WildsDeck.Bridge/WildsDeck.Bridge.csproj -- --mock-town
dotnet run --project bridge/src/WildsDeck.Bridge/WildsDeck.Bridge.csproj -- --mock-hunt
```

`GET http://127.0.0.1:47653/health` reports bridge health and WebSocket client count.

## Plugin workflow

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

Use `npm run watch` after the first link. Plugin logs are written by Stream Deck under the plugin's normal log directory.

## Adding a map

1. Confirm the executable's exact `FileVersion` in bridge logs.
2. Obtain the matching, reviewed `MonsterHunterWilds.<version>.map` from HunterPie.
3. Copy it into `maps/` without modifying or renaming values.
4. Update `THIRD_PARTY_NOTICES.md`/the map list if the distributed set changes.
5. Test mock mode and then validate real reads on that exact game version.

Do not copy an older map under a new filename and do not infer offsets from neighboring versions.

