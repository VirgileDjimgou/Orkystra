import { createPinia, setActivePinia } from "pinia";
import { mount } from "@vue/test-utils";
import { nextTick } from "vue";
import PilotReviewView from "./PilotReviewView.vue";
import { useSessionStore } from "../features/auth/store";

function authenticateAsAdmin() {
  setActivePinia(createPinia());
  useSessionStore().applySession({
    accessToken: "token",
    expiresAtUtc: "2099-01-01T00:00:00Z",
    user: {
      userId: "user-1",
      email: "admin@northwind.local",
      fullName: "Northwind Admin",
      organizationName: "Northwind Logistics",
      roles: ["Admin"],
      twoFactorEnabled: false,
    },
  });
}

async function flushUi() {
  await nextTick();
  await new Promise((resolve) => setTimeout(resolve, 0));
  await nextTick();
}

describe("PilotReviewView", () => {
  it("records a consented aggregate and exports tenant pilot evidence", async () => {
    authenticateAsAdmin();
    const createObjectUrl = vi.fn(() => "blob:pilot-evidence");
    const revokeObjectUrl = vi.fn();
    Object.defineProperty(URL, "createObjectURL", {
      value: createObjectUrl,
      configurable: true,
      writable: true,
    });
    Object.defineProperty(URL, "revokeObjectURL", {
      value: revokeObjectUrl,
      configurable: true,
      writable: true,
    });
    const anchorClick = vi.fn();
    const originalCreateElement = document.createElement.bind(document);
    const createElementSpy = vi
      .spyOn(document, "createElement")
      .mockImplementation(((tagName: string) => {
        if (tagName === "a") {
          return {
            click: anchorClick,
            href: "",
            download: "",
          } as unknown as HTMLAnchorElement;
        }

        return originalCreateElement(tagName);
      }) as typeof document.createElement);

    const metric = {
      capturedOnUtc: "2026-07-18",
      activationEvents: 1,
      activeDrivers: 2,
      returningDrivers: 1,
      processedSyncCommands: 3,
      completedMissions: 4,
      completeProofs: 4,
      openExceptions: 0,
      refreshedAtUtc: "2026-07-18T12:00:00Z",
    };
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: string | URL, init?: RequestInit) => {
        const url = String(input);
        if (url.endsWith("/api/v1/pilot/metrics")) {
          return new Response(
            JSON.stringify({
              analyticsConsent: true,
              latestDailyMetric: metric,
              openIncidents: 1,
            }),
          );
        }
        if (url.endsWith("/api/v1/pilot/incidents")) {
          return new Response(JSON.stringify([]));
        }
        if (
          url.endsWith("/api/v1/pilot/metrics/collect") &&
          init?.method === "POST"
        ) {
          return new Response(JSON.stringify(metric));
        }
        if (url.endsWith("/api/v1/pilot/export")) {
          return new Response(
            JSON.stringify({
              analyticsConsent: true,
              dailyMetrics: [metric],
              incidents: [],
              decisions: [],
            }),
          );
        }

        throw new Error(`Unhandled request ${url} ${init?.method}`);
      }),
    );

    const wrapper = mount(PilotReviewView);
    await flushUi();
    expect(wrapper.text()).toContain("Returning drivers");
    expect(wrapper.text()).toContain("Open support incidents");

    const collectButton = wrapper
      .findAll("button")
      .find((button) => button.text() === "Record daily aggregate");
    expect(collectButton).toBeTruthy();
    await collectButton!.trigger("click");
    await flushUi();
    expect(wrapper.text()).toContain("Daily aggregate recorded.");

    const exportButton = wrapper
      .findAll("button")
      .find((button) => button.text() === "Export evidence");
    expect(exportButton).toBeTruthy();
    await exportButton!.trigger("click");
    await flushUi();
    expect(createObjectUrl).toHaveBeenCalled();
    expect(anchorClick).toHaveBeenCalled();
    expect(revokeObjectUrl).toHaveBeenCalledWith("blob:pilot-evidence");
    expect(wrapper.text()).toContain("Pilot evidence export generated.");

    createElementSpy.mockRestore();
  });
});
