# Project Decisions

## Active Decisions

- The repository remains sprint-driven, with one bounded sprint per default session.
- The current maturity target is a production-usable open-source release candidate, not a broad platform rewrite.
- Support packet quality, operator handoff, and evidence capture remain first-class product concerns.
- SQLite remains the default local persistence mode; PostgreSQL remains the recommended self-host posture.
- MQTT stays the event backbone for the packaged self-host flow.
- The next maturity increments should favor reusable operational flows over isolated visual polish.

## Remote Lineage Decisions

- The remote `main` was force-updated to an unrelated FleetOps hosted-demo lineage (`ea529f6`, 2026-09-30) with no common ancestor with the local Orkystra release-candidate history; remote history must not be rewritten before a human decides the canonical lineage.
- The Sprint 201 release-memory commit (`4453898`) is preserved on remote branch `release/rc-preflight-cleanup` until the lineage decision is made.
- No `v0.1.0-rc.1` tag may be cut until CI is confirmed on the agreed canonical lineage.
- Resolved 2026-10-02: the remote FleetOps lineage is canonical. Local `main` was reset to `origin/main`; the Orkystra release-candidate lineage is archived on `archive/orkystra-rc-lineage` and remote `release/rc-preflight-cleanup`, frozen and not merged. CI is confirmed green on the canonical lineage (`36993883034`, `36994528056`). The Orkystra `v0.1.0-rc.1` tag is superseded and must not be cut from the archive.

## Session Continuity Decisions

- `AGENTS.md` defines the repository workflow.
- `CURRENT_STATE.md` is the compact restart brief for new sessions.
- `PROJECT_STATUS.md` is the detailed operational history.
- `IMPLEMENTATION_ROADMAP.md` is the sprint ledger and next-work source.
- `Continue the Orkystra project.` is the canonical continuation command across tools.
