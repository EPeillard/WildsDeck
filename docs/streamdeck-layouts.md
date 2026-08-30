# Stream Deck layouts

The source of truth is `streamdeck/profiles/*.layout.json`. `npm run profiles` deterministically generates exactly two archives at the plugin root because the manifest's `Profiles[].Name` is both a relative archive path and the exact string accepted by `switchToProfile`.

## WildsDeck - Hunt

| Row | Key 1 | Key 2 | Key 3 | Key 4 | Key 5 |
|---|---|---|---|---|---|
| 1 | Monster | HP | Rage | Stamina | Capture |
| 2 | Head | Body | Tail | Ailment 1 | Ailment 2 |
| 3 | Damage | Share % | Party | Attack | Affinity |

## WildsDeck - Town

| Row | Key 1 | Key 2 | Key 3 | Key 4 | Key 5 |
|---|---|---|---|---|---|
| 1 | Support Ship | Ingredients | Material | NPC alert | HR |
| 2 | NPC 1 | NPC 2 | NPC 3 | Player | Weapon |
| 3 | Attack | Affinity | Mode | Bridge | WildsDeck status |

Unavailable real NPC values render `— / unavailable`; they are never replaced with invented names.

## Profile generation and fallback

The generator follows the exported profile structure used by Elgato's official `lights-out` SDK sample. `streamdeck validate` validates the containing plugin and CI runs `unzip -t` on both archives. Elgato does not publish a profile-authoring CLI or a formal schema for the internal archive manifests.

If a future Stream Deck release rejects the generated archives:

1. Link the plugin with `scripts/install-plugin.ps1`.
2. Create a standard 5×3 profile named exactly `WildsDeck - Town`.
3. Drag `Wilds Display` onto all keys and choose metrics from `town.layout.json` by coordinate.
4. Export the profile, replace `WildsDeck - Town.streamDeckProfile`, and repeat for Hunt.
5. Keep the two manifest `Profiles` entries unchanged and run `npm run validate`.

The profiles are declared `Readonly = false`, so imported keys remain customizable.

