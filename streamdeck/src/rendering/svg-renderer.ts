import type { MetricView } from "../metrics/metric-resolver.js";
import { theme } from "./theme.js";

export function renderMetric(view: MetricView): string {
  const accent = theme.tones[view.tone];
  const percent = Number.isFinite(view.percent) ? Math.max(0, Math.min(100, view.percent!)) : undefined;
  const secondaryPercent = Number.isFinite(view.secondaryPercent)
    ? Math.max(0, Math.min(100, view.secondaryPercent!))
    : undefined;
  const secondaryAccent = view.secondaryTone ? theme.tones[view.secondaryTone] : accent;
  const valueSize = fontSize(view.value);
  const hasDualGauge = view.style === "gauge" && percent !== undefined && secondaryPercent !== undefined;
  const detailY = hasDualGauge ? 96 : 101;
  const detail = view.detail ? `<text x="72" y="${detailY}" class="detail">${escapeXml(view.detail)}</text>` : "";

  let gauge: string;
  if (hasDualGauge) {
    gauge = [
      `<rect x="12" y="108" width="120" height="7" rx="3.5" fill="${theme.track}"/>`,
      `<rect x="12" y="108" width="${(120 * percent! / 100).toFixed(1)}" height="7" rx="3.5" fill="${theme.tones.neutral}"/>`,
      `<rect x="12" y="120" width="120" height="7" rx="3.5" fill="${theme.track}"/>`,
      `<rect x="12" y="120" width="${(120 * secondaryPercent! / 100).toFixed(1)}" height="7" rx="3.5" fill="${secondaryAccent}"/>`
    ].join("");
  } else if (view.style === "gauge" && percent !== undefined) {
    gauge = `<rect x="12" y="116" width="120" height="10" rx="5" fill="${theme.track}"/><rect x="12" y="116" width="${(120 * percent / 100).toFixed(1)}" height="10" rx="5" fill="${accent}"/>`;
  } else {
    gauge = `<circle cx="72" cy="119" r="4" fill="${accent}"/>`;
  }

  return `<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144">
  <rect width="144" height="144" rx="18" fill="${theme.background}"/>
  <rect x="5" y="5" width="134" height="134" rx="15" fill="${theme.panel}" stroke="${accent}" stroke-width="2"/>
  <style>
    text{font-family:Arial,'Segoe UI',sans-serif;text-anchor:middle;fill:${theme.text}}
    .label{font-size:15px;font-weight:700;letter-spacing:.8px;fill:${theme.muted}}
    .value{font-size:${valueSize}px;font-weight:800}
    .detail{font-size:11px;font-weight:600;fill:${theme.muted}}
  </style>
  <text x="72" y="29" class="label">${escapeXml(truncate(view.label.toUpperCase(), 17))}</text>
  <text x="72" y="76" class="value" fill="${accent}">${escapeXml(truncate(view.value, 18))}</text>
  ${detail}
  ${gauge}
</svg>`;
}

function fontSize(value: string): number {
  if (value.length <= 4) return 42;
  if (value.length <= 8) return 32;
  if (value.length <= 12) return 24;
  return 19;
}

function truncate(value: string, maximum: number): string {
  return value.length <= maximum ? value : `${value.slice(0, maximum - 1)}…`;
}

function escapeXml(value: string): string {
  return value.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&apos;");
}
