import streamDeck, { DeviceType } from "@elgato/streamdeck";
import type { ConnectionSnapshot, GameMode } from "./telemetry.js";
import { pageForMode, WILDSDECK_PROFILE } from "./profile-pages.js";

export class ProfileManager {
  readonly #lastPageByDevice = new Map<string, number>();
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
    const page = pageForMode(this.#mode);
    if (page === undefined || this.#lastPageByDevice.get(deviceId) === page) return;

    await streamDeck.profiles.switchToProfile(deviceId, WILDSDECK_PROFILE, page);
    this.#lastPageByDevice.set(deviceId, page);
    streamDeck.logger.info(`Switched ${deviceId} to ${WILDSDECK_PROFILE} page ${page} (${this.#mode})`);
  }

  disconnected(deviceId: string): void {
    this.#lastPageByDevice.delete(deviceId);
  }
}
