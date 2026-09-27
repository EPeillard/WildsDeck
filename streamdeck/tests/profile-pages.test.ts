import { describe, expect, it } from "vitest";
import { pageForMode, WILDSDECK_PROFILE } from "../src/profile-pages.js";

describe("profile pages", () => {
  it("uses one bundled WildsDeck profile", () => {
    expect(WILDSDECK_PROFILE).toBe("WildsDeck");
  });

  it("maps stable game modes to profile pages", () => {
    expect(pageForMode("town")).toBe(0);
    expect(pageForMode("hunt")).toBe(1);
    expect(pageForMode("unknown")).toBeUndefined();
  });
});
