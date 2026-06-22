import {
  buildFallbackTransportSyncStatus,
  mapApiTransportSyncStatusToView,
  type TransportSyncStatusView,
} from '../data/controlTower'
import { sendApiRequest } from './apiClient'

export type TransportSyncLoadResult = {
  status: TransportSyncStatusView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

async function readTransportSync(path: string, method: 'GET' | 'POST'): Promise<TransportSyncLoadResult> {
  try {
    const response = await sendApiRequest(path, {
      includeTenantHeader: true,
      method,
    })

    if (!response.ok) {
      throw new Error(`Transport sync request failed with status ${response.status}`)
    }

    return {
      status: mapApiTransportSyncStatusToView(await response.json()),
      source: 'api',
      errorMessage: null,
    }
  } catch (error) {
    return {
      status: buildFallbackTransportSyncStatus(),
      source: 'fallback',
      errorMessage: error instanceof Error ? error.message : 'Transport sync request failed.',
    }
  }
}

export function loadTransportSyncStatus(): Promise<TransportSyncLoadResult> {
  return readTransportSync('/api/transport/sync-status', 'GET')
}

export function runTransportSync(): Promise<TransportSyncLoadResult> {
  return readTransportSync('/api/transport/sync', 'POST')
}
