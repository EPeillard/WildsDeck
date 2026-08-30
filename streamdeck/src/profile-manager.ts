import streamDeck, { DeviceType } from "@elgato/streamdeck";
import type { ConnectionSnapshot, GameMode } from "./telemetry.js";

const profiles: Partial<Record<GameMode, string>> = {
  town: "WildsDeck - Town",
  hunt: "WildsDeck - Hunt"
};

export class ProfileManager {
  readonly #lastProfileByDevice = new Map<string, string>();
  #mode: GameMode = "unknown";

  update(snapshot: ConnectionSnapshot): void {
    const nextMode = snapshot.state?.connected ? snapshot.state.mode : "unknown";
    if (nextMode === "unknown") return;
    this.#mode = nextMode;
    void this.syncAll();
  }

  async syncAll(): Promise<void> {
    await Promise.all([...streamDeck.devices]
      .filter((device) => device.isConnected && device.type === DeviceType.StreamDeck)
      .map((device) => this.syncDevice(device.id)));
  }

  async syncDevice(deviceId: string): Promise<void> {
    const profile = profiles[this.#mode];
    if (!profile || this.#lastProfileByDevice.get(deviceId) === profile) return;

    await streamDeck.profiles.switchToProfile(deviceId, profile);
    this.#lastProfileByDevice.set(deviceId, profile);
    streamDeck.logger.info(`Switched ${deviceId} to ${profile}`);
  }

  disconnected(deviceId: string): void {
    this.#lastProfileByDevice.delete(deviceId);
  }
}

