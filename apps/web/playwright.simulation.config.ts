import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./simulation",
  fullyParallel: false,
  retries: 0,
  reporter: "list",
  use: {
    baseURL: process.env.PLAYWRIGHT_WEB_BASE_URL ?? "http://127.0.0.1:4174",
    trace: "off",
    video: "off",
    screenshot: "off",
    viewport: { width: 1440, height: 1000 },
  },
  projects: [
    {
      name: "simulation-chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
});
