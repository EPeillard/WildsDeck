import { describe, expect, it } from "vitest";
import { renderMetric } from "../src/rendering/svg-renderer.js";

describe("SVG renderer", () => {
  it("creates readable SVG with a gauge", () => {
    const svg = renderMetric({ label: "HP", value: "62%", detail: "620 / 1K", percent: 62, style: "gauge", tone: "danger" });
    expect(svg).toContain("<svg");
    expect(svg).toContain("62%");
    expect(svg).toContain('width="74.4"');
  });

  it("escapes custom text", () => {
    const svg = renderMetric({ label: "A&B", value: "<ready>", style: "text", tone: "good" });
    expect(svg).toContain("A&amp;B");
    expect(svg).toContain("&lt;ready&gt;");
  });
});
