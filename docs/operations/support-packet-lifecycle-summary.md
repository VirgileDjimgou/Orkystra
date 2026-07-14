# Support Packet Lifecycle Summary

This guide explains how to identify the active support packet handoff state after multiple retries and archived snapshots.

## Summary outputs

Use:

```powershell
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/summarize-support-issue.ps1 -PacketDirectory <packet-folder>
```

That command writes:

- `SUPPORT_LIFECYCLE.json`
- `SUPPORT_LIFECYCLE.md`

## What the summary answers

The lifecycle summary makes these questions obvious:

- what the current active packet state is
- which attempt is the latest active retry
- how many refreshes happened
- how many archived packet snapshots still exist
- whether the current packet is the canonical maintainer handoff
- whether the packet should be reused, refreshed, or regenerated
- what class of packet this is and which triage lane should look first
- who should own the next pass and which quick triage checks still matter
- which repository branch, commit, and release hint produced the packet
- which host, shell, and capture source produced the runtime evidence
- whether the latest retry reused the same repo and runtime posture as the previous attempt
- which artifacts are canonical, which are optional comparison context, and in what order to read them
- which evidence category is currently strongest, weakest, or still absent
- what the next focused remediation pass should do, who should own it, and when that pass should stop
- what the fastest capture shortcut is for the current packet class and release posture
- what the timeline of attempts and archived snapshots looks like
- what changed between the active packet and the latest archived packet

## Canonical handoff rule

Treat the active packet as canonical only when:

- the current validation is still usable
- the current retry state reflects the latest known failure
- archived packet snapshots are being kept only for comparison, not as the main handoff artifact

## What to read first

When a maintainer opens a mature support packet, the preferred reading order is:

1. `SUPPORT_LIFECYCLE.md`
2. `MAINTAINER_HANDOFF.md`
3. `SUPPORT_VALIDATION.json`
4. `ISSUE_DRAFT.md`
5. `support-bundle.json`

## Triage-first reading

Before reading the full packet story, skim these lifecycle fields first:

- `packetClass`
- `triageLane`
- `nextOwner`
- `triageChecks`
- `releaseContext`
- `runtimeContext`
- `contextDrift`
- `evidenceProvenance`
- `evidenceGapScore`
- `remediationChecklist`
- `captureGuidance`

That keeps the first pass short and helps avoid using maintainer time on a packet that still belongs in operator refresh or configuration review.

### Capture guidance block

The `captureGuidance` block is the most actionable section in the lifecycle summary. It tells the next operator the fastest evidence move for the current packet class and release posture without requiring them to re-read the full packet story.

Fields:

| Field | Purpose |
|-------|---------|
| `label` | Short human name for this preset (e.g., `Configuration capture shortcut`). |
| `summary` | One-line explanation of why this preset was selected. |
| `presetId` | Deterministic identifier combining packet class and release posture. |
| `presetLabel` | Readable composite label (e.g., `configuration-or-persistence / release-candidate`). |
| `releasePosture` | Current release posture that produced the packet. |
| `packetClass` | Packet classification used to select the preset. |
| `triageLane` | Triage lane that should look first. |
| `nextOwner` | Who should own the next capture pass. |
| `presetActions` | Ordered steps for the fastest useful capture move. |
| `shortcuts` | Same actions in compact form for quick reference. |
| `checks` | Guard conditions to avoid common capture drift. |
| `exitCriteria` | Conditions for ending the capture pass cleanly. |

The four presets are selected automatically based on escalation target and readiness:

1. **Configuration capture shortcut** — escalation target is `configuration-or-persistence`.
2. **Dependency capture shortcut** — escalation target is `dependency-or-event-backbone`.
3. **Operator capture shortcut** — packet readiness is not `Ready`.
4. **Release-aware capture shortcut** — packet is already strong, delta-focused capture.

### Preset sections in the lifecycle markdown

The lifecycle markdown always includes these four preset sections under the capture guidance heading:

- `## Capture preset actions` — the ordered capture steps.
- `## Capture shortcuts` — the same steps in compact reference form.
- `## Capture checks` — guard conditions against drift.
- `## Capture exit criteria` — stopping conditions.

If any of these sections are missing, `validate-support-issue.ps1` reports an error and the packet should be regenerated before handoff.

## Related flows

- [support-handoff-and-reproduction.md](support-handoff-and-reproduction.md)
- [support-packet-review.md](support-packet-review.md)
- [support-packet-refresh-loop.md](support-packet-refresh-loop.md)
- [support-packet-archive-hygiene.md](support-packet-archive-hygiene.md)
