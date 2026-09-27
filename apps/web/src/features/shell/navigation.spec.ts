import { describe, expect, it } from "vitest";
import { primaryNavigation } from "./navigation";

describe("primaryNavigation", () => {
  it("organizes the application into the four product navigation groups", () => {
    expect(primaryNavigation.map((group) => group.label)).toEqual([
      "Operate",
      "Fleet",
      "Manage",
      "Administration",
    ]);
  });

  it("keeps missions and daily planning under Manage", () => {
    const manage = primaryNavigation.find((group) => group.label === "Manage");

    expect(manage?.items).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          label: "Missions",
          to: "/dispatch/missions",
        }),
        expect.objectContaining({
          label: "Daily planning",
          to: "/dispatch/productivity",
        }),
      ]),
    );
  });
});
