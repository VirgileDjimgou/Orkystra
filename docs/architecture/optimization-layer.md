# Optimization Layer

Sprint 8 introduces the first explainable optimization service in `python-services/optimization-service`.

## Design principles

- Inputs are normalized into a canonical route model before solving.
- The solver must return both a route and a human-readable explanation.
- Infeasible plans are surfaced explicitly instead of being hidden behind a weak fallback.
- Alternative plans are part of the contract because dispatch trust depends on comparison, not just one opaque answer.

## Current scope

This sprint supports a narrow but real routing use case:

- one depot
- one vehicle
- multiple stops
- capacity constraint
- time windows
- route duration limit
- explainable alternatives

## Solver behavior

- Prefer OR-Tools when available at runtime.
- Fall back to a deterministic permutation-based evaluator when OR-Tools is unavailable.
- Preserve the same response contract across both backends.

## Current endpoints

- `GET /health`
- `GET /capabilities`
- `GET /optimize/demo`
- `POST /optimize`

## Product workflow integration

Sprint 22 connects the optimization service to the operator workspace through a bounded backend workflow:

- `Orkystra.Api` now exposes `POST /api/transport/routes/{routeId}/optimization`
- the endpoint gathers the current tenant-aware route detail projection and selected scenario id before calling the Python service
- the browser receives one dispatcher-facing optimization envelope instead of talking to the Python service directly
- the workflow falls back to a local comparison plan when the optimization service is unreachable, so the control tower keeps a usable recovery surface during local development

## Response contract

The main response contains:

- optimization status
- ordered stop references
- ETA minutes
- load distribution
- objective score
- constraint violations
- explanation
- alternative plans
- solver backend

## Current limitations

- single-vehicle sequencing only
- no pickup-and-delivery pairing yet
- no heterogeneous fleet search yet
- no persisted optimization run history yet
- no event publication yet
