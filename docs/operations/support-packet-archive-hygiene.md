# Support Packet Archive Hygiene

This guide explains how to keep repeated support packet retries tidy without losing the useful handoff trail.

## Archive before refresh

When the packet already contains a meaningful failed state, archive it before overwriting it with a new retry:

```powershell
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/refresh-support-issue.ps1 -PacketDirectory <packet-folder> -ApiKey <api-key> -ArchiveCurrentStateBeforeRefresh -RefreshReason "retry-after-reset"
```

That preserves the current packet snapshot under `archive/` before the new refresh is collected and validated.

## When to prune

Prune old archives when:

- the packet has accumulated many retries
- older attempts no longer represent a distinct failure mode
- the packet folder is becoming noisy for the maintainer handoff

Recommended command:

```powershell
powershell -ExecutionPolicy Bypass -File infrastructure/scripts/prune-support-issue-archives.ps1 -PacketDirectory <packet-folder> -KeepLatest 3
```

## Keep versus discard

Keep older archives when:

- they capture a different failure posture
- the escalation target changed over time
- a maintainer still needs to compare pre-reset and post-reset evidence

Discard older archives when:

- they are near-duplicates of the latest successful retry trail
- they only add noise without changing the debugging direction
- the current packet already validates cleanly and the older retries no longer help explain the issue

## Files that define archive hygiene

- `SUPPORT_ARCHIVE_INDEX.json`
- `SUPPORT_ATTEMPTS.json`
- `SUPPORT_MANIFEST.json`
- `MAINTAINER_HANDOFF.md`

Together they tell the next maintainer which packet states were kept, which were refreshed, and how much retry history is still active.
