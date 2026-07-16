import {
  normalizeAlertListItemResponse,
  normalizeAlertNotificationResponse,
  normalizeAlertSummaryResponse,
} from "./contracts";

describe("alert contracts", () => {
  it("normalizes numeric alert enums returned by the backend", () => {
    const alert = normalizeAlertListItemResponse({
      id: "alert-1",
      ruleType: 4,
      severity: 3,
      status: 2,
      title: "Overdue maintenance by mileage",
      message: "NW-100 plan Quarterly Safety Service is due at 10100 km.",
      targetType: "vehicle",
      targetEntityId: "veh-1",
      targetLabel: "NW-100",
      assignedToUserId: null,
      assignedToDisplayName: null,
      acknowledgedByUserId: null,
      acknowledgedByDisplayName: null,
      lastDetectedAtUtc: "2026-07-16T08:00:00Z",
      assignedAtUtc: null,
      acknowledgedAtUtc: null,
      resolvedAtUtc: null,
      rowVersion: 2,
    });

    const notification = normalizeAlertNotificationResponse({
      id: "notification-1",
      alertId: "alert-1",
      channel: 2,
      subject: "Overdue maintenance by mileage",
      body: "NW-100 plan Quarterly Safety Service is due at 10100 km.",
      sentAtUtc: "2026-07-16T08:00:00Z",
    });

    const summary = normalizeAlertSummaryResponse({
      openCount: 1,
      acknowledgedCount: 1,
      criticalCount: 1,
      warningCount: 0,
      inactiveVehicleCount: 0,
      maintenanceCount: 1,
      complianceCount: 0,
      topAlerts: [alert],
      recentNotifications: [notification],
    });

    expect(alert.ruleType).toBe("VehicleMaintenanceByMileage");
    expect(alert.severity).toBe("Critical");
    expect(alert.status).toBe("Acknowledged");
    expect(notification.channel).toBe("EmailDev");
    expect(summary.topAlerts[0].severity).toBe("Critical");
    expect(summary.recentNotifications[0].channel).toBe("EmailDev");
  });
});
