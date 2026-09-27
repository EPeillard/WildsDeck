import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const DEFAULT_HEALTH_URL = "http://127.0.0.1:47653/health";
const DEFAULT_LAUNCH_COOLDOWN_MS = 5_000;

export interface BridgeProcessLogger {
  info(message: string): void;
  error(message: string): void;
}

export interface BridgeProcessDependencies {
  isHealthy(): Promise<boolean>;
  exists(): boolean;
  launch(): void;
  now?(): number;
  onMissing?(): void;
  onLaunchError?(error: unknown): void;
}

export class BridgeProcessManager {
  readonly #dependencies: BridgeProcessDependencies;
  readonly #launchCooldownMs: number;
  #ensurePromise?: Promise<void>;
  #lastLaunchAttemptAt = Number.NEGATIVE_INFINITY;

  constructor(dependencies: BridgeProcessDependencies, launchCooldownMs = DEFAULT_LAUNCH_COOLDOWN_MS) {
    this.#dependencies = dependencies;
    this.#launchCooldownMs = launchCooldownMs;
  }

  async ensureRunning(): Promise<void> {
    if (this.#ensurePromise) {
      await this.#ensurePromise;
      return;
    }

    const current = this.#ensureRunning();
    this.#ensurePromise = current;

    try {
      await current;
    } finally {
      if (this.#ensurePromise === current) this.#ensurePromise = undefined;
    }
  }

  async #ensureRunning(): Promise<void> {
    let healthy = false;
    try {
      healthy = await this.#dependencies.isHealthy();
    } catch {
      healthy = false;
    }
    if (healthy) return;

    const now = this.#dependencies.now?.() ?? Date.now();
    if (now - this.#lastLaunchAttemptAt < this.#launchCooldownMs) return;
    this.#lastLaunchAttemptAt = now;

    if (!this.#dependencies.exists()) {
      this.#dependencies.onMissing?.();
      return;
    }

    try {
      this.#dependencies.launch();
    } catch (error) {
      this.#dependencies.onLaunchError?.(error);
    }
  }
}

export function createBundledBridgeProcessManager(logger: BridgeProcessLogger): BridgeProcessManager {
  const pluginBinDirectory = path.dirname(fileURLToPath(import.meta.url));
  const bridgeDirectory = path.join(pluginBinDirectory, "bridge");
  const executablePath = path.join(bridgeDirectory, "WildsDeck.Bridge.exe");

  return new BridgeProcessManager({
    isHealthy: async () => {
      try {
        const response = await fetch(DEFAULT_HEALTH_URL, { signal: AbortSignal.timeout(400) });
        if (!response.ok) return false;
        const payload = await response.json() as { status?: unknown };
        return payload.status === "ok";
      } catch {
        return false;
      }
    },
    exists: () => existsSync(executablePath),
    launch: () => {
      const child = spawn(executablePath, ["--parent-pid", String(process.pid)], {
        cwd: bridgeDirectory,
        windowsHide: true,
        stdio: "ignore"
      });
      child.once("error", (error) => logger.error(`Bundled bridge process error: ${String(error)}`));
      child.unref();
      logger.info(`Starting bundled WildsDeck bridge (PID ${child.pid ?? "pending"}).`);
    },
    onMissing: () => logger.error(`Bundled bridge not found at ${executablePath}. Run scripts/build.ps1 before linking the plugin.`),
    onLaunchError: (error) => logger.error(`Could not start bundled WildsDeck bridge: ${String(error)}`)
  });
}
