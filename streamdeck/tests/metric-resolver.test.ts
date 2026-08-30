import { describe, expect, it } from "vitest";
import { compactNumber, formatPercent, resolveMetric } from "../src/metrics/metric-resolver.js";
import type { ConnectionSnapshot } from "../src/telemetry.js";

describe("metric resolver", () => {
  it("renders bridge offline without throwing", () => {
    expect(resolveMetric({ bridgeConnected: false }, { metric: "monster.hp" })).toMatchObject({
      label: "WILDS",
      value: "OFFLINE",
      tone: "inactive"
    });
  });

  it("renders a known health percentage", () => {
    const snapshot: ConnectionSnapshot = {
      bridgeConnected: true,
      state: {
        connected: true,
        mode: "hunt",
        timestamp: new Date().toISOString(),
        monster: { health: { current: 620, max: 1000, percent: 62 } }
      }
    };
    expect(resolveMetric(snapshot, { metric: "monster.hp" })).toMatchObject({ value: "62%", percent: 62, style: "gauge" });
  });

  it("ranks active ailments before higher inactive buildup", () => {
    const snapshot: ConnectionSnapshot = {
      bridgeConnected: true,
      state: {
        connected: true,
        mode: "hunt",
        timestamp: new Date().toISOString(),
        monster: {
          ailments: [
            { id: "poison", name: "Poison", active: false, current: 90, max: 100, percent: 90 },
            { id: "stun", name: "Stun", active: true, current: 30, max: 100, percent: 30 },
            { id: "sleep", name: "Sleep", active: false, current: 70, max: 100, percent: 70 }
          ]
        }
      }
    };

    expect(resolveMetric(snapshot, { metric: "monster.ailment.primary" })).toMatchObject({
      label: "AILMENT",
      value: "Stun",
      detail: "ACTIVE",
      tone: "danger"
    });
    expect(resolveMetric(snapshot, { metric: "monster.ailment.secondary" })).toMatchObject({
      label: "NEXT",
      value: "Poison",
      detail: "90%",
      percent: 90,
      style: "gauge"
    });
  });

  it("shows NONE when no ailment has useful state", () => {
    const snapshot: ConnectionSnapshot = {
      bridgeConnected: true,
      state: {
        connected: true,
        mode: "hunt",
        timestamp: new Date().toISOString(),
        monster: { ailments: [] }
      }
    };
    expect(resolveMetric(snapshot, { metric: "monster.ailment.primary" })).toMatchObject({
      label: "AILMENT",
      value: "NONE",
      tone: "inactive"
    });
  });

  it("ranks dynamic parts and disambiguates duplicate Blangonga heads", () => {
    const snapshot: ConnectionSnapshot = {
      bridgeConnected: true,
      state: {
        connected: true,
        mode: "hunt",
        timestamp: new Date().toISOString(),
        monster: {
          id: 7,
          parts: [
            { id: "0", name: "Part 1", type: "flinch", current: 60, max: 100, percent: 60 },
            { id: "7", name: "Part 8", type: "breakable", breakable: true, current: 35, max: 100, percent: 35, breakCount: 0, maxBreaks: 2 },
            { id: "8", name: "Part 9", type: "severable", severable: true, current: 45, max: 100, percent: 45, breakCount: 1, maxBreaks: 2 }
          ]
        }
      }
    };

    expect(resolveMetric(snapshot, { metric: "monster.part.primary" })).toMatchObject({
      label: "PART 1",
      value: "Head 3",
      detail: "45% · SEVERABLE · 1/2",
      percent: 45,
      tone: "danger"
    });
    expect(resolveMetric(snapshot, { metric: "monster.part.secondary" })).toMatchObject({
      label: "PART 2",
      value: "Head 2",
      detail: "35% · BREAKABLE · 0/2",
      percent: 35
    });
  });

  it("renders a named material collector as slots used out of 16", () => {
    const snapshot: ConnectionSnapshot = {
      bridgeConnected: true,
      state: {
        connected: true,
        mode: "town",
        timestamp: new Date().toISOString(),
        town: {
          materialCollectors: [
            { id: "rysher", name: "Rysher", current: 6, max: 16, percent: 37.5 }
          ]
        }
      }
    };
    expect(resolveMetric(snapshot, { metric: "town.material.rysher" })).toMatchObject({
      label: "Rysher",
      value: "6/16",
      percent: 37.5,
      style: "gauge",
      tone: "good"
    });
  });

  it("does not turn an unknown capture state into false", () => {
    const snapshot: ConnectionSnapshot = {
      bridgeConnected: true,
      state: { connected: true, mode: "hunt", timestamp: new Date().toISOString(), monster: {} }
    };
    expect(resolveMetric(snapshot, { metric: "monster.capture" }).value).toBe("—");
  });

  it("shows a missing map diagnostic", () => {
    const snapshot: ConnectionSnapshot = {
      bridgeConnected: true,
      state: {
        connected: false,
        gameVersion: "1.43.0.0",
        mode: "unknown",
        timestamp: new Date().toISOString(),
        error: { code: "mapMissing", message: "missing", requiredMapFile: "MonsterHunterWilds.1.43.0.0.map" }
      }
    };
    expect(resolveMetric(snapshot, { metric: "system.status" })).toMatchObject({ label: "MAP", value: "MISSING", detail: "1.43" });
  });
});

describe("formatting", () => {
  it("formats percentages and compact numbers", () => {
    expect(formatPercent(61.6)).toBe("62%");
    expect(compactNumber(15_500)).toBe("15.5K");
  });
});
