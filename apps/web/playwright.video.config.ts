import { defineConfig, devices } from "@playwright/test";
import path from "node:path";

const apiBaseUrl =
  process.env.PLAYWRIGHT_API_BASE_URL ?? "http://127.0.0.1:5080";
const webBaseUrl =
  process.env.PLAYWRIGHT_WEB_BASE_URL ?? "http://127.0.0.1:4177";
const webPort = new URL(webBaseUrl).port || "4177";
const outputDirectory = path.resolve(
  process.cwd(),
  process.env.DEMO_VIDEO_OUTPUT ?? "../../.runtime/demo-video/playwright",
);

export default defineConfig({
  testDir: "./demo-video",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 20 * 60 * 1000,
  reporter: "list",
  outputDir: outputDirectory,
  use: {
    baseURL: webBaseUrl,
    viewport: { width: 1600, height: 900 },
    trace: "off",
    screenshot: "off",
    actionTimeout: 15_000,
    video: { mode: "on", size: { width: 1600, height: 900 } },
  },
  projects: [
    {
      name: "demo-video",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
  webServer: [
    {
      command: `npm run dev -- --host 127.0.0.1 --port ${webPort}`,
      url: `${webBaseUrl}/login`,
      cwd: ".",
      reuseExistingServer: false,
      timeout: 120_000,
      env: {
        VITE_API_BASE_URL: apiBaseUrl,
      },
    },
  ],
});
