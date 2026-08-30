import streamDeck from "@elgato/streamdeck";
import { WildsDisplayAction } from "./actions/wilds-display.js";
import { BridgeClient } from "./bridge/bridge-client.js";
import { ProfileManager } from "./profile-manager.js";

streamDeck.logger.setLevel("info");

const bridge = new BridgeClient();
const display = new WildsDisplayAction(bridge);
const profiles = new ProfileManager();

streamDeck.actions.registerAction(display);
await streamDeck.connect();

streamDeck.devices.onDeviceDidConnect((event) => void profiles.syncDevice(event.device.id));
streamDeck.devices.onDeviceDidDisconnect((event) => profiles.disconnected(event.device.id));

bridge.subscribe((snapshot) => {
  profiles.update(snapshot);
  void display.renderAll().catch((error: unknown) => streamDeck.logger.error(`Render failed: ${String(error)}`));
});
bridge.start();
await profiles.syncAll();
