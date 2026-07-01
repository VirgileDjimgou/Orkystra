import { test, expect } from '@playwright/test';
import { mockControlTowerOverview, mockTransportSyncStatus, mockGpsBoard } from './mock-data';

test.describe('Transport sync and GPS board smoke checks', () => {

  test('shows transport sync status with mocked API data', async ({ page }) => {
    const overviewData = mockControlTowerOverview();
    await page.route('**/api/control-tower/overview', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(overviewData) });
    });
    await page.route('**/api/transport/sync-status', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockTransportSyncStatus()) });
    });
    await page.route('**/api/providers/catalog', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ generatedAtUtc: new Date().toISOString(), providers: [] }) });
    });
    await page.route('**/api/gps/board', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockGpsBoard()) });
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
    const body = page.locator('body');
    await expect(body).toContainText(/E2E-RT-1/i, { timeout: 30000 });
    await expect(body).toContainText(/TRUCK-01/i);
  });

  test('falls back gracefully when transport sync API is down', async ({ page }) => {
    const overviewData = mockControlTowerOverview();
    await page.route('**/api/transport/sync-status', async (route) => {
      await route.fulfill({ status: 400, contentType: 'application/json', body: '{}' });
    });
    await page.route('**/api/gps/board', async (route) => {
      await route.fulfill({ status: 400, contentType: 'application/json', body: '{}' });
    });
    await page.route('**/api/control-tower/overview', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(overviewData) });
    });
    await page.route('**/api/providers/catalog', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ generatedAtUtc: new Date().toISOString(), providers: [] }) });
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
    const body = page.locator('body');
    await expect(body).toContainText(/scenario|control|tower|truck|warehouse/i, { timeout: 30000 });
    await expect(body).not.toContainText(/error/i);
  });
});