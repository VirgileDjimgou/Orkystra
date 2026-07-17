# Pilot Onboarding Guide

## Objective

Enable three pilot users to operate FleetOps safely with a predictable setup and support routine.

## Recommended personas

- one `Admin` for security, users, integrations, and data lifecycle;
- one `Operator` for dispatch, map, alerts, and proof review;
- one `Driver` using the Android application.

## Day-zero checklist

1. Confirm the target organization name and slug.
2. Validate backup creation and one dry-run restore on the pilot environment.
3. Enable administrator MFA and store recovery codes outside the application.
4. Open **Administration > Guided setup** and download the CSV templates.
5. Preview each import, correct every line-level error, and explicitly confirm the validated rows.
6. Invite an Operator and link the Driver invitation to an imported driver profile.
7. Generate the short-lived pairing code and enter it in the Android application within ten minutes.
8. Verify at least one active device assignment and any required compliance documents.
9. Complete one dispatch mission end to end with inspection and delivery proof.
10. Remove the optional sample data set before real operations begin.
11. Configure one integration credential and one sandbox webhook if external supervision is required.

The setup page saves the latest import preview so an administrator can leave and resume without importing unconfirmed data. Reconfirming a completed preview reports the original result and does not duplicate fleet records.

## Pilot training flow

- `Admin`: user management, MFA, exports, controlled purge, integrations.
- `Operator`: fleet map, alert triage, mission creation and lifecycle control.
- `Driver`: sign in, mission cache refresh, offline action sync, inspection and proof capture.

## Self-service activation walkthrough

The Sprint 15 acceptance walkthrough covers ten tasks: MFA guidance, template download, preview, error correction, confirmation, invitations, driver-profile linkage, Android pairing, readiness review, and first mission completion. Nine are completed directly from Guided setup without synchronous support; mission execution continues in the existing Dispatch and Driver workspaces. This gives a 90% self-service result while keeping operational execution in its role-specific surface.

If setup stalls, export the privacy-minimal diagnostics from Guided setup. The payload contains organization-scoped counts and technical readiness only; it does not contain names, email addresses, invitation tokens, pairing codes, or session credentials.

## Acceptance evidence

- screenshots or logs for `/health/ready`;
- one SQL backup artifact in `backups/`;
- one successful MFA enablement for the admin account;
- one tenant export JSON package;
- one completed mission with inspection and delivery proof;
- one alert scan and one webhook delivery trace.
- one activation-metrics snapshot showing start, abandonment, import errors, and first-value time without personal data.
