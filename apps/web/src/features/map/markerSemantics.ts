import type { TrackingPositionResponse } from "../tracking/contracts";

export function markerSemantics(
  position: TrackingPositionResponse,
  hasException: boolean,
) {
  const quality = position.qualityStatus ?? "Fresh";
  const moving = position.speedKph >= 3;
  const state = hasException ? "exception" : moving ? "moving" : "stopped";
  const symbol = hasException ? "!" : moving ? "›" : "■";
  return {
    state,
    symbol,
    label: `${position.registrationNumber}: ${state}, ${quality.toLowerCase()}, heading ${Math.round(position.headingDegrees)} degrees`,
    color: hasException
      ? "#b02a37"
      : quality === "Fresh"
        ? "#198754"
        : "#b7791f",
  };
}
