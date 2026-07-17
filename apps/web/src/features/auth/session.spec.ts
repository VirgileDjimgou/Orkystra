import { beforeEach, describe, expect, it } from "vitest";
import { clearLegacyStoredSession } from "./session";

describe("protected Web session migration", () => {
  beforeEach(() => window.localStorage.clear());

  it("erases the historical localStorage JWT cache", () => {
    window.localStorage.setItem(
      "fleetops.session",
      JSON.stringify({ accessToken: "must-not-remain-readable" }),
    );

    clearLegacyStoredSession();

    expect(window.localStorage.getItem("fleetops.session")).toBeNull();
  });
});
