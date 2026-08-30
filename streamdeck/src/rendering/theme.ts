import type { Tone } from "../metrics/metric-resolver.js";

export const theme = {
  background: "#0A0D12",
  panel: "#141A23",
  text: "#F7FAFC",
  muted: "#98A6B8",
  track: "#2A3442",
  tones: {
    neutral: "#68A8FF",
    good: "#42D392",
    warning: "#FFBF47",
    danger: "#FF5B63",
    inactive: "#667085",
    error: "#FF3D71"
  } satisfies Record<Tone, string>
} as const;

