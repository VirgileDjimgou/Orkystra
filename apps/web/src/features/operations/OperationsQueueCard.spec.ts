import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import OperationsQueueCard from "./OperationsQueueCard.vue";
import type { OperationsExceptionListItemResponse } from "./contracts";

const item: OperationsExceptionListItemResponse = {
  id: "exception-1",
  sourceType: "Alert",
  severity: "Critical",
  workflowStatus: "Open",
  title: "Tyre pressure",
  message: "Pressure is below the safe threshold.",
  detectedAtUtc: "2026-09-27T08:00:00Z",
  snoozedUntilUtc: null,
  snoozeReason: null,
  resolvedAtUtc: null,
  resolutionReason: null,
  assignedToUserId: null,
  assignedToDisplayName: null,
  acknowledgedByUserId: null,
  acknowledgedByDisplayName: null,
  searchText: "tyre pressure",
  sourceRowVersion: 1,
  stateRowVersion: 1,
  concurrencyToken: "token",
  links: {
    missionId: "mission-1",
    missionReference: "NW-100",
    vehicleId: "vehicle-1",
    vehicleRegistrationNumber: "NW-100",
    driverId: null,
    driverName: null,
    alertId: "alert-1",
    inspectionId: null,
    syncIncidentId: null,
  },
};

describe("OperationsQueueCard", () => {
  it("keeps queue actions available through native keyboard controls", async () => {
    const wrapper = mount(OperationsQueueCard, {
      props: {
        item,
        assignees: [
          {
            userId: "user-1",
            fullName: "Morgan Fleet",
            email: "morgan@example.test",
            role: "Operator",
          },
        ],
        assignmentUserId: "",
        selected: false,
        active: false,
      },
      global: { stubs: { RouterLink: true } },
    });

    await wrapper.get("select").setValue("user-1");
    expect(wrapper.emitted("update:assignmentUserId")).toEqual([["user-1"]]);

    await wrapper
      .get('input[aria-label="Select Tyre pressure"]')
      .trigger("change");
    await wrapper.get(".btn-outline-success").trigger("click");
    expect(wrapper.emitted("toggle")).toEqual([["exception-1"]]);
    expect(wrapper.emitted("acknowledge")).toEqual([[item]]);
  });
});
