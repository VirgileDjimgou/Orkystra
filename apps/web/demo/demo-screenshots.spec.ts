import type { APIRequestContext } from "@playwright/test";
import { expect, test } from "@playwright/test";
import { copyFile } from "node:fs/promises";
import path from "node:path";

const apiBaseUrl =
  process.env.PLAYWRIGHT_API_BASE_URL ?? "http://127.0.0.1:5083";
const internalApiKey = "FleetOps_Tests_Internal_Key_12345678901234567890";
const screenshotDirectory = path.resolve(
  process.cwd(),
  "../../docs/assets/screenshots",
);

test("capture the public demo launch page", async ({ page }, testInfo) => {
  await page.goto("/demo");
  await expect(
    page.getByRole("heading", { name: "Explore FleetOps live." }),
  ).toBeVisible();
  await capture(page, testInfo, "demo-launch.png");
});

test("capture the animated public demo cockpit and exception context", async ({
  page,
  request,
}, testInfo) => {
  const fleet = await loadPublicDemoFleet(request);
  await pushPublicDemoTelemetry(request, fleet);

  await page.goto("/demo");
  await page.getByRole("button", { name: "Launch Live Demo" }).click();
  await expect(
    page.getByRole("heading", { name: "Operations cockpit" }),
  ).toBeVisible();
  await expect(page.getByLabel("Fleet indicators")).toContainText("12");
  await expect(page.locator('[aria-label^="DEMO-100:"]')).toHaveCount(1);
  await expect(
    page.getByRole("button", { name: /Mission DEMO-M-100 delayed/ }),
  ).toBeVisible();
  await capture(page, testInfo, "demo-cockpit-fleet.png");

  await page
    .getByRole("button", { name: /Mission DEMO-M-100 delayed/ })
    .click();
  await expect(
    page
      .locator(".cockpit-inspector")
      .getByRole("heading", { name: "DEMO-100" }),
  ).toBeVisible();
  await expect(page.locator(".cockpit-inspector")).toContainText("Demo Driver");
  await capture(page, testInfo, "demo-vehicle-exception.png");
});

test("capture virtual driver activity with an operations mission", async ({
  page,
  request,
}, testInfo) => {
  const reference = `NW-DEMO-${Date.now()}`;
  const mission = await createAssignedMission(request, reference);
  const operatorToken = await loginViaApi(
    request,
    "operator@northwind.local",
    "Operator123!",
  );
  const activity = await request.post(
    `${apiBaseUrl}/api/v1/demo/agent-activities`,
    {
      headers: { Authorization: `Bearer ${operatorToken}` },
      data: {
        agentId: mission.id,
        driverId: mission.driverId,
        vehicleId: mission.vehicleId,
        missionId: mission.id,
        stopId: mission.stops[0].id,
        sequence: 1,
        observedState: 3,
        policy: "schedule-delay",
        action: 5,
        resultCode: "delay-reported",
        resultMessage: "Controlled 15-minute delay recorded.",
        occurredAtUtc: new Date().toISOString(),
      },
    },
  );
  expect(activity.ok()).toBeTruthy();

  await page.goto("/login");
  await page.getByLabel("Email").fill("operator@northwind.local");
  await page.getByLabel("Password").fill("Operator123!");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/$/);

  const dock = page.getByRole("tablist", { name: "Cockpit activity" });
  await dock.getByRole("tab", { name: "Virtual drivers" }).click();
  await expect(page.getByText("ReportDelay · EnRoute").first()).toBeVisible();
  await capture(page, testInfo, "demo-agent-activity.png");
});

async function capture(
  page: import("@playwright/test").Page,
  testInfo: import("@playwright/test").TestInfo,
  fileName: string,
) {
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.waitForTimeout(800);
  const temporaryPath = testInfo.outputPath(fileName);
  await page.screenshot({
    path: temporaryPath,
    fullPage: true,
    animations: "disabled",
  });
  await copyWithRetry(temporaryPath, path.join(screenshotDirectory, fileName));
}

async function copyWithRetry(source: string, destination: string) {
  let lastError: unknown;
  for (let attempt = 0; attempt < 5; attempt += 1) {
    try {
      await copyFile(source, destination);
      return;
    } catch (error) {
      lastError = error;
      await new Promise((resolve) => setTimeout(resolve, 250));
    }
  }
  throw lastError;
}

async function loginViaApi(
  request: APIRequestContext,
  email: string,
  password: string,
) {
  const response = await request.post(`${apiBaseUrl}/api/auth/login`, {
    data: { email, password },
  });
  expect(response.ok()).toBeTruthy();
  const payload = await response.json();
  return payload.accessToken as string;
}

type PublicDemoFleet = {
  organizationId: string;
  vehicles: { vehicleId: string; deviceId: string }[];
};

async function loadPublicDemoFleet(request: APIRequestContext) {
  const response = await request.get(
    `${apiBaseUrl}/api/internal/v1/tracking/scenarios/public-demo?maxVehicles=20`,
    { headers: { "X-FleetOps-Internal-Key": internalApiKey } },
  );
  expect(response.ok()).toBeTruthy();
  return (await response.json()) as PublicDemoFleet;
}

async function pushPublicDemoTelemetry(
  request: APIRequestContext,
  fleet: PublicDemoFleet,
) {
  const base = Date.now();
  for (const [index, vehicle] of fleet.vehicles.entries()) {
    const response = await request.post(
      `${apiBaseUrl}/api/internal/v1/tracking/events`,
      {
        headers: { "X-FleetOps-Internal-Key": internalApiKey },
        data: {
          organizationId: fleet.organizationId,
          vehicleId: vehicle.vehicleId,
          deviceId: vehicle.deviceId,
          eventId: `demo-visual-${base}-${index}`,
          recordedAtUtc: new Date(base - index * 1000).toISOString(),
          latitude: 48.74 + index * 0.02,
          longitude: 9.1 + index * 0.02,
          speedKph: 32 + index,
          headingDegrees: 90 + index,
          sequenceNumber: index + 1,
          accuracyMeters: 5,
          source: "demo-engine",
        },
      },
    );
    expect(response.ok()).toBeTruthy();
  }
}

async function createAssignedMission(
  request: APIRequestContext,
  reference: string,
) {
  const operatorToken = await loginViaApi(
    request,
    "operator@northwind.local",
    "Operator123!",
  );
  const authorization = { Authorization: `Bearer ${operatorToken}` };
  const start = new Date(Date.now() + 48 * 60 * 60 * 1000);
  const end = new Date(start.getTime() + 2 * 60 * 60 * 1000);

  const missionResponse = await request.post(
    `${apiBaseUrl}/api/v1/dispatch/missions`,
    {
      headers: authorization,
      data: {
        reference,
        title: "Demo activity mission",
        scheduledStartUtc: start.toISOString(),
        scheduledEndUtc: end.toISOString(),
        stops: [
          {
            sequence: 1,
            name: "Depot",
            address: "1 Dispatch Way",
            plannedArrivalUtc: new Date(
              start.getTime() + 30 * 60 * 1000,
            ).toISOString(),
          },
          {
            sequence: 2,
            name: "Customer",
            address: "22 Fleet Street",
            plannedArrivalUtc: new Date(
              start.getTime() + 90 * 60 * 1000,
            ).toISOString(),
          },
        ],
      },
    },
  );
  expect(missionResponse.ok()).toBeTruthy();
  const mission = await missionResponse.json();

  const drivers = await (
    await request.get(`${apiBaseUrl}/api/v1/fleet/drivers`, {
      headers: authorization,
    })
  ).json();
  const vehicles = await (
    await request.get(`${apiBaseUrl}/api/v1/fleet/vehicles`, {
      headers: authorization,
    })
  ).json();
  const driver = drivers.find(
    (item: { licenseNumber: string }) => item.licenseNumber === "NW-DL-001",
  );
  const vehicle = vehicles.find(
    (item: { registrationNumber: string }) =>
      item.registrationNumber === "NW-100",
  );

  const planned = await request.post(
    `${apiBaseUrl}/api/v1/dispatch/missions/${mission.id}/status`,
    {
      headers: authorization,
      data: { targetStatus: 1, rowVersion: mission.rowVersion },
    },
  );
  expect(planned.ok()).toBeTruthy();
  const plannedMission = await planned.json();

  const assigned = await request.put(
    `${apiBaseUrl}/api/v1/dispatch/missions/${plannedMission.id}/assignment`,
    {
      headers: authorization,
      data: {
        driverId: driver.id,
        vehicleId: vehicle.id,
        rowVersion: plannedMission.rowVersion,
      },
    },
  );
  expect(assigned.ok()).toBeTruthy();
  const assignedMission = await assigned.json();

  const ready = await request.post(
    `${apiBaseUrl}/api/v1/dispatch/missions/${assignedMission.id}/status`,
    {
      headers: authorization,
      data: { targetStatus: 2, rowVersion: assignedMission.rowVersion },
    },
  );
  expect(ready.ok()).toBeTruthy();
  return ready.json();
}
