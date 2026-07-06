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

That keeps the first pass short and helps avoid using maintainer time on a packet that still belongs in operator refresh or configuration review.

## Related flows

- [support-handoff-and-reproduction.md](support-handoff-and-reproduction.md)
- [support-packet-review.md](support-packet-review.md)
- [support-packet-refresh-loop.md](support-packet-refresh-loop.md)
- [support-packet-archive-hygiene.md](support-packet-archive-hygiene.md)
