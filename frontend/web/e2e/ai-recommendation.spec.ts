import { test, expect } from '@playwright/test';
import { mockControlTowerOverview, mockProviderCatalog, mockTransportSyncStatus } from './mock-data';

function createMockAiRecommendation(providerName: string) {
  return {
    recommendation: {
      intent: 'warehouse',
      directAnswer: 'The clearest warehouse pressure point is E2E North Hub.',
      evidence: [
        { source: 'warehouse_summary_projection', detail: 'E2E North Hub is using 215 of 480 slots (45%).', grounding: 'projection_data' },
        { source: 'dock_projection', detail: '2 occupied docks reported.', grounding: 'projection_data' },
      ],
      assumptions: ['Dock pressure cannot be estimated reliably.'],
      recommendedActions: [
        { title: 'Monitor E2E North Hub capacity', rationale: 'Occupancy is rising.', priority: 'medium' },
      ],
      confidenceLevel: 'medium',
      alternativeScenarioNote: 'Run a what-if scenario with adjusted inbound flow.',
      missingData: [],
      specialistAgents: ['warehouse-agent'],
    },
    source: 'api',
    errorMessage: null,
    providerName,
  };
}

test.describe('AI recommendation panel smoke checks', () => {

  test.beforeEach(async ({ page }) => {
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
    await page.route('**/api/observability/**', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ activities: [], generatedAtUtc: new Date().toISOString() }) });
    });
  });

  test('shows provider name in AI recommendation panel when API returns recommendation', async ({ page }) => {
    await page.route('**/api/ai/recommendations', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(createMockAiRecommendation('local')) });
    });

    await page.goto('/');

    await expect(page.locator('body')).toContainText(/Provider:/i, { timeout: 30000 });
    await expect(page.locator('body')).toContainText(/local/i);
  });

  test('shows http provider name when backend uses http provider', async ({ page }) => {
    await page.route('**/api/ai/recommendations', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(createMockAiRecommendation('http')) });
    });

    await page.goto('/');

    await expect(page.locator('body')).toContainText(/http/i, { timeout: 30000 });
  });

  test('shows grounding label on evidence items', async ({ page }) => {
    await page.route('**/api/ai/recommendations', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(createMockAiRecommendation('local')) });
    });

    await page.goto('/');

    await expect(page.locator('body')).toContainText(/Grounding:/i, { timeout: 30000 });
    await expect(page.locator('body')).toContainText(/projection_data/i);
  });

  test('shows fallback message when AI API is unreachable', async ({ page }) => {
    await page.route('**/api/ai/recommendations', async (route) => {
      await route.fulfill({ status: 500, contentType: 'application/json', body: '{}' });
    });

    await page.goto('/');

    const body = page.locator('body');
    await expect(body).toContainText(/unreachable|fallback|configured/i, { timeout: 30000 });
  });
});
