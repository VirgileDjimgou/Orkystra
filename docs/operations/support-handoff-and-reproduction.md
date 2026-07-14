# Support Handoff and Issue Reproduction

This guide standardizes how to file, reproduce, and escalate a problem in Orkystra after the post-release-candidate checkpoint.

## When to use this guide

Use it when you need to:

- report a bug
- reproduce an issue for another contributor
- hand an incident to maintainers with enough evidence for a first-pass diagnosis
- attach a support bundle before escalation

The operator workspace now also exposes a lightweight support intake surface beside the support bundle export. Use it to prefill a GitHub-ready issue draft with the current tenant, persistence posture, selected scenario, selected route, and the latest runtime support summary.

## Minimum issue package

Before opening an issue, collect:

- a short summary of the problem
- the exact repository state or release tag
- the environment, including OS, runtime versions, and deployment mode
- the exact steps to reproduce
- the expected result
- the actual result
- the first failing log line or stack trace
- the support bundle export, if the problem touches persistence, event flow, GPS, or observability

## Reproduction checklist

1. Start from a clean checkout or clearly describe the local modifications.
2. Record the current branch and commit hash.
3. Note whether the issue appears in:
   - local single-tenant development
   - Docker Compose self-host
   - PostgreSQL-backed self-host
   - frontend-only development
   - backend API execution
4. Reproduce the issue once without changing anything else.
5. Capture the exact user action or API call that triggered the problem.
6. Save the relevant logs, response payloads, screenshots, or console output.
7. Export a support bundle when the issue involves runtime state or cross-service behavior.
8. Verify whether the issue still appears after a clean restart.

## Support bundle guidance

Use the support bundle when the issue may involve:

- persistence state
- event backbone behavior
- GPS or map synchronization
- audit traces
- supportability or runtime diagnostics

Recommended export command:

```powershell
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/export-support-bundle.ps1 -ApiKey <api-key>
```

If you want a ready-to-attach local issue packet instead of collecting files one by one, use:

```powershell
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/package-support-issue.ps1 -ApiKey <api-key>
```

That command prepares one folder containing:

- `support-bundle.json`
- `ISSUE_DRAFT.md`
- `SUPPORT_MANIFEST.json`
- `SUPPORT_VALIDATION.json`
- `SUPPORT_LIFECYCLE.json`
- `SUPPORT_LIFECYCLE.md`
- `MAINTAINER_HANDOFF.md`

The generated packet now also captures repository, release, and runtime context automatically in the manifest and lifecycle summary, so maintainers can see which local repo state and shell posture produced the failure.
When retries happen more than once, keep that context stable if possible, or call out explicitly when branch, commit, host, shell, or capture-source posture changed between attempts.
The packet now also carries an explicit evidence reading order, so the issue draft and active support bundle stay clearly marked as canonical while archive and comparison files stay secondary.
It now also scores evidence gaps by category so the next operator action can focus on the weakest proof instead of collecting broad, low-signal attachments.
It now also synthesizes a remediation checklist so the next pass can stay focused on one owner, one lane, and one stopping rule instead of turning into an open-ended evidence hunt.
It now also includes a capture shortcut so the next operator knows the fastest evidence move for the current packet class and release posture.

## Capture presets during handoff

When packaging or handing off a support issue, capture presets help the next operator choose the fastest evidence move without re-reading the full packet history.

### Preset identity

Each capture preset is identified by:

- **`presetId`** — deterministic identifier derived from packet class and release posture (e.g., `configuration-or-persistence` + `release-candidate` → `configuration-or-persistence-release-candidate`).
- **`presetLabel`** — human-readable label (e.g., `configuration-or-persistence / release-candidate`).

### How presets appear in the packet

- The lifecycle summary (`SUPPORT_LIFECYCLE.json` / `SUPPORT_LIFECYCLE.md`) includes a `captureGuidance` block with the full preset: label, summary, `presetId`, `presetLabel`, `releasePosture`, `packetClass`, `triageLane`, `nextOwner`, `presetActions`, `shortcuts`, `checks`, and `exitCriteria`.
- The maintainer handoff (`MAINTAINER_HANDOFF.md`) echoes the same preset actions so the next operator sees the recommended capture steps without opening the lifecycle file.
- The frontend support panel returns the same `captureGuidance` structure in the bundle summary, so operators can see the preset before running command-line scripts.

### Choosing a preset during packaging

The `package-support-issue.ps1` and `summarize-support-issue.ps1` scripts automatically select the preset that matches the current escalation target and readiness state. The four presets are:

1. **Configuration capture shortcut** — when the escalation target is `configuration-or-persistence`. Freeze deployment posture, capture runtime evidence from the matching failure.
2. **Dependency capture shortcut** — when the escalation target is `dependency-or-event-backbone`. Pin down broker or event-flow evidence around the retry that failed.
3. **Operator capture shortcut** — when the packet is not yet `Ready`. Tighten the user story, then capture workflow and audit evidence from the same run.
4. **Release-aware capture shortcut** — when the packet is already strong. Keep captures narrow and delta-focused; archive older state only when genuinely superseded.

### Validating presets

After packaging, run `validate-support-issue.ps1` to confirm the preset contract is intact. The validator checks:

- The `captureGuidance` block exists in the lifecycle JSON.
- All required fields (`label`, `summary`, `presetId`, `presetLabel`, `releasePosture`, `triageLane`, `nextOwner`, `shortcuts`, `presetActions`, `checks`, `exitCriteria`) are present.
- The lifecycle markdown includes the `## Capture preset actions`, `## Capture shortcuts`, `## Capture checks`, and `## Capture exit criteria` sections.

To validate an existing packet before handoff, use:

```powershell
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/validate-support-issue.ps1 -PacketDirectory <packet-folder>
```

If the issue is reproducible through the API, include the response from:

- `GET /observability/support-bundle`

If you are already in the frontend control tower:

- open the support bundle panel
- fill the issue summary, reproduction, expected result, actual result, and optional evidence note
- check the visible packet class and triage shortcut before escalating
- record the branch, commit, and release posture that match the failing run
- use `Copy issue draft` or `Download issue draft`
- attach the exported support bundle JSON before filing the issue

## What maintainers need to see

A good issue should make the next action obvious. Prefer evidence that answers:

- what failed
- where it failed
- when it failed
- how to reproduce it
- whether it is data-dependent
- whether it is isolated to one service or shared across the stack

## What not to send

Do not include:

- secret values
- private keys
- raw production credentials
- unnecessary personal data
- entire log archives when a focused excerpt is enough

## Escalation rule

If the issue cannot be reproduced from the steps above, add more detail before escalating. If it is reproducible, open the GitHub issue with the template in `.github/ISSUE_TEMPLATE/bug_report.md` and attach the support bundle or the minimal evidence needed to continue.

For maintainer acceptance and packet review, use:

- [docs/operations/support-packet-review.md](support-packet-review.md)
- [docs/operations/support-packet-refresh-loop.md](support-packet-refresh-loop.md)
- [docs/operations/support-packet-archive-hygiene.md](support-packet-archive-hygiene.md)
- [docs/operations/support-packet-lifecycle-summary.md](support-packet-lifecycle-summary.md)
