const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://127.0.0.1:5043'
const apiKey = import.meta.env.VITE_API_KEY ?? ''
const tenantId = import.meta.env.VITE_TENANT_ID ?? 'north-hub-demo'

type ApiRequestOptions = {
  body?: BodyInit
  headers?: Record<string, string>
  includeTenantHeader?: boolean
  method?: 'GET' | 'PUT'
  retryCount?: number
  timeoutMs?: number
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => window.setTimeout(resolve, ms))
}

function buildHeaders(includeTenantHeader: boolean, headers: Record<string, string> = {}): Record<string, string> {
  const nextHeaders: Record<string, string> = { ...headers }

  if (apiKey) {
    nextHeaders['X-Api-Key'] = apiKey
  }

  if (includeTenantHeader && tenantId) {
    nextHeaders['X-Tenant-Id'] = tenantId
  }

  return nextHeaders
}

function shouldRetry(status: number): boolean {
  return status === 408 || status === 425 || status === 429 || status >= 500
}

export async function sendApiRequest(path: string, options: ApiRequestOptions = {}): Promise<Response> {
  const {
    body,
    headers,
    includeTenantHeader = false,
    method = 'GET',
    retryCount = 4,
    timeoutMs = 5000,
  } = options

  for (let attempt = 0; attempt <= retryCount; attempt += 1) {
    const controller = new AbortController()
    const timeoutHandle = window.setTimeout(() => controller.abort(), timeoutMs)

    try {
      const response = await fetch(`${apiBaseUrl}${path}`, {
        method,
        headers: buildHeaders(includeTenantHeader, headers),
        body,
        signal: controller.signal,
      })

      if (response.ok || attempt === retryCount || !shouldRetry(response.status)) {
        return response
      }
    } catch (error) {
      if (attempt === retryCount || !(error instanceof Error)) {
        throw error
      }
    } finally {
      window.clearTimeout(timeoutHandle)
    }

    await delay(250 * (attempt + 1))
  }

  throw new Error(`Request to '${path}' failed unexpectedly.`)
}
