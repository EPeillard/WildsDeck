import streamDeck from "@elgato/streamdeck";
import { WildsDisplayAction } from "./actions/wilds-display.js";
import { BridgeClient } from "./bridge/bridge-client.js";
import { createBundledBridgeProcessManager } from "./bridge/bridge-process.js";
import { ProfileManager } from "./profile-manager.js";

streamDeck.logger.setLevel("info");

const bridge = new BridgeClient();
const bridgeProcess = createBundledBridgeProcessManager({
  info: (message) => streamDeck.logger.info(message),
  error: (message) => streamDeck.logger.error(message)
});
const display = new WildsDisplayAction(bridge);
const profiles = new ProfileManager();

streamDeck.actions.registerAction(display);
await streamDeck.connect();

streamDeck.devices.onDeviceDidConnect((event) => void profiles.syncDevice(event.device.id));
streamDeck.devices.onDeviceDidDisconnect((event) => profiles.disconnected(event.device.id));

bridge.subscribe((snapshot) => {
  if (!snapshot.bridgeConnected) void bridgeProcess.ensureRunning();
  profiles.update(snapshot);
  void display.renderAll().catch((error: unknown) => streamDeck.logger.error(`Render failed: ${String(error)}`));
});
bridge.start();
await profiles.syncAll();
