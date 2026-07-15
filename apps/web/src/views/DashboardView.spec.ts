import { createPinia, setActivePinia } from "pinia";
import { mount } from "@vue/test-utils";
import DashboardView from "./DashboardView.vue";
import { useSessionStore } from "../features/auth/store";

describe("DashboardView", () => {
  it("renders the authenticated overview", () => {
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

    const wrapper = mount(DashboardView);
    expect(wrapper.text()).toContain("Operations overview");
    expect(wrapper.text()).toContain("Northwind Logistics");
    expect(wrapper.text()).toContain("Enabled");
  });
});
