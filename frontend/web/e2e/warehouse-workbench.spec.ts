import { test, expect } from '@playwright/test';
import { mockControlTowerOverview, mockWarehouseWorkbench } from './mock-data';

test.describe('Warehouse workbench smoke checks', () => {

  test('shows warehouse workbench with mocked data', async ({ page }) => {
    const overviewData = mockControlTowerOverview();
    await page.route('**/api/control-tower/overview', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(overviewData) });
    });
    await page.route('**/api/warehouses/workbench', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockWarehouseWorkbench()) });
    });
    await page.route('**/api/**', async (route) => {
      await route.fulfill({ status: 400, contentType: 'application/json', body: '{}' });
    });
    await page.route('**/api/warehouses/**', (route) => {
      if (route.request().url().includes('/workbench')) return;
      return route.fulfill({ status: 400 });
    });

    await page.goto('/');
    await expect(page.locator('body')).toContainText(/Warehouse signals/i, { timeout: 30000 });
    await expect(page.locator('body')).toContainText(/Zone XDK/i);
    await expect(page.locator('body')).toContainText(/Critical/i);
  });

  test('shows fallback warehouse workbench when API is unreachable', async ({ page }) => {
    await page.route('**/api/**', async (route) => {
      await route.fulfill({ status: 400, contentType: 'application/json', body: '{}' });
    });

    await page.goto('/');
    await expect(page.locator('body')).toContainText(/Warehouse signals/i, { timeout: 30000 });
    await expect(page.locator('body')).toContainText(/Focus warehouse/i);
  });
});
