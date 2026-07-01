import { test, expect } from '@playwright/test';
import { mockControlTowerOverview, mockProviderCatalog } from './mock-data';

test.describe('Provider configuration smoke checks', () => {

  test('shows provider catalog with mocked data', async ({ page }) => {
    const overviewData = mockControlTowerOverview();
    await page.route('**/api/control-tower/overview', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(overviewData) });
    });
    await page.route('**/api/providers/catalog', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockProviderCatalog()) });
    });
    await page.route('**/api/transport/sync-status', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ providerId: 'e2e', source: 'demo-fallback', liveImport: false, hasPersistedSnapshot: false, importedRouteCount: 0, importedRouteIds: [], importedRouteReferences: [], lastImportedAtUtc: null, lastSuccessfulSyncAt: null, lastAttemptedSyncAt: null, syncStatus: 'idle', syncDetail: null, health: { providerId: 'e2e', providerName: 'E2E', status: 'Healthy', checkedAt: new Date().toISOString(), summary: 'OK', signals: [] } }) });
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
    const body = page.locator('body');
    await expect(body).toContainText(/REST Transport Adapter/i, { timeout: 30000 });
    await expect(body).toContainText(/api-key/i);
  });

  test('shows fallback provider catalog when API is unreachable', async ({ page }) => {
    await page.route('**/api/**', async (route) => {
      await route.fulfill({ status: 400, contentType: 'application/json', body: '{}' });
    });

    await page.goto('/');
    const body = page.locator('body');
    await expect(body).toContainText(/REST Transport Adapter/i, { timeout: 30000 });
  });
});
