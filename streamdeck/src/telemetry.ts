export type GameMode = "unknown" | "town" | "hunt";

export interface ProtocolEnvelope<T = unknown> {
  protocolVersion: number;
  type: "hello" | "state" | "modeChanged" | "error";
  data: T;
}

export interface WildsState {
  connected: boolean;
  gameVersion?: string;
  mode: GameMode;
  timestamp: string;
  mock?: boolean;
  mapFile?: string;
  error?: TelemetryError;
  player?: PlayerState;
  quest?: QuestState;
  monster?: MonsterState;
  party?: PartyMemberState[];
  town?: TownState;
}

export interface TelemetryError {
  code: string;
  message: string;
  requiredMapFile?: string;
}

export interface QuestState {
  active?: boolean;
  id?: number;
  elapsedSeconds?: number;
  maxSeconds?: number;
}

export interface PlayerState {
  name?: string;
  weaponType?: string;
  damageTotal?: number;
  damagePartySharePercent?: number;
  attack?: number;
  affinity?: number;
}

export interface PartyMemberState {
  name?: string;
  weaponType?: string;
  damage?: number;
  damageSharePercent?: number;
  isLocalPlayer?: boolean;
}

export interface GaugeState {
  current?: number;
  max?: number;
  percent?: number;
}

export interface MonsterState {
  id?: number;
  name?: string;
  selection?: string;
  health?: GaugeState;
  enrage?: { active?: boolean; value?: number; max?: number; timer?: number; maxTimer?: number; percent?: number };
  stamina?: GaugeState;
  captureReady?: boolean;
  captureThreshold?: number;
  parts?: MonsterPartState[];
  ailments?: AilmentState[];
}

export interface MonsterPartState extends GaugeState {
  id: string;
  name?: string;
  type?: "flinch" | "breakable" | "severable" | string;
  flinch?: GaugeState;
  break?: GaugeState;
  sever?: GaugeState;
  breakable?: boolean;
  severable?: boolean;
  broken?: boolean;
  breakCount?: number;
  maxBreaks?: number;
  resetCount?: number;
  breakMultiplier?: number;
}

export interface AilmentState extends GaugeState {
  id: string;
  name?: string;
  active?: boolean;
  timer?: number;
  maxTimer?: number;
}

export interface ActivityState {
  available?: boolean;
  status?: string;
  ready?: boolean;
  current?: number;
  max?: number;
  timer?: number;
  support?: "unsupported" | "experimental" | "supported";
}

export interface MaterialCollectorState {
  id: string;
  name: string;
  current: number;
  max: number;
  percent?: number;
}

export interface TownState {
  hunterRank?: number;
  supportShip?: ActivityState;
  ingredientsCenter?: ActivityState;
  materialCollectors?: MaterialCollectorState[];
  materialRetrieval?: ActivityState;
  npcNotification?: boolean;
  npcs?: { id?: string; name?: string; hasNotification?: boolean }[];
}

export interface ConnectionSnapshot {
  bridgeConnected: boolean;
  state?: WildsState;
}
