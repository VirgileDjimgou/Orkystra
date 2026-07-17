import { defineConfig, devices } from "@playwright/test";

const apiBaseUrl =
  process.env.PLAYWRIGHT_API_BASE_URL ?? "http://127.0.0.1:5080";
const webBaseUrl =
  process.env.PLAYWRIGHT_WEB_BASE_URL ?? "http://127.0.0.1:4173";

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["list"], ["html", { open: "never" }]] : "list",
  use: {
    baseURL: webBaseUrl,
    // Network traces can retain HttpOnly Set-Cookie values. Keep pilot artifacts credential-free.
    trace: "off",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
  webServer: [
    {
      command:
        "dotnet run --project ../backend/FleetOps.Api --no-launch-profile --urls http://127.0.0.1:5080",
      url: `${apiBaseUrl}/health/ready`,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: {
        ASPNETCORE_ENVIRONMENT: "Development",
        Testing__UseInMemoryDatabase: "true",
        Testing__DatabaseName: "fleetops-playwright",
        Bootstrap__SeedDemoData: "true",
        FLEETOPS_WEB_URL: webBaseUrl,
        Jwt__Issuer: "FleetOps.Tests",
        Jwt__Audience: "FleetOps.Tests.Web",
        Jwt__SigningKey: "FleetOps_Tests_Signing_Key_12345678901234567890",
        Security__LoginPermitLimit: "100",
        Integrations__RetryBaseDelaySeconds: "0",
        Integrations__MaxWebhookAttempts: "3",
      },
    },
    {
      command: "npm run dev -- --host 127.0.0.1 --port 4173",
      url: `${webBaseUrl}/login`,
      cwd: ".",
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: {
        VITE_API_BASE_URL: apiBaseUrl,
      },
    },
  ],
});
