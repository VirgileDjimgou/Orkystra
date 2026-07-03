import {
  buildFallbackTransportSyncHistory,
  buildFallbackTransportSyncImportDetail,
  mapApiTransportSyncHistoryToView,
  mapApiTransportSyncImportDetailToView,
  type TransportSyncHistoryView,
  type TransportSyncImportDetailView,
} from '../data/controlTower'
import { sendApiRequest } from './apiClient'

export type TransportSyncHistoryLoadResult = {
  history: TransportSyncHistoryView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

export type TransportSyncImportDetailLoadResult = {
  detail: TransportSyncImportDetailView | null
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

export async function loadTransportSyncImportDetail(runId: number): Promise<TransportSyncImportDetailLoadResult> {
  try {
    const response = await sendApiRequest(`/api/transport/sync-history/${runId}`, {
      includeTenantHeader: true,
    })

    if (response.status === 404) {
      return {
        detail: null,
        source: 'api',
        errorMessage: 'Import run not found.',
      }
    }

    if (!response.ok) {
      throw new Error(`Transport sync import detail request failed with status ${response.status}`)
    }

    return {
      detail: mapApiTransportSyncImportDetailToView(await response.json()),
      source: 'api',
      errorMessage: null,
    }
  } catch (error) {
    return {
      detail: buildFallbackTransportSyncImportDetail(),
      source: 'fallback',
      errorMessage: error instanceof Error ? error.message : 'Transport sync import detail request failed.',
    }
  }
}
