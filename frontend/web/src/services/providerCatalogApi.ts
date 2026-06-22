import { buildFallbackProviderCatalog, mapApiProviderCatalogToView, type ProviderCatalogView } from '../data/controlTower'
import { sendApiRequest } from './apiClient'

export type ProviderCatalogLoadResult = {
  catalog: ProviderCatalogView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

export type UpdateProviderConfigurationInput = {
  providerId: string
  enabled: boolean
  environment: string
  settings: Record<string, string>
}

export type UpdateProviderSecretInput = {
  providerId: string
  secretKey: string
  secretValue: string
}

export async function loadProviderCatalog(): Promise<ProviderCatalogLoadResult> {
  try {
    const response = await sendApiRequest('/api/providers/catalog', {
      includeTenantHeader: true,
    })

    if (!response.ok) {
      throw new Error(`Provider catalog request failed with status ${response.status}`)
    }

    return {
      catalog: mapApiProviderCatalogToView(await response.json()),
      source: 'api',
      errorMessage: null,
    }
  } catch (error) {
    return {
      catalog: buildFallbackProviderCatalog(),
      source: 'fallback',
      errorMessage: error instanceof Error ? error.message : 'Provider catalog request failed.',
    }
  }
}

export async function updateProviderConfiguration(input: UpdateProviderConfigurationInput): Promise<void> {
  const response = await sendApiRequest(`/api/providers/catalog/${input.providerId}/configuration`, {
    method: 'PUT',
    includeTenantHeader: true,
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      enabled: input.enabled,
      environment: input.environment,
      settings: input.settings,
    }),
  })

  if (!response.ok) {
    throw new Error(`Provider configuration update failed with status ${response.status}`)
  }
}

export async function updateProviderSecret(input: UpdateProviderSecretInput): Promise<void> {
  const response = await sendApiRequest(`/api/providers/catalog/${input.providerId}/secrets`, {
    method: 'PUT',
    includeTenantHeader: true,
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      secretKey: input.secretKey,
      secretValue: input.secretValue,
    }),
  })

  if (!response.ok) {
    throw new Error(`Provider secret update failed with status ${response.status}`)
  }
}
