import { expect, test } from "@playwright/test";
import { copyFile } from "node:fs/promises";
import path from "node:path";

const screenshotDirectory = path.resolve(
  process.cwd(),
  "../../docs/assets/screenshots",
);

const captures = [
  {
    email: "operator@northwind.local",
    password: "Operator123!",
    organization: "Northwind Logistics",
    route: "/dispatch/missions",
    heading: "Mission control board",
    file: "simulation-northwind-operator-dispatch.png",
  },
  {
    email: "admin@northwind.local",
    password: "Admin123!",
    organization: "Northwind Logistics",
    route: "/admin/pilot",
    heading: "Pilot review",
    file: "simulation-northwind-admin-pilot-review.png",
  },
  {
    email: "admin@southridge.local",
    password: "Admin123!",
    organization: "Southridge Transport",
    route: "/map",
    heading: "Fleet map",
    file: "simulation-southridge-live-map.png",
    readyText: "SR-200",
  },
  {
    email: "operator@westland.local",
    password: "Operator123!",
    organization: "Westland Field Services",
    route: "/",
    heading: "Operations center",
    file: "simulation-westland-operations-center.png",
  },
] as const;

for (const capture of captures) {
  test(`capture ${capture.organization} ${capture.heading}`, async ({
    page,
  }, testInfo) => {
    await page.goto("/login");
    await page.getByLabel("Email").fill(capture.email);
    await page.getByLabel("Password").fill(capture.password);
    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(page).toHaveURL(/\/$/);

    await page.goto(capture.route);
    await expect(page.locator(".brand-subtitle")).toHaveText(
      capture.organization,
    );
    await expect(
      page.getByRole("heading", { name: capture.heading, exact: true }),
    ).toBeVisible();
    if ("readyText" in capture) {
      await expect(
        page.getByText(capture.readyText, { exact: true }).first(),
      ).toBeVisible();
      await expect(
        page.getByText("Live stream", { exact: true }),
      ).toBeVisible();
    }
    const temporaryPath = testInfo.outputPath(capture.file);
    await page.screenshot({
      path: temporaryPath,
      fullPage: true,
      animations: "disabled",
    });
    await copyWithRetry(
      temporaryPath,
      path.join(screenshotDirectory, capture.file),
    );
  });
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
