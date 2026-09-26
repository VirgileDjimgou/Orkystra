import { describe, expect, it } from "vitest";
import { markerSemantics } from "./markerSemantics";

const position = {
  vehicleId: "v",
  registrationNumber: "NW-100",
  displayName: "Van",
  deviceId: "gps",
  recordedAtUtc: "2026-09-26T10:00:00Z",
  latitude: 1,
  longitude: 2,
  speedKph: 25,
  headingDegrees: 90,
  qualityStatus: "Delayed" as const,
};

describe("markerSemantics", () => {
  it("communicates movement, quality and exception without relying on colour", () => {
    expect(markerSemantics(position, false)).toMatchObject({
      state: "moving",
      symbol: "›",
    });
    expect(markerSemantics({ ...position, speedKph: 0 }, true)).toMatchObject({
      state: "exception",
      symbol: "!",
    });
    expect(markerSemantics(position, false).label).toContain("delayed");
  });
});
