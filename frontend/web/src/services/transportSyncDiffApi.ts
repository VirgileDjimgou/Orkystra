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

export async function loadTransportSyncDiff(
  previousRunId?: number,
  currentRunId?: number,
): Promise<TransportSyncDiffLoadResult> {
  try {
    let url = '/api/transport/sync-diff'

    if (previousRunId !== undefined && currentRunId !== undefined) {
      url += `?previousRunId=${previousRunId}&currentRunId=${currentRunId}`
    }

    const response = await sendApiRequest(url, {
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
