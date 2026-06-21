import { buildFallbackRouteDetail, mapApiRouteDetailToView, type RouteDetailView } from '../data/controlTower'
import { sendApiRequest } from './apiClient'

export type TransportProjectionLoadResult = {
  route: RouteDetailView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

export async function loadTransportProjection(routeId: string): Promise<TransportProjectionLoadResult> {
  try {
    const response = await sendApiRequest(`/api/transport/routes/${routeId}`, {
      includeTenantHeader: true,
    })

    if (!response.ok) {
      throw new Error(`Transport API request failed with status ${response.status}`)
    }

    return {
      route: mapApiRouteDetailToView(await response.json()),
      source: 'api',
      errorMessage: null,
    }
  } catch (error) {
    return {
      route: buildFallbackRouteDetail(routeId),
      source: 'fallback',
      errorMessage: error instanceof Error ? error.message : 'Transport API request failed.',
    }
  }
}
