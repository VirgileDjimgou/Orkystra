import { sendApiRequest } from './apiClient'

type ApiProjectionEntry = {
  projectionName: string
  projectionKey: string
  source: string
  capturedAtUtc: string
  payload: Record<string, unknown>
}

type ApiWorkflowEntry = {
  runId: number
  workflowKind: string
  subjectKey: string
  scenarioId: string | null
  source: string
  status: string
  createdAtUtc: string
  payload: Record<string, unknown>
}

type ApiAuditEntry = {
  method: string
  path: string
  occurredAtUtc: string
  tenantId: string
  reason: string
  correlationId: string
  responseStatusCode: number
}

type ApiProjectionEnvelope = {
  count: number
  entries: ApiProjectionEntry[]
}

type ApiWorkflowEnvelope = {
  count: number
  entries: ApiWorkflowEntry[]
}

type ApiAuditEnvelope = {
  count: number
  entries: ApiAuditEntry[]
}

export type OperationalWorkflowRunView = {
  id: string
  workflowKind: string
  subjectLabel: string
  scenarioLabel: string
  source: string
  status: string
  createdAtLabel: string
  summary: string
}

export type OperationalProjectionSnapshotView = {
  id: string
  projectionName: string
  projectionKey: string
  source: string
  capturedAtLabel: string
  summary: string
}

export type OperationalAuditEntryView = {
  id: string
  actionLabel: string
  statusCode: number
  occurredAtLabel: string
  correlationId: string
  summary: string
}

export type OperationalActivityView = {
  workflowRuns: OperationalWorkflowRunView[]
  projectionSnapshots: OperationalProjectionSnapshotView[]
  auditEntries: OperationalAuditEntryView[]
}

export type OperationalActivityLoadResult = {
  activity: OperationalActivityView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

function formatUtcLabel(value: string): string {
  return new Intl.DateTimeFormat('en-GB', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'UTC',
  }).format(new Date(value)).replace(',', '') + ' UTC'
}

function countArrayField(payload: Record<string, unknown>, key: string): number {
  const value = payload[key]
  return Array.isArray(value) ? value.length : 0
}

function countArrayPayload(payload: Record<string, unknown>): number {
  const value = payload as unknown
  return Array.isArray(value) ? value.length : 0
}

function projectionSummary(entry: ApiProjectionEntry): string {
  const payload = entry.payload

  switch (entry.projectionName) {
    case 'control-tower-overview':
      return `${countArrayField(payload, 'routes')} routes, ${countArrayField(payload, 'warehouses')} warehouses, ${countArrayField(payload, 'providers')} providers.`
    case 'warehouse-summaries':
      return `${countArrayPayload(payload)} warehouse summaries refreshed.`
    case 'warehouse-detail':
      return `${String(payload.name ?? entry.projectionKey)} with ${countArrayField(payload, 'zones')} zones and ${countArrayField(payload, 'docks')} docks.`
    case 'route-summaries':
      return `${countArrayPayload(payload)} route summaries refreshed.`
    case 'route-detail':
      return `${String(payload.reference ?? entry.projectionKey)} with ${countArrayField(payload, 'stops')} stops and ${countArrayField(payload, 'deliveries')} deliveries.`
    case 'provider-catalog':
      return `${countArrayField(payload, 'providers')} providers visible in the runtime catalog.`
    default:
      return `Latest ${entry.projectionName} snapshot stored for ${entry.projectionKey}.`
  }
}

function workflowSummary(entry: ApiWorkflowEntry): string {
  const payload = entry.payload

  if (entry.workflowKind === 'ai-recommendation') {
    const recommendation = payload.recommendation as Record<string, unknown> | undefined
    return recommendation?.directAnswer
      ? String(recommendation.directAnswer)
      : 'Grounded AI recommendation stored for later review.'
  }

  if (entry.workflowKind === 'route-optimization') {
    const optimization = payload.optimization as Record<string, unknown> | undefined
    const orderedStops = Array.isArray(optimization?.orderedStopReferences)
      ? optimization?.orderedStopReferences.length
      : 0
    return `${String(optimization?.routeReference ?? entry.subjectKey)} returned ${orderedStops} ordered stops with status ${String(optimization?.status ?? entry.status)}.`
  }

  return `Stored ${entry.workflowKind} run for ${entry.subjectKey}.`
}

function auditSummary(entry: ApiAuditEntry): string {
  return `${entry.method} ${entry.path}${entry.reason ? ` (${entry.reason})` : ''}`
}

export function buildFallbackOperationalActivity(): OperationalActivityView {
  return {
    workflowRuns: [
      {
        id: 'fallback-workflow-1',
        workflowKind: 'route-optimization',
        subjectLabel: 'RT-412',
        scenarioLabel: 'Scenario snapshot unavailable',
        source: 'fallback',
        status: 'optimized',
        createdAtLabel: 'Local fallback',
        summary: 'Recent workflow history is unavailable, but the current route recovery review remains usable.',
      },
    ],
    projectionSnapshots: [
      {
        id: 'fallback-projection-1',
        projectionName: 'control-tower-overview',
        projectionKey: 'current',
        source: 'fallback',
        capturedAtLabel: 'Local fallback',
        summary: 'Projection history is unavailable, so only the current workspace state can be inspected.',
      },
    ],
    auditEntries: [
      {
        id: 'fallback-audit-1',
        actionLabel: 'Observability offline',
        statusCode: 0,
        occurredAtLabel: 'Local fallback',
        correlationId: 'n/a',
        summary: 'Protected observability endpoints are unavailable right now.',
      },
    ],
  }
}

export async function loadOperationalActivity(): Promise<OperationalActivityLoadResult> {
  try {
    const [workflowResponse, projectionResponse, auditResponse] = await Promise.all([
      sendApiRequest('/observability/persistence/workflows?count=6', { includeTenantHeader: true }),
      sendApiRequest('/observability/persistence/projections?count=6', { includeTenantHeader: true }),
      sendApiRequest('/observability/audit?count=6', { includeTenantHeader: true }),
    ])

    if (!workflowResponse.ok || !projectionResponse.ok || !auditResponse.ok) {
      throw new Error('Operational observability endpoints did not return a successful response.')
    }

    const workflows = (await workflowResponse.json()) as ApiWorkflowEnvelope
    const projections = (await projectionResponse.json()) as ApiProjectionEnvelope
    const audits = (await auditResponse.json()) as ApiAuditEnvelope

    return {
      activity: {
        workflowRuns: workflows.entries.map((entry) => ({
          id: `workflow-${entry.runId}`,
          workflowKind: entry.workflowKind,
          subjectLabel: entry.subjectKey,
          scenarioLabel: entry.scenarioId ?? 'No scenario override',
          source: entry.source,
          status: entry.status,
          createdAtLabel: formatUtcLabel(entry.createdAtUtc),
          summary: workflowSummary(entry),
        })),
        projectionSnapshots: projections.entries.map((entry) => ({
          id: `projection-${entry.projectionName}-${entry.projectionKey}`,
          projectionName: entry.projectionName,
          projectionKey: entry.projectionKey,
          source: entry.source,
          capturedAtLabel: formatUtcLabel(entry.capturedAtUtc),
          summary: projectionSummary(entry),
        })),
        auditEntries: audits.entries.map((entry) => ({
          id: `audit-${entry.correlationId}-${entry.path}`,
          actionLabel: `${entry.method} ${entry.path}`,
          statusCode: entry.responseStatusCode,
          occurredAtLabel: formatUtcLabel(entry.occurredAtUtc),
          correlationId: entry.correlationId,
          summary: auditSummary(entry),
        })),
      },
      source: 'api',
      errorMessage: null,
    }
  } catch (error) {
    return {
      activity: buildFallbackOperationalActivity(),
      source: 'fallback',
      errorMessage: error instanceof Error ? error.message : 'Operational observability request failed.',
    }
  }
}
