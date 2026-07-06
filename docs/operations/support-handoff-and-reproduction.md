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
