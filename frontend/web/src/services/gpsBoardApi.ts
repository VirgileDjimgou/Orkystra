import {
  buildFallbackGpsFleetBoard,
  mapApiGpsFleetBoardToView,
  type GpsFleetBoardView,
} from '../data/controlTower'
import { sendApiRequest } from './apiClient'

export type GpsFleetBoardLoadResult = {
  board: GpsFleetBoardView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

async function readGpsFleetBoard(
  path: string,
  method: 'GET' | 'POST'
): Promise<GpsFleetBoardLoadResult> {
  try {
    const response = await sendApiRequest(path, {
      includeTenantHeader: true,
      method,
    })

    if (!response.ok) {
      throw new Error(`GPS fleet request failed with status ${response.status}`)
    }

    const payload =
      method === 'POST'
        ? await sendApiRequest('/api/gps/board', {
            includeTenantHeader: true,
            method: 'GET',
          })
        : response

    if (!payload.ok) {
      throw new Error(`GPS fleet board refresh failed with status ${payload.status}`)
    }

    return {
      board: mapApiGpsFleetBoardToView(await payload.json()),
      source: 'api',
      errorMessage: null,
    }
  } catch (error) {
    return {
      board: buildFallbackGpsFleetBoard(),
      source: 'fallback',
      errorMessage: error instanceof Error ? error.message : 'GPS fleet request failed.',
    }
  }
}

export function loadGpsFleetBoard(): Promise<GpsFleetBoardLoadResult> {
  return readGpsFleetBoard('/api/gps/board', 'GET')
}

export function publishGpsFleetTelemetry(): Promise<GpsFleetBoardLoadResult> {
  return readGpsFleetBoard('/api/gps/positions/publish', 'POST')
}
