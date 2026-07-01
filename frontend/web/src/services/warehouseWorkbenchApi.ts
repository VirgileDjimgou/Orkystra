import {
  buildFallbackWarehouseWorkbench,
  mapApiWarehouseWorkbenchToView,
  type WarehouseWorkbenchView,
} from '../data/controlTower'
import { sendApiRequest } from './apiClient'

export type WarehouseWorkbenchLoadResult = {
  workbench: WarehouseWorkbenchView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

export async function loadWarehouseWorkbench(): Promise<WarehouseWorkbenchLoadResult> {
  try {
    const response = await sendApiRequest('/api/warehouses/workbench', {
      includeTenantHeader: true,
    })

    if (!response.ok) {
      throw new Error(`Warehouse workbench request failed with status ${response.status}`)
    }

    return {
      workbench: mapApiWarehouseWorkbenchToView(await response.json()),
      source: 'api',
      errorMessage: null,
    }
  } catch (error) {
    return {
      workbench: buildFallbackWarehouseWorkbench(),
      source: 'fallback',
      errorMessage: error instanceof Error ? error.message : 'Warehouse workbench request failed.',
    }
  }
}
