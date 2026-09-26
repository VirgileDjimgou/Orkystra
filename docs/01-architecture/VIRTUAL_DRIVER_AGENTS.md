# Autonomous Virtual Driver Agents

Virtual drivers are deterministic Demo actors, not general-purpose AI. An agent has synthetic driver, vehicle, assignment, explicit lifecycle state, and constrained typed tools.

```text
AVAILABLE → ASSIGNED → PRE_TRIP → EN_ROUTE → AT_STOP
    → DELIVERING → COMPLETING → COMPLETED
                  ↘ BLOCKED / OFFLINE → recover through policy
```

`IAgentDecisionProvider` selects policy-driven actions. The default `DeterministicDriverDecisionProvider` is sufficient for the public demo. Future model providers may be added behind this port only after a separate decision; an external LLM is not a Demo dependency.

Tools are typed capabilities such as assignment lookup, inspection submission, mission start, telemetry emission, stop arrival, proof submission, completion, delay report, and vehicle-issue report. Agents receive neither unrestricted database access nor arbitrary HTTP access. Each action is tenant scoped, authorized, idempotent, auditable, and retry-safe.

The activity trace contains structured observed state, policy trigger, selected tool/action, and result. It must never store hidden reasoning or sensitive media payloads. Tenant-scoped activity may be presented through the existing realtime pattern in the cockpit dock.
