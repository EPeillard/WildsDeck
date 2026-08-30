# Third-party notices

## HunterPie

WildsDeck uses HunterPie as the technical reference for Monster Hunter Wilds memory structures and includes HunterPie address-map data.

- Project: [HunterPie](https://github.com/HunterPie/HunterPie)
- Reference commit: [`ef654889658684848cb465176b676b9b553ea102`](https://github.com/HunterPie/HunterPie/commit/ef654889658684848cb465176b676b9b553ea102)
- Copyright: HunterPie contributors
- License: Apache License 2.0; see [third_party/HunterPie-LICENSE](third_party/HunterPie-LICENSE)

The following WildsDeck material is substantially derived from or informed by HunterPie:

- `maps/MonsterHunterWilds.*.map` is copied from `HunterPie/Address/` without changing its values.
- `WildsCrypto` reimplements the encrypted-float decoding algorithm from `MHWildsCryptoService` and the portable AES final-round fallback from `ManualAesCrypto`.
- `WildsTelemetryReader` reimplements the minimum required pointer traversal and data layouts from the Wilds integration classes listed in `docs/hunterpie-reference.md`.
- Monster and ailment identifier labels in `WildsKnowledgeBase` are derived from HunterPie's `Localization` and `MonsterData.xml` data.

WildsDeck does not copy HunterPie's UI, process access layer, scanner, dependency injection framework, or widget architecture. In particular, WildsDeck deliberately does not carry over HunterPie's write/injection-capable memory APIs; it exposes read operations only.

## Elgato Stream Deck SDK

The plugin depends on the official [`@elgato/streamdeck`](https://www.npmjs.com/package/@elgato/streamdeck) package. Bundled profile archive structure and manifest conventions follow Elgato's official `lights-out` sample in [`elgatosf/streamdeck-plugin-samples`](https://github.com/elgatosf/streamdeck-plugin-samples/tree/main/lights-out). Dependencies retain their own licenses in `node_modules` when installed.

