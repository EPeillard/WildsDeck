import streamDeck, { action, type DidReceiveSettingsEvent, SingletonAction, type WillAppearEvent } from "@elgato/streamdeck";
import type { BridgeClient } from "../bridge/bridge-client.js";
import { resolveMetric, type MetricSettings } from "../metrics/metric-resolver.js";
import { renderMetric } from "../rendering/svg-renderer.js";

@action({ UUID: "com.wildsdeck.streamdeck.metric" })
export class WildsDisplayAction extends SingletonAction<MetricSettings> {
  readonly #bridge: BridgeClient;

  constructor(bridge: BridgeClient) {
    super();
    this.#bridge = bridge;
  }

  override onWillAppear(event: WillAppearEvent<MetricSettings>): Promise<void> {
    return this.renderAction(event.action, event.payload.settings);
  }

  override onDidReceiveSettings(event: DidReceiveSettingsEvent<MetricSettings>): Promise<void> {
    return this.renderAction(event.action, event.payload.settings);
  }

  async renderAll(): Promise<void> {
    await Promise.all([...this.actions].map(async (visibleAction) => {
      const settings = await visibleAction.getSettings<MetricSettings>();
      await this.renderAction(visibleAction, settings);
    }));
  }

  private async renderAction(
    target: { setImage(image: string): Promise<void> },
    settings: MetricSettings
  ): Promise<void> {
    const view = resolveMetric(this.#bridge.snapshot, settings);
    const svg = renderMetric(view);
    const image = `data:image/svg+xml,${encodeURIComponent(svg)}`;

    try {
      await target.setImage(image);
    } catch (error: unknown) {
      streamDeck.logger.error(`Failed to render metric ${settings.metric ?? "system.status"}: ${String(error)}`);
      throw error;
    }
  }
}
