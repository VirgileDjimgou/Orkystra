import { defineConfig, devices } from "@playwright/test";

const apiBaseUrl =
  process.env.PLAYWRIGHT_API_BASE_URL ?? "http://127.0.0.1:5083";
const webBaseUrl =
  process.env.PLAYWRIGHT_WEB_BASE_URL ?? "http://127.0.0.1:4175";
const apiPort = new URL(apiBaseUrl).port || "5083";
const webPort = new URL(webBaseUrl).port || "4175";

export default defineConfig({
  testDir: "./demo",
  fullyParallel: false,
  retries: 0,
  reporter: "list",
  use: {
    baseURL: webBaseUrl,
    trace: "off",
    video: "off",
    screenshot: "off",
    viewport: { width: 1440, height: 1000 },
  },
  projects: [
    {
      name: "demo-chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
  webServer: [
    {
      command: `dotnet run --project ../backend/FleetOps.Api --no-launch-profile --urls http://127.0.0.1:${apiPort}`,
      url: `${apiBaseUrl}/health/ready`,
      reuseExistingServer: false,
      timeout: 120_000,
      env: {
        ASPNETCORE_ENVIRONMENT: "DemoTesting",
        Testing__UseInMemoryDatabase: "true",
        Testing__DatabaseName: "fleetops-demo-captures",
        Bootstrap__SeedDemoData: "true",
        Bootstrap__PublicDemoOnly: "false",
        PublicDemo__Enabled: "true",
        PublicDemo__SideEffectsSandboxed: "true",
        PublicDemo__SessionLifetimeSeconds: "900",
        PublicDemo__LaunchPermitLimit: "100",
        PublicDemo__MaxConcurrentSessions: "20",
        InternalApi__Key: "FleetOps_Tests_Internal_Key_12345678901234567890",
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
