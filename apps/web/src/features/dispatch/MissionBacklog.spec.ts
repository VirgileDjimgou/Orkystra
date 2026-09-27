import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import MissionBacklog from "./MissionBacklog.vue";
import type { MissionSummaryResponse } from "./contracts";

const mission: MissionSummaryResponse = {
  id: "mission-1",
  reference: "NW-100",
  title: "Morning delivery",
  status: "Planned",
  scheduledStartUtc: "2026-09-27T08:00:00Z",
  scheduledEndUtc: "2026-09-27T10:00:00Z",
  driverId: "driver-1",
  driverName: "Alex North",
  vehicleId: "vehicle-1",
  vehicleRegistrationNumber: "NW-100",
  stopCount: 2,
  simulatedDelayMinutes: 0,
  rowVersion: 1,
  currentLatitude: 51.5,
  currentLongitude: -0.1,
};

describe("MissionBacklog", () => {
  it("exposes loading, error, and selectable mission states", async () => {
    const wrapper = mount(MissionBacklog, {
      props: { missions: [], status: "loading", error: "" },
    });
    expect(wrapper.text()).toContain("Loading missions...");

    await wrapper.setProps({
      status: "error",
      error: "Unable to load missions.",
    });
    expect(wrapper.get('[role="alert"]').text()).toContain(
      "Unable to load missions.",
    );

    await wrapper.setProps({
      status: "success",
      error: "",
      missions: [mission],
    });
    await wrapper.get('button[role="button"]').trigger("click");
    expect(wrapper.emitted("select")).toEqual([["mission-1"]]);
  });
});
