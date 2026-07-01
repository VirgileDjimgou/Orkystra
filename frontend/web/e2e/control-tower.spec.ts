import { test, expect } from '@playwright/test';
import { mockControlTowerOverview, mockProviderCatalog, mockTransportSyncStatus } from './mock-data';

test.describe('Control tower smoke checks', () => {

  test('loads and shows fallback overview', async ({ page }) => {
    await page.route('**/api/**', async (route) => {
      await route.fulfill({ status: 400, contentType: 'application/json', body: '{}' });
    });

    await page.goto('/');
    const body = page.locator('body');
    await expect(body).toContainText(/scenario|control|tower|truck|warehouse/i, { timeout: 30000 });
  });

  test('renders control tower sections with mocked API overview', async ({ page }) => {
    const overviewData = mockControlTowerOverview();
    await page.route('**/api/control-tower/overview', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(overviewData) });
    });
    await page.route('**/api/providers/catalog', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockProviderCatalog()) });
    });
    await page.route('**/api/transport/sync-status', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockTransportSyncStatus()) });
    });
    await page.route('**/api/gps/board', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ generatedAtUtc: new Date().toISOString(), positions: [], summary: { totalTrucks: 0, movingTrucks: 0, idleTrucks: 0, staleTrucks: 0, speedingTrucks: 0, attentionCount: 0, staleCount: 0, routeLinkedCount: 0, summary: 'No telemetry' }, focusTarget: null }) });
    });
    await page.route('**/api/warehouses/**', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ warehouseId: 'e2e-wh', name: 'E2E North Hub', storedPalletCount: 500, slotCount: 800, updatedAtLabel: '2026-07-01 12:00 UTC', zones: [], docks: [] }) });
    });
    await page.route('**/api/routes/**', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ routeId: 'e2e-rt-1', reference: 'E2E-RT-1', status: 'On time', stopCount: 5, shipmentCount: 8, completedDeliveryCount: 2, driverName: 'E2E Driver', truckReference: 'TRUCK-01', truckCapacityKilograms: 20000, totalLoadKilograms: 12000, updatedAtLabel: '2026-07-01 12:00 UTC', stops: [], shipments: [], deliveries: [], routeOptimization: null }) });
    });
    await page.route('**/api/ai/**', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ recommendation: null, assumptions: [], missingData: [] }) });
    });
    await page.route('**/api/observability/**', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ activities: [], generatedAtUtc: new Date().toISOString() }) });
    });

    await page.goto('/');
    await expect(page.locator('body')).toContainText(/E2E Test Scenario/i, { timeout: 30000 });
    await expect(page.locator('body')).toContainText(/E2E North Hub/i);
    await expect(page.locator('body')).toContainText(/E2E-RT-1/i);
  });

  test('renders fallback data when API is unreachable', async ({ page }) => {
    await page.route('**/api/**', async (route) => {
      await route.fulfill({ status: 400, contentType: 'application/json', body: '{}' });
    });

    await page.goto('/');
    const body = page.locator('body');
    await expect(body).toContainText(/scenario|control|tower|truck|warehouse/i, { timeout: 30000 });
  });
});
