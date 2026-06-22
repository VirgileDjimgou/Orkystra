import {
  buildFallbackTransportExceptionWorkbench,
  mapApiTransportExceptionWorkbenchToView,
  type TransportExceptionWorkbenchView,
} from '../data/controlTower'
import { sendApiRequest } from './apiClient'

export type TransportExceptionWorkbenchLoadResult = {
  workbench: TransportExceptionWorkbenchView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

export async function loadTransportExceptionWorkbench(): Promise<TransportExceptionWorkbenchLoadResult> {
  try {
    const response = await sendApiRequest('/api/transport/exceptions-workbench', {
      includeTenantHeader: true,
    })

    if (!response.ok) {
      throw new Error(`Transport exception workbench request failed with status ${response.status}`)
    }

    return {
      workbench: mapApiTransportExceptionWorkbenchToView(await response.json()),
      source: 'api',
      errorMessage: null,
    }
  } catch (error) {
    return {
      workbench: buildFallbackTransportExceptionWorkbench(),
      source: 'fallback',
      errorMessage: error instanceof Error ? error.message : 'Transport exception workbench request failed.',
    }
  }
}
