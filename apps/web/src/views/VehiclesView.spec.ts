import { createPinia, setActivePinia } from "pinia";
import { mount } from "@vue/test-utils";
import VehiclesView from "./VehiclesView.vue";
import { useSessionStore } from "../features/auth/store";
import { useFleetStore } from "../features/fleet/store";

describe("VehiclesView", () => {
  it("renders empty state when no vehicles are loaded", () => {
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

    const wrapper = mount(VehiclesView);

    expect(wrapper.text()).toContain("Vehicles");
    expect(wrapper.text()).toContain("No vehicles registered yet.");
  });

  it("shows the create form for admins", () => {
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

    const wrapper = mount(VehiclesView);

    expect(wrapper.text()).toContain("Register a vehicle");
  });

  it("hides the create form for operators", () => {
    setActivePinia(createPinia());
    const session = useSessionStore();
    session.applySession({
      accessToken: "token",
      expiresAtUtc: "2099-01-01T00:00:00Z",
      user: {
        userId: "user-2",
        email: "operator@northwind.local",
        fullName: "Northwind Operator",
        organizationName: "Northwind Logistics",
        roles: ["Operator"],
      },
    });

    const wrapper = mount(VehiclesView);

    expect(wrapper.text()).toContain("Read-only access");
    expect(wrapper.text()).not.toContain("Register a vehicle");
  });

  it("lists loaded vehicles", async () => {
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
    const fleet = useFleetStore();
    fleet.vehicles = [
      {
        id: "veh-1",
        registrationNumber: "NW-100",
        displayName: "Dispatch van",
        isActive: true,
        rowVersion: 0,
      },
    ];

    const wrapper = mount(VehiclesView);

    expect(wrapper.text()).toContain("NW-100");
    expect(wrapper.text()).toContain("Dispatch van");
  });
});
