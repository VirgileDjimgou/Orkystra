# Support Packet Review

This guide explains how a maintainer should accept, reject, or request more evidence for an Orkystra support packet.

## Entry points

Use one of these flows:

- operator workspace support intake plus support bundle export
- `infrastructure/scripts/package-support-issue.ps1`
- `infrastructure/scripts/validate-support-issue.ps1`

## Minimum acceptance rule

A support packet is acceptable for first-line debugging when:

- `support-bundle.json` is present
- `ISSUE_DRAFT.md` is present
- `SUPPORT_MANIFEST.json` is present
- `SUPPORT_VALIDATION.json` is present
- the validation status is `Accepted` or `AcceptedWithWarnings`

## Quick maintainer pass

1. Open `SUPPORT_VALIDATION.json`.
2. Check the `status`.
3. Read `SUPPORT_LIFECYCLE.md` for packet class, triage lane, next owner, release context, runtime context, and the latest delta.
4. Read `MAINTAINER_HANDOFF.md`.
5. Read `ISSUE_DRAFT.md`.
6. Inspect `support-bundle.json` only after the narrative and validation posture are clear.

## Reject conditions

Reject or return the packet for more detail when:

- validation status is `Rejected`
- reproduction steps are still vague
- the actual result is missing
- there is no usable workflow or audit evidence
- the packet includes secrets or irrelevant raw data

## Warning posture

`AcceptedWithWarnings` means the packet may still be usable, but the maintainer should expect one of these gaps:

- weak workflow evidence
- weak audit evidence
- manifest mismatch
- thin runtime context despite a valid file set

## Reuse versus refresh

Use `SUPPORT_VALIDATION.json` as the first decision point:

- `recommendedAction: Reuse` means the packet is coherent enough to keep for the next maintainer pass.
- `recommendedAction: Refresh` means the packet should be refreshed after the next retry or reset, not thrown away immediately.
- `recommendedAction: Regenerate` means the packet is too incomplete or misleading to keep as the main handoff artifact.

## Triage fields

The lifecycle summary is now also the quickest way to understand:

- `packetClass`: what kind of support packet this currently is
- `triageLane`: which troubleshooting lane should look first
- `nextOwner`: who should own the next pass
- `triageChecks`: the short list of questions or checks to run before deeper debugging
- `releaseContext`: which branch, commit, and release hint produced the packet
- `runtimeContext`: which host and shell posture produced the capture
- `contextDrift`: whether the latest retry still represents the same repo and runtime state as the previous attempt
- `evidenceProvenance`: which artifacts are mandatory, which are optional, and in what order they should be read
- `evidenceGapScore`: which proof category is strong, weak, or absent and what to strengthen first

If these fields point to operator refresh or configuration review, treat that as a signal to avoid spending maintainer time on deep workflow debugging yet.

## Release posture link

When judging whether a packet is enough for a deeper debugging pass, align it with:

- [docs/operations/post-release-candidate-checkpoint.md](post-release-candidate-checkpoint.md)

That checkpoint remains the source of truth for what the current OSS posture supports and what still falls outside the supported promise.
