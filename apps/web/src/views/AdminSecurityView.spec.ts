import { createPinia, setActivePinia } from "pinia";
import { mount } from "@vue/test-utils";
import { nextTick } from "vue";
import AdminSecurityView from "./AdminSecurityView.vue";
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
      twoFactorEnabled: false,
    },
  });
}

async function flushUi() {
  await nextTick();
  await new Promise((resolve) => setTimeout(resolve, 0));
  await nextTick();
}

describe("AdminSecurityView", () => {
  it("loads MFA status and lifecycle summary", async () => {
    authenticateAsAdmin();
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: string | URL) => {
        const url = String(input);
        if (url.endsWith("/api/admin/security/mfa")) {
          return new Response(
            JSON.stringify({
              isEnabled: false,
              hasSharedKey: false,
              accountEmail: "admin@northwind.local",
            }),
          );
        }
        if (url.endsWith("/api/admin/data-lifecycle/summary")) {
          return new Response(
            JSON.stringify({
              generatedAtUtc: "2026-07-16T12:00:00Z",
              organizationName: "Northwind Logistics",
              organizationSlug: "northwind",
              trackingRetentionDays: 7,
              counts: [
                { key: "users", label: "Users", count: 3 },
                {
                  key: "telemetry-points",
                  label: "Telemetry points",
                  count: 12,
                },
              ],
              categories: [
                {
                  key: "tracking-history",
                  label: "Tracking history",
                  description:
                    "Delete telemetry history points and stale current positions.",
                },
              ],
            }),
          );
        }

        throw new Error(`Unhandled request ${url}`);
      }),
    );

    const wrapper = mount(AdminSecurityView);
    await flushUi();

    expect(wrapper.text()).toContain("Security and data lifecycle");
    expect(wrapper.text()).toContain("Not enabled");
    expect(wrapper.text()).toContain("Users");
    expect(wrapper.text()).toContain("Telemetry points");
    expect(wrapper.text()).toContain("7 day(s)");
  });

  it("enables MFA and exports the tenant snapshot", async () => {
    authenticateAsAdmin();
    const originalCreateElement = document.createElement.bind(document);
    const createObjectUrl = vi.fn(() => "blob:tenant-export");
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
        if (url.endsWith("/api/admin/security/mfa")) {
          return new Response(
            JSON.stringify({
              isEnabled: false,
              hasSharedKey: false,
              accountEmail: "admin@northwind.local",
            }),
          );
        }
        if (url.endsWith("/api/admin/data-lifecycle/summary")) {
          return new Response(
            JSON.stringify({
              generatedAtUtc: "2026-07-16T12:00:00Z",
              organizationName: "Northwind Logistics",
              organizationSlug: "northwind",
              trackingRetentionDays: 7,
              counts: [{ key: "users", label: "Users", count: 3 }],
              categories: [
                {
                  key: "tracking-history",
                  label: "Tracking history",
                  description:
                    "Delete telemetry history points and stale current positions.",
                },
              ],
            }),
          );
        }
        if (
          url.endsWith("/api/admin/security/mfa/setup") &&
          init?.method === "POST"
        ) {
          return new Response(
            JSON.stringify({
              isEnabled: false,
              sharedKey: "ABCD EFGH IJKL",
              manualEntryKey: "abcdefghijkl",
              authenticatorUri:
                "otpauth://totp/Orkystra%20FleetOps:admin@northwind.local",
            }),
          );
        }
        if (
          url.endsWith("/api/admin/security/mfa/verify") &&
          init?.method === "POST"
        ) {
          return new Response(
            JSON.stringify({
              isEnabled: true,
              recoveryCodes: ["code-1", "code-2"],
            }),
          );
        }
        if (url.endsWith("/api/admin/data-lifecycle/export")) {
          return new Response('{"schemaVersion":"pilot-1"}');
        }

        throw new Error(`Unhandled request ${url} ${init?.method}`);
      }),
    );

    const wrapper = mount(AdminSecurityView);
    await flushUi();

    const setupButton = wrapper
      .findAll("button")
      .find((button) => button.text().includes("Generate setup secret"));
    expect(setupButton).toBeTruthy();
    await setupButton!.trigger("click");
    await flushUi();

    await wrapper.get("#mfaVerifyCode").setValue("123456");
    const enableButton = wrapper
      .findAll("button")
      .find((button) => button.text() === "Enable MFA");
    expect(enableButton).toBeTruthy();
    await enableButton!.trigger("click");
    await flushUi();

    expect(wrapper.text()).toContain("Administrator MFA enabled successfully.");
    expect(wrapper.text()).toContain("code-1");

    const exportButton = wrapper
      .findAll("button")
      .find((button) => button.text() === "Download tenant export");
    expect(exportButton).toBeTruthy();
    await exportButton!.trigger("click");
    await flushUi();

    expect(createObjectUrl).toHaveBeenCalled();
    expect(anchorClick).toHaveBeenCalled();
    expect(wrapper.text()).toContain("Tenant export generated successfully.");

    createElementSpy.mockRestore();
  });
});
