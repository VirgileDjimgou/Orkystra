import {
  buildFallbackTransportSyncDiff,
  mapApiTransportSyncDiffToView,
  type TransportSyncDiffView,
} from '../data/controlTower'
import { sendApiRequest } from './apiClient'

export type TransportSyncDiffLoadResult = {
  diff: TransportSyncDiffView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

export async function loadTransportSyncDiff(): Promise<TransportSyncDiffLoadResult> {
  try {
    const response = await sendApiRequest('/api/transport/sync-diff', {
      includeTenantHeader: true,
    })

    if (!response.ok) {
      throw new Error(`Transport sync diff request failed with status ${response.status}`)
    }

    return {
      diff: mapApiTransportSyncDiffToView(await response.json()),
      source: 'api',
      errorMessage: null,
    }
  } catch (error) {
    return {
      diff: buildFallbackTransportSyncDiff(),
      source: 'fallback',
      errorMessage: error instanceof Error ? error.message : 'Transport sync diff request failed.',
    }
  }
}
