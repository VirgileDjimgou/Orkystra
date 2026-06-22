import {
  buildFallbackTransportExceptionResolutionHistory,
  mapApiTransportExceptionResolutionHistoryToView,
  type TransportExceptionResolutionHistoryView,
} from '../data/controlTower'
import { sendApiRequest } from './apiClient'

export type TransportExceptionResolutionHistoryLoadResult = {
  history: TransportExceptionResolutionHistoryView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

export async function loadTransportExceptionResolutionHistory(
  count = 12
): Promise<TransportExceptionResolutionHistoryLoadResult> {
  try {
    const response = await sendApiRequest(
      `/api/transport/exceptions-workbench/resolution-history?count=${count}`,
      {
        includeTenantHeader: true,
      }
    )

    if (!response.ok) {
      throw new Error(`Transport exception resolution history request failed with status ${response.status}`)
    }

    return {
      history: mapApiTransportExceptionResolutionHistoryToView(await response.json()),
      source: 'api',
      errorMessage: null,
    }
  } catch (error) {
    return {
      history: buildFallbackTransportExceptionResolutionHistory(),
      source: 'fallback',
      errorMessage:
        error instanceof Error
          ? error.message
          : 'Transport exception resolution history request failed.',
    }
  }
}
