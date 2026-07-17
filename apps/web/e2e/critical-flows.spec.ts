import type { APIRequestContext, Page } from "@playwright/test";
import { expect, test } from "@playwright/test";

const apiBaseUrl =
  process.env.PLAYWRIGHT_API_BASE_URL ?? "http://127.0.0.1:5080";
const samplePngBase64 =
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9pN96ZQAAAAASUVORK5CYII=";

test("operator can sign in and reach the tenant-aware overview", async ({
  page,
}) => {
  await loginViaUi(page, "operator@northwind.local", "Operator123!");

  await expect(
    page.getByRole("heading", { name: "Operations overview" }),
  ).toBeVisible();
  await expect(page.locator(".brand-subtitle")).toHaveText(
    "Northwind Logistics",
  );
  await expect(page.getByRole("link", { name: "Mission board" })).toBeVisible();
});

test("operator can create, assign, and progress a mission in the dispatch board", async ({
  page,
}) => {
  await loginViaUi(page, "operator@northwind.local", "Operator123!");
  await page.goto("/dispatch/missions");

  const reference = `NW-E2E-${Date.now()}`;
  const schedule = buildFutureSchedule(reference);
  await page.getByPlaceholder("Mission reference").fill(reference);
  await page
    .getByPlaceholder("Mission title")
    .fill("Playwright dispatch proof");
  const scheduleInputs = page.locator('input[type="datetime-local"]');
  await scheduleInputs.nth(0).fill(schedule.scheduledStartUtcLocal);
  await scheduleInputs.nth(1).fill(schedule.scheduledEndUtcLocal);
  await scheduleInputs.nth(2).fill(schedule.firstStopUtcLocal);
  await scheduleInputs.nth(3).fill(schedule.secondStopUtcLocal);
  await page.getByRole("button", { name: "Create mission" }).click();

  await expect(
    page.getByText(`Mission ${reference} created in Draft.`),
  ).toBeVisible();
  await expect(
    page.getByText(`Timeline and controls for ${reference}`),
  ).toBeVisible();

  const assignmentPanel = page.locator(".dispatch-inline-panel").filter({
    has: page.getByRole("heading", { name: "Assignment" }),
  });
  const selects = assignmentPanel.getByRole("combobox");
  await selects.nth(0).selectOption({ label: "Alex North" });
  await selects.nth(1).selectOption({ label: "NW-100" });
  await page.getByRole("button", { name: "Save assignment" }).click();
  await expect(
    page.getByText(`Mission ${reference} assignment updated.`),
  ).toBeVisible();

  await page.getByRole("button", { name: "Move to Planned" }).click();
  await expect(
    page.getByText(`Mission ${reference} moved to Planned.`),
  ).toBeVisible();

  await page.getByRole("button", { name: "Move to Assigned" }).click();
  await expect(
    page.getByText(`Mission ${reference} moved to Assigned.`),
  ).toBeVisible();
  await expect(
    page.locator(".metric-card").filter({ hasText: /^Driver/ }),
  ).toContainText("Alex North");
  await expect(
    page.locator(".metric-card").filter({ hasText: /^Vehicle/ }),
  ).toContainText("NW-100");
});

test("operator can review a delivery proof prepared by the driver workflow", async ({
  page,
  request,
}) => {
  const mission = await createMissionWithProof(
    request,
    `NW-PROOF-${Date.now()}`,
  );

  await loginViaUi(page, "operator@northwind.local", "Operator123!");
  await page.goto("/dispatch/missions");
  await page
    .locator(".user-card[role='button']")
    .filter({ hasText: mission.reference })
    .click();

  await expect(
    page
      .locator(".dispatch-stop-card")
      .filter({ hasText: "Taylor Receiver" })
      .first(),
  ).toBeVisible();
  await expect(page.getByText("Signed by Taylor Receiver")).toBeVisible();
  await expect(page.getByRole("link", { name: "Demo capture" })).toBeVisible();
});

test("a second tenant cannot discover a Northwind mission in the dispatch board", async ({
  page,
  request,
}) => {
  const reference = `NW-ISO-${Date.now()}`;
  await createAssignedMission(request, reference);

  await loginViaUi(page, "admin@southridge.local", "Admin123!");
  await page.goto("/dispatch/missions");

  await expect(page.locator(".brand-subtitle")).toHaveText(
    "Southridge Transport",
  );
  await expect(page.getByText(reference)).toHaveCount(0);
});

async function loginViaUi(page: Page, email: string, password: string) {
  await page.goto("/login");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/$/);
}

async function createMissionWithProof(
  request: APIRequestContext,
  reference: string,
) {
  const mission = await createAssignedMission(request, reference);
  const driverToken = await loginViaApi(
    request,
    "driver@northwind.local",
    "Driver123!",
  );
  const uploadSession = await request.post(
    `${apiBaseUrl}/api/v1/driver/uploads/sessions`,
    {
      headers: { Authorization: `Bearer ${driverToken}` },
      data: {
        fileName: "proof-demo.png",
        contentType: "image/png",
        totalBytes: Buffer.from(samplePngBase64, "base64").byteLength,
        purpose: 2,
      },
    },
  );
  expect(uploadSession.ok()).toBeTruthy();
  const upload = await uploadSession.json();

  const appendChunk = await request.post(
    `${apiBaseUrl}/api/v1/driver/uploads/sessions/${upload.uploadSessionId}/chunks`,
    {
      headers: { Authorization: `Bearer ${driverToken}` },
      data: {
        offset: 0,
        base64Content: samplePngBase64,
      },
    },
  );
  expect(appendChunk.ok()).toBeTruthy();

  const completeUpload = await request.post(
    `${apiBaseUrl}/api/v1/driver/uploads/sessions/${upload.uploadSessionId}/complete`,
    {
      headers: { Authorization: `Bearer ${driverToken}` },
    },
  );
  expect(completeUpload.ok()).toBeTruthy();
  const asset = await completeUpload.json();

  const proofResponse = await request.post(
    `${apiBaseUrl}/api/v1/driver/missions/${mission.id}/stops/${mission.stops[1].id}/proof`,
    {
      headers: { Authorization: `Bearer ${driverToken}` },
      data: {
        commandId: `proof-${reference}`,
        recipientName: "Taylor Receiver",
        signatureName: "Taylor Receiver",
        deliveredAtUtc: new Date().toISOString(),
        notes: "Delivered during Sprint 11 proof flow.",
        photos: [{ mediaAssetId: asset.assetId, caption: "Demo capture" }],
      },
    },
  );
  expect(proofResponse.ok()).toBeTruthy();

  return mission;
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

  const digits = Number(reference.replace(/\D/g, "").slice(-6)) || 0;
  const offsetHours = 12 + (digits % 120);
  const start = new Date(Date.now() + offsetHours * 60 * 60 * 1000);
  const end = new Date(start.getTime() + 2 * 60 * 60 * 1000);
  const missionResponse = await request.post(
    `${apiBaseUrl}/api/v1/dispatch/missions`,
    {
      headers: { Authorization: `Bearer ${operatorToken}` },
      data: {
        reference,
        title: "Sprint 11 browser proof",
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

  const driversResponse = await request.get(
    `${apiBaseUrl}/api/v1/fleet/drivers`,
    {
      headers: { Authorization: `Bearer ${operatorToken}` },
    },
  );
  const vehiclesResponse = await request.get(
    `${apiBaseUrl}/api/v1/fleet/vehicles`,
    {
      headers: { Authorization: `Bearer ${operatorToken}` },
    },
  );
  const drivers = await driversResponse.json();
  const vehicles = await vehiclesResponse.json();
  const driver = drivers.find(
    (item: { licenseNumber: string }) => item.licenseNumber === "NW-DL-001",
  );
  const vehicle = vehicles.find(
    (item: { registrationNumber: string }) =>
      item.registrationNumber === "NW-100",
  );

  const plannedResponse = await request.post(
    `${apiBaseUrl}/api/v1/dispatch/missions/${mission.id}/status`,
    {
      headers: { Authorization: `Bearer ${operatorToken}` },
      data: {
        targetStatus: 1,
        rowVersion: mission.rowVersion,
      },
    },
  );
  expect(plannedResponse.ok()).toBeTruthy();
  const planned = await plannedResponse.json();

  const assignmentResponse = await request.put(
    `${apiBaseUrl}/api/v1/dispatch/missions/${planned.id}/assignment`,
    {
      headers: { Authorization: `Bearer ${operatorToken}` },
      data: {
        driverId: driver.id,
        vehicleId: vehicle.id,
        rowVersion: planned.rowVersion,
      },
    },
  );
  expect(assignmentResponse.ok()).toBeTruthy();
  const assigned = await assignmentResponse.json();

  const assignedStatusResponse = await request.post(
    `${apiBaseUrl}/api/v1/dispatch/missions/${assigned.id}/status`,
    {
      headers: { Authorization: `Bearer ${operatorToken}` },
      data: {
        targetStatus: 2,
        rowVersion: assigned.rowVersion,
      },
    },
  );
  expect(assignedStatusResponse.ok()).toBeTruthy();
  return assignedStatusResponse.json();
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

function buildFutureSchedule(reference: string) {
  const digits = Number(reference.replace(/\D/g, "").slice(-6)) || 0;
  const offsetHours = 12 + (digits % 120);
  const start = new Date(Date.now() + offsetHours * 60 * 60 * 1000);
  const end = new Date(start.getTime() + 2 * 60 * 60 * 1000);

  return {
    scheduledStartUtcLocal: toLocalDateTimeInputValue(start),
    scheduledEndUtcLocal: toLocalDateTimeInputValue(end),
    firstStopUtcLocal: toLocalDateTimeInputValue(
      new Date(start.getTime() + 30 * 60 * 1000),
    ),
    secondStopUtcLocal: toLocalDateTimeInputValue(
      new Date(start.getTime() + 90 * 60 * 1000),
    ),
  };
}

function toLocalDateTimeInputValue(value: Date) {
  return new Date(value.getTime() - value.getTimezoneOffset() * 60000)
    .toISOString()
    .slice(0, 16);
}
