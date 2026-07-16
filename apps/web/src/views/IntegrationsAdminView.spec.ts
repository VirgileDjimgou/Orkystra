import { createPinia, setActivePinia } from "pinia";
import { mount } from "@vue/test-utils";
import { nextTick } from "vue";
import IntegrationsAdminView from "./IntegrationsAdminView.vue";
import { useSessionStore } from "../features/auth/store";

function authenticateAsAdmin() {
  setActivePinia(createPinia());
  const session = useSessionStore();
  session.applySession({
    accessToken: "token",
    expiresAtUtc: "2099-01-01T00:00:00Z",
    user: {
      userId: "user-1",
      email: "admin@northwind.local",
      fullName: "Northwind Admin",
      organizationName: "Northwind Logistics",
      roles: ["Admin"],
    },
  });
}

async function flushUi() {
  await nextTick();
  await new Promise((resolve) => setTimeout(resolve, 0));
  await nextTick();
}

describe("IntegrationsAdminView", () => {
  it("loads credentials, webhooks, contracts, and outbox details", async () => {
    authenticateAsAdmin();
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: string | URL, init?: RequestInit) => {
        const url = String(input);
        if (url.endsWith("/api/admin/integrations/api-keys")) {
          return new Response(
            JSON.stringify([
              {
                id: "cred-1",
                name: "Northwind Partner",
                credentialType: "Partner",
                scopes: ["partner-fleet-read"],
                keyId: "fo_abc123",
                secretPreview: "fo_abc123.****",
                isActive: true,
                lastUsedAtUtc: null,
                revokedAtUtc: null,
                rowVersion: 0,
              },
            ]),
          );
        }
        if (url.endsWith("/api/admin/integrations/webhooks")) {
          return new Response(
            JSON.stringify([
              {
                id: "wh-1",
                name: "Sandbox alerts",
                eventType: "alerts.opened",
                targetUrl: "https://fleetops.test/webhooks",
                isActive: true,
                isSandbox: true,
                lastSucceededAtUtc: "2026-07-16T10:00:00Z",
                disabledAtUtc: null,
                rowVersion: 0,
              },
            ]),
          );
        }
        if (url.endsWith("/api/admin/integrations/contracts")) {
          return new Response(
            JSON.stringify([
              {
                eventType: "fleet.vehicle.created",
                description: "Raised when a vehicle is created.",
                examplePayload: {
                  vehicleId: "veh-1",
                  registrationNumber: "NW-100",
                },
              },
            ]),
          );
        }
        if (url.endsWith("/api/admin/integrations/outbox")) {
          return new Response(
            JSON.stringify([
              {
                id: "out-1",
                webhookEndpointId: "wh-1",
                eventType: "fleet.vehicle.created",
                aggregateType: "vehicle",
                aggregateId: "veh-1",
                status: "Delivered",
                attemptCount: 1,
                occurredAtUtc: "2026-07-16T10:00:00Z",
                nextAttemptAtUtc: "2026-07-16T10:00:00Z",
                deliveredAtUtc: "2026-07-16T10:00:02Z",
                deadLetteredAtUtc: null,
                lastError: null,
              },
            ]),
          );
        }

        throw new Error(`Unhandled request ${url} ${init?.method}`);
      }),
    );

    const wrapper = mount(IntegrationsAdminView);
    await flushUi();

    expect(wrapper.text()).toContain("Integrations and audit");
    expect(wrapper.text()).toContain("Northwind Partner");
    expect(wrapper.text()).toContain("Sandbox alerts");
    expect(wrapper.text()).toContain("fleet.vehicle.created");
    expect(wrapper.text()).toContain("Delivered");
  });

  it("creates a credential and reveals the one-time secret", async () => {
    authenticateAsAdmin();
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: string | URL, init?: RequestInit) => {
        const url = String(input);
        if (url.endsWith("/api/admin/integrations/contracts")) {
          return new Response(
            JSON.stringify([
              {
                eventType: "fleet.vehicle.created",
                description: "Raised when a vehicle is created.",
                examplePayload: { vehicleId: "veh-1" },
              },
            ]),
          );
        }
        if (
          url.endsWith("/api/admin/integrations/api-keys") &&
          (init?.method ?? "GET") === "POST"
        ) {
          return new Response(
            JSON.stringify({
              id: "cred-2",
              name: "Fresh device",
              credentialType: "Device",
              scopes: ["device-tracking-write"],
              keyId: "fo_new",
              plainTextSecret: "fo_new.super-secret",
              secretPreview: "fo_new.****",
              isActive: true,
              rowVersion: 0,
            }),
          );
        }
        if (url.endsWith("/api/admin/integrations/api-keys")) {
          return new Response(JSON.stringify([]));
        }
        if (url.endsWith("/api/admin/integrations/webhooks")) {
          return new Response(JSON.stringify([]));
        }
        if (url.endsWith("/api/admin/integrations/outbox")) {
          return new Response(JSON.stringify([]));
        }

        throw new Error(`Unhandled request ${url}`);
      }),
    );

    const wrapper = mount(IntegrationsAdminView);
    await flushUi();

    await wrapper.get("#credentialType").setValue("Device");
    await wrapper.get("#credentialName").setValue("Fresh device");
    await wrapper.get("form").trigger("submit.prevent");
    await flushUi();

    expect(wrapper.text()).toContain(
      "Credential Fresh device issued successfully.",
    );
    expect(wrapper.text()).toContain("fo_new.super-secret");
  });

  it("exports and imports CSV through the admin exchange desk", async () => {
    authenticateAsAdmin();
    const originalCreateElement = document.createElement.bind(document);
    const createObjectUrl = vi.fn(() => "blob:csv");
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

    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: string | URL, init?: RequestInit) => {
        const url = String(input);
        if (url.endsWith("/api/admin/integrations/api-keys")) {
          return new Response(JSON.stringify([]));
        }
        if (url.endsWith("/api/admin/integrations/webhooks")) {
          return new Response(JSON.stringify([]));
        }
        if (url.endsWith("/api/admin/integrations/contracts")) {
          return new Response(
            JSON.stringify([
              {
                eventType: "fleet.vehicle.created",
                description: "Raised when a vehicle is created.",
                examplePayload: { vehicleId: "veh-1" },
              },
            ]),
          );
        }
        if (url.endsWith("/api/admin/integrations/outbox")) {
          return new Response(JSON.stringify([]));
        }
        if (
          url.endsWith("/api/v1/fleet/vehicles/export") &&
          (init?.method ?? "GET") === "GET"
        ) {
          return new Response(
            "registrationNumber,displayName,isActive,currentOdometerKm",
          );
        }
        if (
          url.endsWith("/api/v1/fleet/vehicles/import") &&
          init?.method === "POST"
        ) {
          return new Response(
            JSON.stringify({
              created: 1,
              updated: 0,
              skipped: 0,
              errors: [],
            }),
          );
        }

        throw new Error(`Unhandled request ${url} ${init?.method}`);
      }),
    );

    const wrapper = mount(IntegrationsAdminView);
    await flushUi();

    const exportButton = wrapper
      .findAll("button")
      .find((button) => button.text() === "Export CSV");
    expect(exportButton).toBeTruthy();
    await exportButton!.trigger("click");
    await flushUi();

    expect(createObjectUrl).toHaveBeenCalled();
    expect(anchorClick).toHaveBeenCalled();
    expect(wrapper.text()).toContain("Vehicles export generated successfully.");

    const importButton = wrapper
      .findAll("button")
      .find((button) => button.text() === "Run import");
    expect(importButton).toBeTruthy();
    await wrapper
      .get('[data-testid="csv-import-form"]')
      .trigger("submit.prevent");
    await flushUi();

    expect(wrapper.text()).toContain(
      "Vehicles import complete: 1 created, 0 updated, 0 skipped.",
    );

    createElementSpy.mockRestore();
  });
});
