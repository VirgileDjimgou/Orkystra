import { buildFallbackProviderCatalog, mapApiProviderCatalogToView, type ProviderCatalogView } from '../data/controlTower'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://127.0.0.1:5043'
const apiKey = import.meta.env.VITE_API_KEY ?? ''

export async function loadProviderCatalog(): Promise<ProviderCatalogView> {
  try {
    const headers: Record<string, string> = {}

    if (apiKey) {
      headers['X-Api-Key'] = apiKey
    }

    const response = await fetch(`${apiBaseUrl}/api/providers/catalog`, {
      headers,
    })

    if (!response.ok) {
      throw new Error(`Provider catalog request failed with status ${response.status}`)
    }

    return mapApiProviderCatalogToView(await response.json())
  } catch {
    return buildFallbackProviderCatalog()
  }
}
