import type { ActivityState, AilmentState, ConnectionSnapshot, MaterialCollectorState, MonsterPartState, WildsState } from "../telemetry.js";

export type DisplayStyle = "percentage" | "gauge" | "status" | "number" | "text" | "compact";
export type Tone = "neutral" | "good" | "warning" | "danger" | "inactive" | "error";

export interface MetricSettings {
  [key: string]: string | undefined;
  metric?: string;
  displayStyle?: DisplayStyle;
  label?: string;
  target?: string;
}

export interface MetricView {
  label: string;
  value: string;
  detail?: string;
  percent?: number;
  style: DisplayStyle;
  tone: Tone;
}

export function resolveMetric(snapshot: ConnectionSnapshot, settings: MetricSettings): MetricView {
  const metric = settings.metric ?? "system.status";
  if (!snapshot.bridgeConnected) return view(settings, "WILDS", "OFFLINE", "status", "inactive");

  const state = snapshot.state;
  if (!state) return view(settings, "WILDS", "CONNECTING", "status", "inactive");
  if (state.error?.code === "mapMissing") {
    const version = state.gameVersion?.split(".").slice(0, 2).join(".") ?? "?";
    return view(settings, "MAP", "MISSING", "status", "error", version);
  }
  if (!state.connected) return view(settings, "WILDS", "WAITING", "status", "inactive", "game closed");

  switch (metric) {
    case "system.status": return view(settings, "STATUS", state.mode.toUpperCase(), "status", state.mode === "unknown" ? "warning" : "good", state.mock ? "MOCK" : state.gameVersion);
    case "monster.name": return text(settings, "MONSTER", state.monster?.name);
    case "monster.hp": return gauge(settings, "HP", state.monster?.health?.percent, formatPair(state.monster?.health?.current, state.monster?.health?.max), "danger");
    case "monster.enrage": return state.monster?.enrage?.active === undefined
      ? unknown(settings, "RAGE")
      : view(settings, "RAGE", state.monster.enrage.active ? "ENRAGED" : "CALM", "status", state.monster.enrage.active ? "danger" : "good", formatPercent(state.monster.enrage.percent));
    case "monster.stamina": return gauge(settings, "STAMINA", state.monster?.stamina?.percent, formatPair(state.monster?.stamina?.current, state.monster?.stamina?.max), "warning");
    case "monster.capture": return state.monster?.captureReady === undefined
      ? unknown(settings, "CAPTURE")
      : view(settings, "CAPTURE", state.monster.captureReady ? "READY ✓" : "NOT YET", "status", state.monster.captureReady ? "good" : "neutral", formatPercent(state.monster?.health?.percent));
    case "monster.part.head": return part(settings, "HEAD", state, ["head"], 0);
    case "monster.part.body": return part(settings, "BODY", state, ["body", "torso"], 1);
    case "monster.part.tail": return part(settings, "TAIL", state, ["tail"], 2);
    case "monster.ailment.primary": return relevantAilment(settings, "AILMENT", state, 0);
    case "monster.ailment.secondary": return relevantAilment(settings, "NEXT", state, 1);
    case "monster.ailment.0": return ailment(settings, "STATUS 1", state.monster?.ailments?.[0]);
    case "monster.ailment.1": return ailment(settings, "STATUS 2", state.monster?.ailments?.[1]);
    case "player.name": return text(settings, "PLAYER", state.player?.name);
    case "player.weapon": return text(settings, "WEAPON", state.player?.weaponType);
    case "player.damage": return number(settings, "DAMAGE", state.player?.damageTotal);
    case "player.partyShare": return gauge(settings, "SHARE", state.player?.damagePartySharePercent, undefined, "good");
    case "player.attack": return number(settings, "ATTACK", state.player?.attack);
    case "player.affinity": return gauge(settings, "AFFINITY", state.player?.affinity, undefined, "good");
    case "party.summary": return view(settings, "PARTY", `${state.party?.length ?? 0} HUNTERS`, "compact", "neutral", compactNumber(sum(state.party?.map((member) => member.damage))));
    case "town.material.rysher": return materialCollector(settings, "Rysher", state, "rysher");
    case "town.material.murtabak": return materialCollector(settings, "Murtabak", state, "murtabak");
    case "town.material.apar": return materialCollector(settings, "Apar", state, "apar");
    case "town.material.plumpeach": return materialCollector(settings, "Plumpeach", state, "plumpeach");
    case "town.material.sabar": return materialCollector(settings, "Sabar", state, "sabar");
    case "town.supportShip": return activity(settings, "SHIP", state.town?.supportShip);
    case "town.ingredientsCenter": return activity(settings, "FOOD / ING", state.town?.ingredientsCenter);
    case "town.materialRetrieval": return activity(settings, "MATERIAL", state.town?.materialRetrieval);
    case "town.npcNotification": return state.town?.npcNotification === undefined
      ? unknown(settings, "NPC ALERT")
      : view(settings, "NPC ALERT", state.town.npcNotification ? "READY ✓" : "CLEAR", "status", state.town.npcNotification ? "warning" : "good");
    case "town.hunterRank": return number(settings, "HUNTER RANK", state.town?.hunterRank, "HR");
    case "town.npc.0": return npc(settings, "NPC 1", state, 0);
    case "town.npc.1": return npc(settings, "NPC 2", state, 1);
    case "town.npc.2": return npc(settings, "NPC 3", state, 2);
    default: return view(settings, "METRIC", "UNKNOWN", "status", "error", metric);
  }
}

function materialCollector(settings: MetricSettings, label: string, state: WildsState, id: string): MetricView {
  const collector = state.town?.materialCollectors?.find((item) => item.id.toLowerCase() === id);
  if (!collector) return unknown(settings, label);
  return materialCollectorView(settings, collector);
}

function materialCollectorView(settings: MetricSettings, collector: MaterialCollectorState): MetricView {
  if (!Number.isFinite(collector.current) || !Number.isFinite(collector.max) || collector.max <= 0)
    return unknown(settings, collector.name);

  const percent = Number.isFinite(collector.percent)
    ? collector.percent
    : Math.max(0, Math.min(100, collector.current / collector.max * 100));
  const tone: Tone = collector.current >= collector.max ? "warning" : "good";
  return view(settings, collector.name, `${collector.current}/${collector.max}`, "gauge", tone, undefined, percent);
}

function activity(settings: MetricSettings, label: string, value?: ActivityState): MetricView {
  if (!value?.available) return unknown(settings, label);
  if (value.ready !== undefined) return view(settings, label, value.ready ? "READY ✓" : value.status ?? "PENDING", "status", value.ready ? "good" : "neutral", value.status);
  return text(settings, label, value.status);
}

function part(settings: MetricSettings, label: string, state: WildsState, names: string[], fallback: number): MetricView {
  const parts = state.monster?.parts ?? [];
  const found = parts.find((item) => names.some((name) => item.name?.toLowerCase().includes(name))) ?? parts[fallback];
  if (!found) return unknown(settings, label);
  return partGauge(settings, label, found);
}

function partGauge(settings: MetricSettings, label: string, value: MonsterPartState): MetricView {
  if (value.broken) return view(settings, label, "BROKEN", "status", "good", value.name);
  return gauge(settings, label, value.percent, value.name, "warning");
}

function relevantAilment(settings: MetricSettings, label: string, state: WildsState, rank: number): MetricView {
  const ranked = [...(state.monster?.ailments ?? [])]
    .filter((value) => value.active || Number.isFinite(value.percent))
    .sort((a, b) => {
      if (a.active !== b.active) return a.active ? -1 : 1;
      return (b.percent ?? -1) - (a.percent ?? -1);
    });
  const selected = ranked[rank];
  if (!selected) return view(settings, label, "NONE", "status", "inactive");
  return prominentAilment(settings, label, selected);
}

function prominentAilment(settings: MetricSettings, label: string, value: AilmentState): MetricView {
  const name = value.name?.trim() || `AILMENT ${value.id}`;
  if (value.active)
    return view(settings, label, name, "status", "danger", "ACTIVE", value.percent);

  if (!Number.isFinite(value.percent))
    return view(settings, label, name, "status", "warning", "buildup");

  return view(settings, label, name, "gauge", "warning", formatPercent(value.percent), value.percent);
}

function ailment(settings: MetricSettings, label: string, value?: AilmentState): MetricView {
  if (!value) return unknown(settings, label);
  if (value.active) return view(settings, value.name ?? label, "ACTIVE", "status", "danger", formatPercent(value.percent));
  return gauge(settings, value.name ?? label, value.percent, undefined, "warning");
}

function npc(settings: MetricSettings, label: string, state: WildsState, index: number): MetricView {
  const value = state.town?.npcs?.[index];
  if (!value) return unknown(settings, label);
  return view(settings, value.name ?? label, value.hasNotification ? "READY ✓" : "CLEAR", "status", value.hasNotification ? "warning" : "good");
}

function gauge(settings: MetricSettings, label: string, percent?: number, detail?: string, tone: Tone = "neutral"): MetricView {
  if (!Number.isFinite(percent)) return unknown(settings, label);
  return view(settings, label, formatPercent(percent), "gauge", tone, detail, percent);
}

function number(settings: MetricSettings, label: string, value?: number, prefix?: string): MetricView {
  if (!Number.isFinite(value)) return unknown(settings, label);
  return view(settings, label, `${prefix ? `${prefix} ` : ""}${compactNumber(value)}`, "number", "neutral");
}

function text(settings: MetricSettings, label: string, value?: string): MetricView {
  return value ? view(settings, label, value, "text", "neutral") : unknown(settings, label);
}

function unknown(settings: MetricSettings, label: string): MetricView {
  return view(settings, label, "—", "status", "inactive", "unavailable");
}

function view(settings: MetricSettings, label: string, value: string, style: DisplayStyle, tone: Tone, detail?: string, percent?: number): MetricView {
  return { label: settings.label?.trim() || label, value, detail, percent, style: settings.displayStyle ?? style, tone };
}

export function formatPercent(value?: number): string {
  return Number.isFinite(value) ? `${Math.round(value!)}%` : "—";
}

export function compactNumber(value?: number): string {
  if (!Number.isFinite(value)) return "—";
  const absolute = Math.abs(value!);
  if (absolute >= 1_000_000) return `${(value! / 1_000_000).toFixed(1)}M`;
  if (absolute >= 10_000) return `${(value! / 1_000).toFixed(1)}K`;
  return Math.round(value!).toLocaleString("en-US");
}

function formatPair(current?: number, maximum?: number): string | undefined {
  return Number.isFinite(current) && Number.isFinite(maximum) ? `${compactNumber(current)} / ${compactNumber(maximum)}` : undefined;
}

function sum(values?: (number | undefined)[]): number | undefined {
  const known = values?.filter((value): value is number => Number.isFinite(value));
  return known?.length ? known.reduce((total, value) => total + value, 0) : undefined;
}
