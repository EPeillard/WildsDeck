# Stream Deck layouts

The source of truth is `streamdeck/profiles/*.layout.json`. `npm run profiles` deterministically generates one bundled archive, `WildsDeck.streamDeckProfile`, containing two pages in a fixed order:

- page `0`: Town
- page `1`: Hunt

The plugin calls `switchToProfile(deviceId, "WildsDeck", page)` whenever the stable game mode changes.

## Page 0 — Town

| Row | Key 1 | Key 2 | Key 3 | Key 4 | Key 5 |
|---|---|---|---|---|---|
| 1 | Rysher | Murtabak | Apar | Plumpeach | Sabar |
| 2 | Support Ship | Ingredients | HR | Player | Weapon |
| 3 | Attack | Affinity | Mode | Bridge | WildsDeck status |

## Page 1 — Hunt

| Row | Key 1 | Key 2 | Key 3 | Key 4 | Key 5 |
|---|---|---|---|---|---|
| 1 | Monster | HP | Rage | Stamina | Capture |
| 2 | Part 1 | Part 2 | Part 3 | Ailment | Next ailment |
| 3 | Damage | Share % | Party | Attack | Affinity |

Unavailable real values render `— / unavailable`; they are never replaced with invented data.

## Profile generation and fallback

The generator follows the exported profile structure used by Elgato's official bundled-profile examples: one `.sdProfile` root manifest references both page manifests. `streamdeck validate` validates the containing plugin and CI runs `unzip -t` on the generated archive. Elgato does not publish a profile-authoring CLI or a formal schema for the internal archive manifests.

If a future Stream Deck release rejects the generated archive:

1. Link the plugin with `scripts/install-plugin.ps1`.
2. Create a standard 5×3 profile named exactly `WildsDeck`.
3. Configure its first page from `town.layout.json`.
4. Add a second page and configure it from `hunt.layout.json`.
5. Export the profile and replace `WildsDeck.streamDeckProfile`.
6. Keep the manifest `Profiles` entry named `WildsDeck` and run `npm run validate`.

The profile is declared `Readonly = false`, so imported keys remain customizable.
