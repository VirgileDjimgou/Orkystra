# Production Hardening

Sprint 10 introduces the first hardening pass for the MVP stack.

## Scope of this sprint

- API key authentication baseline for local and demo environments
- tenant resolution baseline with default local tenant support
- audit logging scaffold with correlation id propagation
- request metrics endpoint and readiness/liveness endpoints
- Docker packaging for API, frontend, AI service, and optimization service
- stack-level compose file for local deployment rehearsal
- smoke-test checklist for demos and staging handoffs

## API hardening posture

The API now includes:

- `GET /health/live`
- `GET /health/ready`
- `GET /observability/metrics`
- `GET /observability/context`

Protected routes require the configured API key header. Tenant resolution is performed before protected operational handling and supports:

- local single-tenant mode with a default tenant
- explicit tenant-header enforcement when required later

## Audit posture

The current audit scaffold logs:

- who
- what
- when
- tenant
- why
- source IP
- correlation id
- response status

This is log-based scaffolding, not a persisted audit store yet.

## Packaging posture

Current packaging artifacts:

- `backend/src/Orkystra.Api/Dockerfile`
- `frontend/web/Dockerfile`
- `python-services/ai-service/Dockerfile`
- `python-services/optimization-service/Dockerfile`
- `infrastructure/docker-compose.stack.yml`

## Current limitations

- no external identity provider yet
- no role or policy matrix beyond the baseline authenticated operator identity
- no persisted audit sink
- no distributed tracing exporter
- no secret management beyond environment-based configuration
