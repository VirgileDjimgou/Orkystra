import { buildFallbackOverview, mapApiOverviewToView, type ControlTowerOverviewView } from '../data/controlTower'
import { sendApiRequest } from './apiClient'

export type ControlTowerOverviewLoadResult = {
  overview: ControlTowerOverviewView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

export async function loadControlTowerOverview(): Promise<ControlTowerOverviewLoadResult> {
  try {
    const response = await sendApiRequest('/api/control-tower/overview', {
      includeTenantHeader: true,
    })

    if (!response.ok) {
      throw new Error(`Control tower API request failed with status ${response.status}`)
    }

    return {
      overview: mapApiOverviewToView(await response.json()),
      source: 'api',
      errorMessage: null,
    }
  } catch (error) {
    return {
      overview: buildFallbackOverview(),
      source: 'fallback',
      errorMessage: error instanceof Error ? error.message : 'Control tower API request failed.',
    }
  }
}
