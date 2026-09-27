import { describe, expect, it, vi } from "vitest";
import { BridgeProcessManager, type BridgeProcessDependencies } from "../src/bridge/bridge-process.js";

describe("BridgeProcessManager", () => {
  it("does not launch when the bridge is already healthy", async () => {
    const launch = vi.fn();
    const dependencies: BridgeProcessDependencies = {
      isHealthy: vi.fn().mockResolvedValue(true),
      exists: vi.fn(() => true),
      launch
    };

    await new BridgeProcessManager(dependencies).ensureRunning();
    expect(launch).not.toHaveBeenCalled();
  });

  it("launches the bundled bridge when health is unavailable", async () => {
    const launch = vi.fn();
    const dependencies: BridgeProcessDependencies = {
      isHealthy: vi.fn().mockResolvedValue(false),
      exists: vi.fn(() => true),
      launch,
      now: () => 10_000
    };

    await new BridgeProcessManager(dependencies).ensureRunning();
    expect(launch).toHaveBeenCalledTimes(1);
  });

  it("coalesces concurrent startup requests and throttles repeated launches", async () => {
    let releaseHealth!: (value: boolean) => void;
    const isHealthy = vi.fn(() => new Promise<boolean>((resolve) => {
      releaseHealth = resolve;
    }));
    const launch = vi.fn();
    let now = 10_000;
    const dependencies: BridgeProcessDependencies = {
      isHealthy,
      exists: vi.fn(() => true),
      launch,
      now: () => now
    };
    const manager = new BridgeProcessManager(dependencies, 5_000);

    const first = manager.ensureRunning();
    const second = manager.ensureRunning();

    expect(isHealthy).toHaveBeenCalledTimes(1);
    releaseHealth(false);
    await Promise.all([first, second]);
    expect(launch).toHaveBeenCalledTimes(1);

    await manager.ensureRunning();
    expect(launch).toHaveBeenCalledTimes(1);

    now += 5_001;
    await manager.ensureRunning();
    expect(launch).toHaveBeenCalledTimes(2);
  });

  it("reports a missing packaged executable instead of spawning", async () => {
    const launch = vi.fn();
    const onMissing = vi.fn();
    const dependencies: BridgeProcessDependencies = {
      isHealthy: vi.fn().mockResolvedValue(false),
      exists: vi.fn(() => false),
      launch,
      onMissing,
      now: () => 10_000
    };

    await new BridgeProcessManager(dependencies).ensureRunning();
    expect(launch).not.toHaveBeenCalled();
    expect(onMissing).toHaveBeenCalledTimes(1);
  });
});
