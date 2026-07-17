import { createPinia, setActivePinia } from "pinia";
import { mount } from "@vue/test-utils";
import { nextTick } from "vue";
import { useSessionStore } from "../features/auth/store";
import OnboardingView from "./OnboardingView.vue";

function authenticateAsAdmin() {
  setActivePinia(createPinia());
  useSessionStore().applySession({
    accessToken: "token",
    expiresAtUtc: "2099-01-01T00:00:00Z",
    user: {
      userId: "admin-1",
      email: "admin@northwind.local",
      fullName: "Northwind Admin",
      organizationName: "Northwind Logistics",
      roles: ["Admin"],
      twoFactorEnabled: true,
    },
  });
}

async function flushUi() {
  await nextTick();
  await new Promise((resolve) => setTimeout(resolve, 0));
  await nextTick();
}

function baseResponse(url: string) {
  if (url.endsWith("/api/v1/onboarding/status")) {
    return new Response(
      JSON.stringify({
        vehicles: 1,
        drivers: 1,
        devices: 1,
        operators: 1,
        driverAccounts: 1,
        pairedDriverSessions: 1,
        activeDeviceAssignments: 1,
        complianceDocuments: 1,
        missions: 1,
        completedMissions: 0,
        adminMfaEnabled: true,
        hasSampleData: false,
        startedAtUtc: "2026-07-17T12:00:00Z",
        firstValueAtUtc: null,
      }),
    );
  }
  if (url.endsWith("/api/v1/onboarding/metrics")) {
    return new Response(JSON.stringify({ minutesToFirstValue: null }));
  }
  if (url.endsWith("/api/v1/admin/users")) {
    return new Response(
      JSON.stringify([
        {
          userId: "driver-user",
          fullName: "Alex Driver",
          role: "Driver",
          driverId: "driver-1",
        },
      ]),
    );
  }
  if (url.endsWith("/api/v1/fleet/drivers")) {
    return new Response(
      JSON.stringify([
        { id: "driver-1", fullName: "Alex Driver", licenseNumber: "LIC-1" },
        { id: "driver-2", fullName: "Sam Driver", licenseNumber: "LIC-2" },
      ]),
    );
  }
  if (url.endsWith("/api/v1/onboarding/imports/latest")) {
    return new Response(null, { status: 204 });
  }
  throw new Error(`Unhandled request ${url}`);
}

describe("OnboardingView", () => {
  it("renders an accessible role-based checklist and resumes the latest preview", async () => {
    authenticateAsAdmin();
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: string | URL) => {
        const url = String(input);
        if (url.endsWith("/api/v1/onboarding/imports/latest")) {
          return new Response(
            JSON.stringify({
              previewId: "preview-1",
              targetType: "drivers",
              rowCount: 2,
              errors: [
                {
                  line: 3,
                  field: "licenseNumber",
                  message: "Value is required.",
                },
              ],
              expiresAtUtc: "2099-01-01T00:00:00Z",
              canConfirm: false,
              rowVersion: 0,
            }),
          );
        }
        return baseResponse(url);
      }),
    );

    const wrapper = mount(OnboardingView);
    await flushUi();

    expect(wrapper.get("h1").text()).toContain("first test mission");
    expect(wrapper.get("#importCsv").attributes("required")).toBeDefined();
    expect(wrapper.get("#inviteEmail").attributes("type")).toBe("email");
    expect(wrapper.text()).toContain("Value is required.");
    expect(
      wrapper
        .findAll("label")
        .some((label) => label.attributes("for") === "driverUser"),
    ).toBe(true);
    const confirmButton = wrapper
      .findAll("button")
      .find((button) => button.text() === "Confirm validated import");
    expect(confirmButton?.attributes("disabled")).toBeDefined();
  });

  it("previews and confirms a valid import", async () => {
    authenticateAsAdmin();
    const fetchMock = vi.fn(async (input: string | URL, init?: RequestInit) => {
      const url = String(input);
      if (
        url.endsWith("/api/v1/onboarding/imports/preview") &&
        init?.method === "POST"
      ) {
        return new Response(
          JSON.stringify({
            previewId: "preview-valid",
            targetType: "vehicles",
            rowCount: 1,
            errors: [],
            expiresAtUtc: "2099-01-01T00:00:00Z",
            canConfirm: true,
            rowVersion: 0,
          }),
        );
      }
      if (
        url.endsWith("/api/v1/onboarding/imports/preview-valid/confirm") &&
        init?.method === "POST"
      ) {
        return new Response(
          JSON.stringify({ created: 1, updated: 0, skipped: 0 }),
        );
      }
      return baseResponse(url);
    });
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mount(OnboardingView);
    await flushUi();
    await wrapper
      .get("#importCsv")
      .setValue("registrationNumber,displayName\nWEB-001,Web van");
    await wrapper.get("form").trigger("submit");
    await flushUi();
    expect(wrapper.text()).toContain("ready for confirmation");

    const confirmButton = wrapper
      .findAll("button")
      .find((button) => button.text() === "Confirm validated import");
    await confirmButton!.trigger("click");
    await flushUi();
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining(
        "/api/v1/onboarding/imports/preview-valid/confirm",
      ),
      expect.objectContaining({ method: "POST" }),
    );
  });
});
