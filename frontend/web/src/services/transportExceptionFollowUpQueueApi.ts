import {
  buildFallbackTransportExceptionFollowUpQueue,
  mapApiTransportExceptionFollowUpQueueToView,
  type TransportExceptionFollowUpQueueView,
} from '../data/controlTower'
import { sendApiRequest } from './apiClient'

export type TransportExceptionFollowUpQueueLoadResult = {
  queue: TransportExceptionFollowUpQueueView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

export async function loadTransportExceptionFollowUpQueue(): Promise<TransportExceptionFollowUpQueueLoadResult> {
  try {
    const response = await sendApiRequest('/api/transport/exceptions-workbench/follow-up-queue', {
      includeTenantHeader: true,
    })

    if (!response.ok) {
      throw new Error(`Transport exception follow-up queue request failed with status ${response.status}`)
    }

    return {
      queue: mapApiTransportExceptionFollowUpQueueToView(await response.json()),
      source: 'api',
      errorMessage: null,
    }
  } catch (error) {
    return {
      queue: buildFallbackTransportExceptionFollowUpQueue(),
      source: 'fallback',
      errorMessage:
        error instanceof Error
          ? error.message
          : 'Transport exception follow-up queue request failed.',
    }
  }
}
