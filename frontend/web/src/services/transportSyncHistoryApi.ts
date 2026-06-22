import {
  buildFallbackTransportSyncHistory,
  mapApiTransportSyncHistoryToView,
  type TransportSyncHistoryView,
} from '../data/controlTower'
import { sendApiRequest } from './apiClient'

export type TransportSyncHistoryLoadResult = {
  history: TransportSyncHistoryView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

export async function loadTransportSyncHistory(count = 6): Promise<TransportSyncHistoryLoadResult> {
  try {
    const response = await sendApiRequest(`/api/transport/sync-history?count=${count}`, {
      includeTenantHeader: true,
    })

    if (!response.ok) {
      throw new Error(`Transport sync history request failed with status ${response.status}`)
    }

    return {
      history: mapApiTransportSyncHistoryToView(await response.json()),
      source: 'api',
      errorMessage: null,
    }
  } catch (error) {
    return {
      history: buildFallbackTransportSyncHistory(),
      source: 'fallback',
      errorMessage: error instanceof Error ? error.message : 'Transport sync history request failed.',
    }
  }
}
