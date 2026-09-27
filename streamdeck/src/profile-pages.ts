import type { GameMode } from "./telemetry.js";

export const WILDSDECK_PROFILE = "WildsDeck";

const pages: Partial<Record<GameMode, number>> = {
  town: 0,
  hunt: 1
};

export function pageForMode(mode: GameMode): number | undefined {
  return pages[mode];
}
