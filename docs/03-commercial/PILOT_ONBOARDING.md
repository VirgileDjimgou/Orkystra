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
4. Import or create the first vehicles, drivers, and GPS devices.
5. Verify at least one active device assignment.
6. Validate one dispatch mission end to end with delivery proof.
7. Configure one integration credential and one sandbox webhook if external supervision is required.

## Pilot training flow

- `Admin`: user management, MFA, exports, controlled purge, integrations.
- `Operator`: fleet map, alert triage, mission creation and lifecycle control.
- `Driver`: sign in, mission cache refresh, offline action sync, inspection and proof capture.

## Acceptance evidence

- screenshots or logs for `/health/ready`;
- one SQL backup artifact in `backups/`;
- one successful MFA enablement for the admin account;
- one tenant export JSON package;
- one completed mission with inspection and delivery proof;
- one alert scan and one webhook delivery trace.
