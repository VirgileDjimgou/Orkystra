# Support Packet Refresh Loop

This guide explains how to keep a support packet coherent when you retry an issue, reset the local environment, or rebootstrap the packaged self-host demo.

## When to reuse

Reuse the current support packet when:

- the issue is unchanged
- the packet already validates as `Accepted`
- the current support bundle still reflects the latest failing state

## When to refresh

Refresh the current support packet when:

- you retried the same issue after a reset or rebootstrap
- runtime evidence changed
- the packet validates as `AcceptedWithWarnings`
- the support bundle needs stronger workflow or audit evidence

## When to regenerate

Regenerate instead of refreshing when:

- validation is `Rejected`
- the issue changed into a different failure mode
- the tenant or environment changed materially
- the packet contains stale or misleading operator narrative

## Reset and refresh sequence

1. Reset or rebootstrap the local environment if needed:

```powershell
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/reset-demo-state.ps1 -Rebootstrap
```

2. Refresh the existing support packet:

```powershell
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/refresh-support-issue.ps1 -PacketDirectory <packet-folder> -ApiKey <api-key> -ArchiveCurrentStateBeforeRefresh -RefreshReason "retry-after-reset"
```

3. Re-check the validation result in `SUPPORT_VALIDATION.json`.
4. Review the latest entry in `SUPPORT_ATTEMPTS.json`.
5. Hand off the packet only if the refreshed packet still matches the current issue.

## Files that matter after refresh

- `SUPPORT_MANIFEST.json`
- `SUPPORT_ATTEMPTS.json`
- `SUPPORT_VALIDATION.json`
- `MAINTAINER_HANDOFF.md`

These files tell the next maintainer whether the packet was reused, refreshed after a retry, or should be regenerated from scratch.

For longer-running investigations, also use:

- [docs/operations/support-packet-archive-hygiene.md](support-packet-archive-hygiene.md)
