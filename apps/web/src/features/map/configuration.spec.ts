import { describe, expect, it } from "vitest";
import { getMapTileProviderConfiguration } from "./configuration";

describe("getMapTileProviderConfiguration", () => {
  it("supplies an attribution-bearing development fallback", () => {
    const provider = getMapTileProviderConfiguration();
    expect(provider.url).toContain("{z}");
    expect(provider.attribution).toContain("OpenStreetMap");
  });
});
