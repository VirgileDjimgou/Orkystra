import {
  buildFallbackRouteOptimization,
  mapApiRouteOptimizationToView,
  type RouteDetailView,
  type RouteOptimizationView,
} from '../data/controlTower'
import { sendApiRequest } from './apiClient'

export type RouteOptimizationLoadResult = {
  optimization: RouteOptimizationView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

export async function loadRouteOptimization(route: RouteDetailView, scenarioId: string): Promise<RouteOptimizationLoadResult> {
  try {
    const response = await sendApiRequest(`/api/transport/routes/${route.routeId}/optimization`, {
      method: 'POST',
      includeTenantHeader: true,
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        scenarioId,
      }),
    })

    if (!response.ok) {
      throw new Error(`Route optimization request failed with status ${response.status}`)
    }

    return mapApiRouteOptimizationToView(await response.json())
  } catch (error) {
    return {
      optimization: buildFallbackRouteOptimization(route),
      source: 'fallback',
      errorMessage: error instanceof Error ? error.message : 'Route optimization request failed.',
    }
  }
}
