import { buildFallbackWarehouseDetail, mapApiWarehouseDetailToView, type WarehouseDetailView } from '../data/controlTower'
import { sendApiRequest } from './apiClient'

export type WarehouseProjectionLoadResult = {
  warehouse: WarehouseDetailView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

export async function loadWarehouseProjection(warehouseId: string): Promise<WarehouseProjectionLoadResult> {
  try {
    const response = await sendApiRequest(`/api/warehouses/${warehouseId}`, {
      includeTenantHeader: true,
    })

    if (!response.ok) {
      throw new Error(`Warehouse API request failed with status ${response.status}`)
    }

    return {
      warehouse: mapApiWarehouseDetailToView(await response.json()),
      source: 'api',
      errorMessage: null,
    }
  } catch (error) {
    return {
      warehouse: buildFallbackWarehouseDetail(warehouseId),
      source: 'fallback',
      errorMessage: error instanceof Error ? error.message : 'Warehouse API request failed.',
    }
  }
}
