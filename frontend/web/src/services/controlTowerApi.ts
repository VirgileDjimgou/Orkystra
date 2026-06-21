import { buildFallbackOverview, mapApiOverviewToView, type ControlTowerOverviewView } from '../data/controlTower'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://127.0.0.1:5043'
const apiKey = import.meta.env.VITE_API_KEY ?? ''
const tenantId = import.meta.env.VITE_TENANT_ID ?? 'north-hub-demo'

export type ControlTowerOverviewLoadResult = {
  overview: ControlTowerOverviewView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

export async function loadControlTowerOverview(): Promise<ControlTowerOverviewLoadResult> {
  try {
    const headers: Record<string, string> = {
      'X-Tenant-Id': tenantId,
    }

    if (apiKey) {
      headers['X-Api-Key'] = apiKey
    }

    const response = await fetch(`${apiBaseUrl}/api/control-tower/overview`, {
      headers,
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
