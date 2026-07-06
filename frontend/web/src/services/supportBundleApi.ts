import { sendApiRequest } from './apiClient'

type ApiSupportBundle = {
  generatedAtUtc: string
  tenantId: string
  summary: {
    posture: string
    summary: string
    escalationTarget: string
    signals: string[]
    collectionHints: string[]
    artifactChecklist: string[]
  }
  collections: {
    projectionCount: number
    workflowCount: number
    auditCount: number
  }
  persistence: {
    provider: string
    posture: string
    healthy: boolean
    verifiedAtUtc: string
  }
}

export type SupportBundleSummaryView = {
  generatedAtLabel: string
  tenantId: string
  posture: string
  summary: string
  escalationTarget: string
  signals: string[]
  collectionHints: string[]
  artifactChecklist: string[]
  projectionCount: number
  workflowCount: number
  auditCount: number
  persistenceLabel: string
}

export type SupportBundleSummaryLoadResult = {
  bundle: SupportBundleSummaryView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

export type SupportIssueDraftInput = {
  bundle: SupportBundleSummaryView
  scenarioLabel: string
  routeLabel: string
  routeStatus: string
  deploymentMode: string
  browserLabel: string
  summary: string
  reproductionSteps: string
  expectedResult: string
  actualResult: string
  evidenceNotes: string
}

export type SupportPacketReadinessView = {
  status: 'Ready' | 'Needs operator details' | 'Needs runtime evidence'
  summary: string
  missingItems: string[]
}

export type SupportReleaseHandshakeView = {
  label: string
  summary: string
}

export type SupportPacketRetryGuidanceView = {
  label: string
  summary: string
  nextStep: string
}

export type SupportPacketArchiveGuidanceView = {
  label: string
  summary: string
}

export type SupportPacketLifecycleGuidanceView = {
  label: string
  summary: string
}

export type SupportPacketTimelineGuidanceView = {
  label: string
  summary: string
}

export type SupportPacketDeltaGuidanceView = {
  label: string
  summary: string
}

export type SupportPacketClassificationView = {
  label: string
  summary: string
}

export type SupportPacketTriageShortcutView = {
  lane: string
  nextOwner: string
  summary: string
  checks: string[]
}

export type SupportPacketReleaseContextGuidanceView = {
  label: string
  summary: string
  checks: string[]
}

export type SupportPacketDriftGuidanceView = {
  label: string
  summary: string
  checks: string[]
}

export type SupportPacketEvidenceGuidanceView = {
  label: string
  summary: string
  checks: string[]
}

export type SupportPacketEvidenceGapGuidanceView = {
  label: string
  summary: string
  checks: string[]
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

export function buildFallbackSupportBundleSummary(): SupportBundleSummaryView {
  return {
    generatedAtLabel: 'Local fallback',
    tenantId: 'local-demo-tenant',
    posture: 'Support bundle unavailable',
    summary:
      'The aggregated support bundle could not be collected, so only the live operational trace cards are available right now.',
    escalationTarget: 'evidence-thin',
    signals: ['support-bundle-fallback'],
    collectionHints: ['Restore protected observability API access before exporting a support bundle.'],
    artifactChecklist: ['Restore protected observability API access before filing a support issue.'],
    projectionCount: 0,
    workflowCount: 0,
    auditCount: 0,
    persistenceLabel: 'Unknown persistence posture',
  }
}

function mapApiSupportBundleToView(bundle: ApiSupportBundle): SupportBundleSummaryView {
  return {
    generatedAtLabel: formatUtcLabel(bundle.generatedAtUtc),
    tenantId: bundle.tenantId,
    posture: bundle.summary.posture,
    summary: bundle.summary.summary,
    escalationTarget: bundle.summary.escalationTarget,
    signals: bundle.summary.signals,
    collectionHints: bundle.summary.collectionHints,
    artifactChecklist: bundle.summary.artifactChecklist,
    projectionCount: bundle.collections.projectionCount,
    workflowCount: bundle.collections.workflowCount,
    auditCount: bundle.collections.auditCount,
    persistenceLabel: `${bundle.persistence.provider} / ${bundle.persistence.posture}`,
  }
}

export async function loadSupportBundleSummary(): Promise<SupportBundleSummaryLoadResult> {
  try {
    const response = await sendApiRequest('/observability/support-bundle?count=6', {
      includeTenantHeader: true,
    })

    if (!response.ok) {
      throw new Error(`Support bundle request failed with status ${response.status}`)
    }

    const payload = (await response.json()) as ApiSupportBundle

    return {
      bundle: mapApiSupportBundleToView(payload),
      source: 'api',
      errorMessage: null,
    }
  } catch (error) {
    return {
      bundle: buildFallbackSupportBundleSummary(),
      source: 'fallback',
      errorMessage: error instanceof Error ? error.message : 'Support bundle request failed.',
    }
  }
}

export async function exportSupportBundle(): Promise<void> {
  const response = await sendApiRequest('/observability/support-bundle?count=10', {
    includeTenantHeader: true,
  })

  if (!response.ok) {
    throw new Error(`Support bundle export failed with status ${response.status}`)
  }

  const payload = await response.text()
  const parsedPayload = JSON.parse(payload) as ApiSupportBundle
  const blob = new Blob([payload], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = `${buildSupportArtifactStem(mapApiSupportBundleToView(parsedPayload))}-support-bundle.json`
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  URL.revokeObjectURL(url)
}

export function downloadSupportIssueDraft(
  bundle: SupportBundleSummaryView,
  issueDraft: string
): void {
  const blob = new Blob([issueDraft], { type: 'text/markdown;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = `${buildSupportArtifactStem(bundle)}-issue-draft.md`
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  URL.revokeObjectURL(url)
}

function normalizeMultilineBlock(value: string, fallback: string): string {
  const normalized = value
    .split('\n')
    .map((line) => line.trimEnd())
    .join('\n')
    .trim()

  return normalized.length > 0 ? normalized : fallback
}

function sanitizeArtifactSegment(value: string): string {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
}

export function buildSupportArtifactStem(bundle: SupportBundleSummaryView): string {
  const tenantSegment = sanitizeArtifactSegment(bundle.tenantId) || 'tenant'
  const postureSegment = sanitizeArtifactSegment(bundle.posture) || 'support'
  const timestampSegment =
    bundle.generatedAtLabel === 'Local fallback'
      ? new Date().toISOString().replaceAll(':', '-')
      : sanitizeArtifactSegment(bundle.generatedAtLabel) || new Date().toISOString().replaceAll(':', '-')

  return `orkystra-${tenantSegment}-${postureSegment}-${timestampSegment}`
}

export function buildSupportIssueDraft(input: SupportIssueDraftInput): string {
  const summary = input.summary.trim() || 'Operator-visible issue requiring reproduction review.'
  const reproductionSteps = normalizeMultilineBlock(
    input.reproductionSteps,
    [
      `1. Open the ${input.scenarioLabel} scenario in the control tower.`,
      `2. Focus route ${input.routeLabel} and refresh the affected workflow surface.`,
      '3. Capture the visible failure and export the support bundle from the operational trace panel.',
    ].join('\n')
  )
  const expectedResult =
    input.expectedResult.trim() ||
    'The control tower should complete the workflow without degraded runtime signals.'
  const actualResult =
    input.actualResult.trim() ||
    input.bundle.summary
  const evidenceNotes = normalizeMultilineBlock(
    input.evidenceNotes,
    [
      `Support posture: ${input.bundle.posture}`,
      `Signals: ${input.bundle.signals.join(', ') || 'none recorded'}`,
      `Collection hints: ${input.bundle.collectionHints.join(' | ') || 'none'}`,
    ].join('\n')
  )

  return [
    '## Summary',
    '',
    summary,
    '',
    '## Environment',
    '',
    `- Tenant: ${input.bundle.tenantId}`,
    `- Deployment mode: ${input.deploymentMode}`,
    `- Browser: ${input.browserLabel}`,
    `- Scenario: ${input.scenarioLabel}`,
    `- Route: ${input.routeLabel} (${input.routeStatus})`,
    `- Support bundle posture: ${input.bundle.posture}`,
    `- Escalation target: ${input.bundle.escalationTarget}`,
    `- Persistence: ${input.bundle.persistenceLabel}`,
    `- Bundle generated: ${input.bundle.generatedAtLabel}`,
    `- Repository branch: record the active branch when filing the packet`,
    `- Commit hash: record the exact commit used for the reproduction`,
    `- Release tag or posture: record the current release candidate or local posture`,
    '',
    '## Steps to reproduce',
    '',
    reproductionSteps,
    '',
    '## Expected result',
    '',
    expectedResult,
    '',
    '## Actual result',
    '',
    actualResult,
    '',
    '## Evidence',
    '',
    `- Support summary: ${input.bundle.summary}`,
    `- Workflow count: ${input.bundle.workflowCount}`,
    `- Projection count: ${input.bundle.projectionCount}`,
    `- Audit count: ${input.bundle.auditCount}`,
    `- Notes: ${evidenceNotes}`,
    `- Artifact checklist: ${input.bundle.artifactChecklist.join(' | ')}`,
    '',
    '## Additional context',
    '',
    'Attach the exported support bundle JSON and any focused logs or screenshots that show the first failing state.',
  ].join('\n')
}

export function buildSupportPacketReadiness(input: SupportIssueDraftInput): SupportPacketReadinessView {
  const missingItems: string[] = []
  const hasBundleEvidence =
    input.bundle.workflowCount > 0 &&
    input.bundle.auditCount > 0 &&
    input.bundle.posture !== 'Support bundle unavailable'

  if (input.summary.trim().length < 8) {
    missingItems.push('Add a clearer issue summary.')
  }

  if (input.reproductionSteps.trim().length < 12) {
    missingItems.push('Describe the reproduction steps in more detail.')
  }

  if (input.expectedResult.trim().length < 8) {
    missingItems.push('State the expected healthy result.')
  }

  if ((input.actualResult.trim() || input.bundle.summary).length < 8) {
    missingItems.push('Describe the actual failing result.')
  }

  if (!hasBundleEvidence) {
    missingItems.push('Refresh runtime evidence so the packet contains workflow and audit support data.')
  }

  if (missingItems.length === 0) {
    return {
      status: 'Ready',
      summary: 'The packet looks complete enough for first-line maintainer handoff.',
      missingItems,
    }
  }

  if (!hasBundleEvidence) {
    return {
      status: 'Needs runtime evidence',
      summary: 'The packet still needs stronger runtime evidence before escalation.',
      missingItems,
    }
  }

  return {
    status: 'Needs operator details',
    summary: 'The packet has live evidence, but the operator narrative still needs more detail.',
    missingItems,
  }
}

export function buildSupportPacketEvidenceGapGuidance(
  input: SupportIssueDraftInput,
  readiness: SupportPacketReadinessView
): SupportPacketEvidenceGapGuidanceView {
  const hasWorkflowEvidence = input.bundle.workflowCount > 0
  const hasAuditEvidence = input.bundle.auditCount > 0
  const hasNarrative =
    input.summary.trim().length >= 8 &&
    input.reproductionSteps.trim().length >= 12 &&
    input.expectedResult.trim().length >= 8 &&
    (input.actualResult.trim() || input.bundle.summary).length >= 8

  if (readiness.status === 'Ready') {
    return {
      label: 'Evidence chain is balanced',
      summary: 'The packet already has a usable narrative and enough runtime evidence for a first maintainer pass.',
      checks: [
        'Keep the current issue draft and support bundle as the canonical pair.',
        'Only attach optional comparison context when it explains a real delta.',
        'Preserve the current packet if the next retry does not materially change the failure.',
      ],
    }
  }

  if (!hasWorkflowEvidence || !hasAuditEvidence) {
    return {
      label: 'Runtime evidence is the weakest link',
      summary: 'The packet still needs stronger workflow or audit proof before deeper escalation is worth the maintainer time.',
      checks: [
        hasWorkflowEvidence ? 'Workflow evidence is present; focus on audit proof next.' : 'Capture a fresh workflow trace in the next support bundle.',
        hasAuditEvidence ? 'Audit evidence is present; focus on workflow proof next.' : 'Capture audit evidence for the same failing run.',
        'Refresh the packet only after the new runtime evidence reflects the same failure.',
      ],
    }
  }

  if (!hasNarrative) {
    return {
      label: 'Operator narrative is the weakest link',
      summary: 'The runtime evidence exists, but the packet still underspecifies what the maintainer is supposed to reproduce and verify.',
      checks: [
        'Tighten the summary, reproduction steps, expected result, and actual result.',
        'Make the issue draft readable without opening the raw bundle first.',
        'Keep the evidence focused on the exact first failing state you want reviewed.',
      ],
    }
  }

  return {
    label: 'Packet still needs one focused pass',
    summary: 'The packet is close, but one category of proof still needs to be made more explicit before handoff.',
    checks: [
      'Prefer strengthening the weakest category before adding more optional attachments.',
      'Keep the issue draft, validation, and lifecycle summary aligned.',
      'Escalate only after the packet reads as one coherent evidence chain.',
    ],
  }
}

export function buildSupportReleaseHandshake(
  bundle: SupportBundleSummaryView,
  readiness: SupportPacketReadinessView
): SupportReleaseHandshakeView {
  if (readiness.status !== 'Ready') {
    return {
      label: 'Hold packet',
      summary: 'Do not escalate yet. Complete the packet until it is ready for a maintainer handoff within the current OSS support posture.',
    }
  }

  switch (bundle.escalationTarget) {
    case 'configuration-or-persistence':
      return {
        label: 'Config review first',
        summary: 'The packet is complete enough, but the first pass should stay focused on self-host configuration and persistence posture before treating it as a product defect.',
      }
    case 'dependency-or-event-backbone':
      return {
        label: 'Dependency review first',
        summary: 'The packet is complete enough, but the next pass should verify broker or dependency health before deeper product debugging.',
      }
    case 'product-or-workflow':
      return {
        label: 'Ready for maintainer handoff',
        summary: 'The packet matches the current release posture closely enough for a maintainer to take a first debugging pass.',
      }
    default:
      return {
        label: 'Collect more evidence',
        summary: 'The packet still sits below the current release handshake bar because runtime evidence remains too thin.',
      }
  }
}

export function buildSupportPacketRetryGuidance(
  bundle: SupportBundleSummaryView,
  readiness: SupportPacketReadinessView
): SupportPacketRetryGuidanceView {
  if (readiness.status === 'Ready' && bundle.escalationTarget === 'product-or-workflow') {
    return {
      label: 'Reuse current packet',
      summary: 'The current packet is already strong enough for maintainer handoff if the issue has not changed after the latest retry.',
      nextStep: 'Reuse the current draft and support bundle unless a new reset or reproduction pass changes the runtime evidence.',
    }
  }

  if (bundle.posture === 'Support bundle unavailable' || readiness.status === 'Needs runtime evidence') {
    return {
      label: 'Refresh after retry',
      summary: 'The packet needs stronger runtime evidence before it should be handed off.',
      nextStep: 'Retry or reset the environment, then export a fresh support bundle and refresh the packet instead of reusing the older evidence.',
    }
  }

  return {
    label: 'Regenerate operator narrative',
    summary: 'The runtime evidence is present, but the operator-facing issue narrative still needs to be tightened after the next retry.',
    nextStep: 'Keep the current bundle if it still reflects the same failure, but refresh the issue draft and expected versus actual details before escalation.',
  }
}

export function buildSupportPacketArchiveGuidance(
  bundle: SupportBundleSummaryView,
  readiness: SupportPacketReadinessView
): SupportPacketArchiveGuidanceView {
  if (readiness.status === 'Needs runtime evidence') {
    return {
      label: 'Archive before overwrite',
      summary: 'If the current packet already captures a meaningful failed state, archive it before the next retry replaces the live evidence.',
    }
  }

  if (bundle.escalationTarget === 'product-or-workflow') {
    return {
      label: 'Prune duplicates later',
      summary: 'Keep distinct retry states while the failure is still being narrowed, then prune near-duplicate archives once the maintainer handoff is stable.',
    }
  }

  return {
    label: 'Keep only meaningful deltas',
    summary: 'Retain archived packets only when they reflect a real change in configuration, dependency posture, or runtime evidence.',
  }
}

export function buildSupportPacketLifecycleGuidance(
  bundle: SupportBundleSummaryView,
  readiness: SupportPacketReadinessView
): SupportPacketLifecycleGuidanceView {
  if (readiness.status === 'Ready' && bundle.escalationTarget === 'product-or-workflow') {
    return {
      label: 'Current packet is canonical',
      summary: 'Treat the current support packet as the active maintainer handoff. Older packet snapshots are historical context, not the primary debugging entrypoint.',
    }
  }

  if (readiness.status === 'Needs runtime evidence') {
    return {
      label: 'Canonical packet not established yet',
      summary: 'Do not treat the current packet as the final handoff state until a stronger runtime capture has been refreshed and validated.',
    }
  }

  return {
    label: 'Current packet needs tightening',
    summary: 'The current packet is still the active working copy, but the maintainer handoff should wait until the operator narrative or evidence quality is clearer.',
  }
}

export function buildSupportPacketTimelineGuidance(
  _bundle: SupportBundleSummaryView,
  readiness: SupportPacketReadinessView
): SupportPacketTimelineGuidanceView {
  if (readiness.status === 'Ready') {
    return {
      label: 'Timeline can stay short',
      summary: 'Use the current packet as the active story and keep only the retry or archive points that materially changed the diagnosis.',
    }
  }

  if (readiness.status === 'Needs runtime evidence') {
    return {
      label: 'Timeline still evolving',
      summary: 'Expect the packet story to change after the next retry or reset, so the timeline should emphasize what evidence is still missing.',
    }
  }

  return {
    label: 'Timeline needs narrative cleanup',
    summary: 'The runtime capture exists, but the packet story still needs clearer wording about what changed between attempts.',
  }
}

export function buildSupportPacketDeltaGuidance(
  bundle: SupportBundleSummaryView,
  readiness: SupportPacketReadinessView
): SupportPacketDeltaGuidanceView {
  if (bundle.escalationTarget === 'product-or-workflow' && readiness.status === 'Ready') {
    return {
      label: 'Describe the latest delta only',
      summary: 'Focus the handoff on what changed between the current packet and the latest archived snapshot, not on every historical retry detail.',
    }
  }

  if (bundle.escalationTarget === 'dependency-or-event-backbone') {
    return {
      label: 'Call out dependency drift',
      summary: 'Make the delta explicit around broker, dependency, or runtime posture changes between retries so the maintainer can see whether the issue actually moved.',
    }
  }

  return {
    label: 'Call out evidence gaps',
    summary: 'Narrate the delta in terms of what evidence improved, what stayed thin, and what still prevents the current packet from becoming the canonical handoff.',
  }
}

export function buildSupportPacketClassification(
  bundle: SupportBundleSummaryView,
  readiness: SupportPacketReadinessView
): SupportPacketClassificationView {
  if (readiness.status === 'Needs runtime evidence') {
    return {
      label: 'Evidence-gap packet',
      summary: 'This packet should stay in evidence collection mode until workflow and audit signals are strong enough to support a maintainer handoff.',
    }
  }

  switch (bundle.escalationTarget) {
    case 'configuration-or-persistence':
      return {
        label: 'Configuration review packet',
        summary: 'The packet currently reads more like an environment, persistence, or self-host posture review than a confirmed product defect.',
      }
    case 'dependency-or-event-backbone':
      return {
        label: 'Runtime dependency packet',
        summary: 'The packet points first toward broker, dependency, or event-backbone drift before deeper workflow-level debugging.',
      }
    case 'product-or-workflow':
      return {
        label: readiness.status === 'Ready' ? 'Product defect candidate' : 'Product narrative still tightening',
        summary:
          readiness.status === 'Ready'
            ? 'The packet is structured closely enough to behave like a first-line product or workflow defect handoff.'
            : 'The packet points toward product behavior, but the operator story still needs tightening before handoff.',
      }
    default:
      return {
        label: 'General support packet',
        summary: 'The packet is usable as a support starting point, but the main troubleshooting lane is still mixed.',
      }
  }
}

export function buildSupportPacketTriageShortcut(
  bundle: SupportBundleSummaryView,
  readiness: SupportPacketReadinessView
): SupportPacketTriageShortcutView {
  if (readiness.status === 'Needs runtime evidence') {
    return {
      lane: 'Operator evidence refresh',
      nextOwner: 'Operator',
      summary: 'Do one more evidence-focused pass before asking a maintainer to spend time on the packet.',
      checks: [
        'Re-run the scenario and export a fresh support bundle.',
        'Confirm workflow and audit evidence are both present.',
        'Tighten the expected-versus-actual wording in the issue draft.',
      ],
    }
  }

  if (bundle.escalationTarget === 'configuration-or-persistence') {
    return {
      lane: 'Self-host configuration review',
      nextOwner: 'Operator or deploy maintainer',
      summary: 'Start with configuration, persistence, and deployment posture before treating the packet as a core product defect.',
      checks: [
        'Verify the persistence provider and deployment mode in the issue draft.',
        'Call out recent configuration or environment changes.',
        'Confirm the failing state still reproduces after a clean restart.',
      ],
    }
  }

  if (bundle.escalationTarget === 'dependency-or-event-backbone') {
    return {
      lane: 'Runtime dependency review',
      nextOwner: 'Platform or runtime maintainer',
      summary: 'The first triage pass should inspect broker, dependency, or event-flow posture before workflow logic.',
      checks: [
        'Highlight dependency or broker drift between the latest attempts.',
        'Keep the latest delta focused on runtime posture changes.',
        'Attach only the minimal logs needed to show the first failing signal.',
      ],
    }
  }

  return {
    lane: readiness.status === 'Ready' ? 'Maintainer workflow review' : 'Operator narrative tightening',
    nextOwner: readiness.status === 'Ready' ? 'Workflow maintainer' : 'Operator',
    summary:
      readiness.status === 'Ready'
        ? 'The packet is ready for a workflow-level maintainer pass with the current bundle, draft, and lifecycle summary.'
        : 'The packet already leans product-side, but the operator should tighten the story before deeper debugging starts.',
    checks:
      readiness.status === 'Ready'
        ? [
            'Read the issue draft before opening the raw bundle JSON.',
            'Use the lifecycle summary to compare only the latest meaningful delta.',
            'Keep older archives as context, not as the main debugging entrypoint.',
          ]
        : [
            'Clarify the user action that first exposed the failure.',
            'Make the latest delta explicit between attempts.',
            'Keep the current bundle only if it still reflects the same failure.',
          ],
  }
}

export function buildSupportPacketReleaseContextGuidance(
  bundle: SupportBundleSummaryView,
  readiness: SupportPacketReadinessView
): SupportPacketReleaseContextGuidanceView {
  if (readiness.status === 'Needs runtime evidence') {
    return {
      label: 'Capture context after the next retry',
      summary: 'Do not record repository or release context too early if the next retry is likely to replace the active packet.',
      checks: [
        'Record the branch and commit that produced the failing retry, not an earlier attempt.',
        'Pair the packet with the runtime posture that actually reproduced the issue.',
        'Refresh the packet before escalation if the environment changed materially.',
      ],
    }
  }

  if (bundle.escalationTarget === 'configuration-or-persistence') {
    return {
      label: 'Capture deployment posture first',
      summary: 'For configuration-heavy packets, the maintainer needs the runtime and deployment context as much as the bug narrative.',
      checks: [
        'Record the active branch and commit used during the failing run.',
        'Call out the persistence provider and self-host posture explicitly.',
        'Mention whether the packet came from a release candidate flow or a local working tree.',
      ],
    }
  }

  return {
    label: 'Freeze the exact reproducing state',
    summary: 'A mature packet should point to the exact repo and release posture that produced the active failure.',
    checks: [
      'Capture the active branch, commit, and closest release hint.',
      'Keep the packet tied to the runtime shell and host posture that reproduced the issue.',
      'Use the same release context in the issue draft, lifecycle summary, and maintainer handoff.',
    ],
  }
}

export function buildSupportPacketDriftGuidance(
  bundle: SupportBundleSummaryView,
  readiness: SupportPacketReadinessView
): SupportPacketDriftGuidanceView {
  if (readiness.status === 'Needs runtime evidence') {
    return {
      label: 'Stabilize the next retry context',
      summary: 'If the next retry changes the shell, deployment, or branch, call that drift out explicitly before comparing failures.',
      checks: [
        'Avoid mixing old evidence with a new runtime shell without noting the change.',
        'Record when the next retry was run from a different branch or commit.',
        'Treat context drift as a first-line explanation until the evidence stabilizes again.',
      ],
    }
  }

  if (bundle.escalationTarget === 'configuration-or-persistence') {
    return {
      label: 'Assume environment drift first',
      summary: 'Configuration-heavy failures should be compared against deployment and host changes before they are treated as code regressions.',
      checks: [
        'Call out persistence or self-host changes between retries.',
        'Note whether the failing packet came from the same host and shell posture.',
        'Keep the maintainer focused on environment drift until the repo state is stable.',
      ],
    }
  }

  return {
    label: 'Compare the last two reproductions',
    summary: 'A mature handoff should say whether the latest retry came from the same repo and runtime state as the previous one.',
    checks: [
      'Record branch and commit changes between retries.',
      'Call out host, shell, or capture-source changes if they occurred.',
      'Only treat the latest failure as the same debugging state when the context stayed stable.',
    ],
  }
}

export function buildSupportPacketEvidenceGuidance(
  bundle: SupportBundleSummaryView,
  readiness: SupportPacketReadinessView
): SupportPacketEvidenceGuidanceView {
  if (readiness.status === 'Needs runtime evidence') {
    return {
      label: 'Re-anchor the packet first',
      summary: 'Do not attach extra comparison artifacts until the primary handoff bundle and issue draft are coherent again.',
      checks: [
        'Keep the current support bundle and issue draft as the primary artifacts.',
        'Attach comparison evidence only when it explains what changed between retries.',
        'Avoid dumping broad log sets when one focused failing artifact would do.',
      ],
    }
  }

  if (bundle.escalationTarget === 'dependency-or-event-backbone') {
    return {
      label: 'Anchor the first failing evidence',
      summary: 'Dependency-heavy failures should still travel with one canonical bundle and one clear operator narrative before optional comparison files.',
      checks: [
        'Lead with the current issue draft and support bundle.',
        'Use optional comparison artifacts only to explain broker or runtime drift.',
        'Keep the maintainer reading order short and explicit.',
      ],
    }
  }

  return {
    label: 'Keep one canonical evidence chain',
    summary: 'A mature packet should tell the maintainer which artifacts are mandatory, which are optional, and in what order to read them.',
    checks: [
      'Make the issue draft and support bundle the first-line evidence pair.',
      'Use lifecycle and validation outputs as interpretation guides, not as replacements for the evidence.',
      'Treat archived snapshots and comparison notes as optional context unless the delta actually matters.',
    ],
  }
}
