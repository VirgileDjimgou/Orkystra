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

## Sprint 28 implementation

The default `DeterministicAgentDecisionProvider` maps the explicit lifecycle to typed actions and injects repeatable delay, vehicle-issue, and connectivity-loss branches from the Demo scenario and logical tick. `VirtualDriverAgentCoordinator` advances agents concurrently; a retryable failure preserves state and reuses the same idempotency key, while a non-retryable failure enters `BLOCKED`.

`HttpVirtualDriverTools` owns the scoped Driver/Operator sessions and exposes no URL or token to an agent. It invokes the existing inspection, mission-command, upload/proof and delay endpoints. A one-pixel synthetic proof is clearly labelled and remains in private media storage. The optional Worker scheduler is disabled whenever the Demo engine is disabled and is bounded to 20 agents.

`AgentActivity` stores only tenant, agent/resource identifiers, sequence, observed state, policy, action, result code/message and timestamp. The authenticated `/api/v1/demo/agent-activities` capability derives the tenant from the session, validates referenced resources, deduplicates `(OrganizationId, AgentId, Sequence)`, writes the normal audit log and notifies the existing tenant Operations SignalR group. The cockpit's **Virtual drivers** tab refreshes on that signal. No prompt, hidden reasoning, media content, password or access token enters the activity record.
