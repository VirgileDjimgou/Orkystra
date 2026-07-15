import { mount } from "@vue/test-utils";
import DashboardView from "./DashboardView.vue";

describe("DashboardView", () => {
  it("renders the bootstrap dashboard", () => {
    const wrapper = mount(DashboardView);
    expect(wrapper.text()).toContain("Vue générale");
  });
});
