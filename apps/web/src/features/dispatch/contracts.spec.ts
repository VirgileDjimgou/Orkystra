import {
  normalizeMissionDetailResponse,
  normalizeMissionSummaryResponse,
  serializeMissionStatus,
} from "./contracts";

describe("dispatch contracts", () => {
  it("normalizes numeric mission enums and serializes status transitions", () => {
    const summary = normalizeMissionSummaryResponse({
      id: "mission-1",
      reference: "NW-PILOT-001",
      title: "Pilot delivery loop",
      status: 5,
      scheduledStartUtc: "2026-07-16T08:00:00Z",
      scheduledEndUtc: "2026-07-16T10:00:00Z",
      driverId: "driver-1",
      driverName: "Alex North",
      vehicleId: "veh-1",
      vehicleRegistrationNumber: "NW-100",
      stopCount: 2,
      simulatedDelayMinutes: 18,
      rowVersion: 5,
      currentLatitude: 48.401,
      currentLongitude: 9.204,
    });

    const detail = normalizeMissionDetailResponse({
      id: "mission-1",
      reference: "NW-PILOT-001",
      title: "Pilot delivery loop",
      status: 5,
      scheduledStartUtc: "2026-07-16T08:00:00Z",
      scheduledEndUtc: "2026-07-16T10:00:00Z",
      driverId: "driver-1",
      driverName: "Alex North",
      vehicleId: "veh-1",
      vehicleRegistrationNumber: "NW-100",
      simulatedDelayMinutes: 18,
      rowVersion: 5,
      latestInspection: {
        inspectionId: "inspection-1",
        outcome: 2,
        hasBlockingCriticalDefect: true,
        completedAtUtc: "2026-07-16T07:10:00Z",
        notes: "Brake issue",
        items: [
          {
            sequence: 1,
            code: "brakes",
            label: "Brakes",
            isPass: false,
            defectSeverity: 3,
            notes: "Pedal pressure is unstable.",
            photoReadUrl: null,
          },
        ],
      },
      deliveryProofs: [],
      stops: [],
      timeline: [
        {
          id: "event-1",
          eventType: 3,
          description: "Mission status changed to Assigned.",
          occurredAtUtc: "2026-07-16T07:20:00Z",
        },
      ],
    });

    expect(summary.status).toBe("Delayed");
    expect(detail.status).toBe("Delayed");
    expect(detail.latestInspection?.outcome).toBe("Failed");
    expect(detail.latestInspection?.items[0].defectSeverity).toBe("Critical");
    expect(detail.timeline[0].eventType).toBe("StatusChanged");
    expect(serializeMissionStatus("Assigned")).toBe(2);
  });
});
