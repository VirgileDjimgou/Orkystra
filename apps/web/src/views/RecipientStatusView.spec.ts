import { mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";

const { apiRequest } = vi.hoisted(() => ({ apiRequest: vi.fn() }));
vi.mock("../services/api", () => ({ apiRequest }));
vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { token: "a".repeat(64) } }),
}));

import RecipientStatusView from "./RecipientStatusView.vue";

describe("RecipientStatusView", () => {
  it("renders only the minimal status and submits a contact correction", async () => {
    apiRequest
      .mockResolvedValueOnce({
        status: "EnRoute",
        etaWindow: "Estimated between 10:00 and 11:00 UTC",
      })
      .mockResolvedValueOnce(undefined);
    const wrapper = mount(RecipientStatusView);
    await Promise.resolve();
    await wrapper.vm.$nextTick();
    expect(wrapper.text()).not.toContain("Fleet Street");
    await wrapper.get("#recipient-email").setValue("recipient@example.test");
    await wrapper.get("form").trigger("submit.prevent");
    expect(apiRequest).toHaveBeenLastCalledWith(
      "/public/v1/recipient-status/" + "a".repeat(64) + "/contact",
      { method: "POST", body: { email: "recipient@example.test" } },
    );
  });
});
