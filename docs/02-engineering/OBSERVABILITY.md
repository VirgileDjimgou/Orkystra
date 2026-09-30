# Observabilité

## Logs

JSON structuré avec correlation ID, organization ID pseudonymisé, module et résultat. Ne pas journaliser tokens, signatures ou contenu complet des positions.

## Métriques MVP

- requêtes API et erreurs ;
- latence ingestion télémétrie ;
- appareils actifs/inactifs ;
- connexions SignalR ;
- longueur outbox ;
- webhooks en échec ;
- synchronisations Android ;
- espace stockage documents.

## Mètre applicatif `FleetOps` (Sprint 31)

Les instruments suivants sont créés dans `FleetOps.Core/Observability/FleetOpsMetrics.cs`, enregistrés via `.AddMeter("FleetOps")` dans l'API et le Worker, et exportés par OTLP lorsque `OTEL_EXPORTER_OTLP_ENDPOINT` est configuré :

| Instrument | Type | Tags | Usage |
| --- | --- | --- | --- |
| `fleetops.tracking.ingest.duration` | histogramme (ms) | `result` | latence d'ingestion canonique |
| `fleetops.tracking.ingest.events` | compteur | `result` (accepted, duplicate, out_of_order, rejected, error) | résultats d'ingestion |
| `fleetops.tracking.broadcast.duration` | histogramme (ms) | `outcome` | durée de diffusion SignalR |
| `fleetops.tracking.broadcast.events` | compteur | `outcome` (sent, failed) | diffusions SignalR |
| `fleetops.tracking.snapshot.duration` | histogramme (ms) | `surface` (positions, history) | lecture snapshot / catch-up |
| `fleetops.tracking.reset.duration` | histogramme (ms) | `outcome` | reset synthétique borné |
| `fleetops.tracking.reset.events` | compteur | `outcome` (completed, failed) | resets |
| `fleetops.tracking.signalr.connections` | jauge observable | — | connexions hub actives |
| `fleetops.demo.engine.tick.duration` | histogramme (ms) | `scenario` | tick du moteur Demo |
| `fleetops.demo.engine.telemetry.emitted` | compteur | `scenario` | points émis |
| `fleetops.demo.engine.state.save.duration` | histogramme (ms) | — | persistance du snapshot |
| `fleetops.demo.agent.step.duration` | histogramme (ms) | — | pas d'agent conducteur virtuel |
| `fleetops.demo.agent.steps` | compteur | `result` | pas d'agent par code résultat |
| `fleetops.public_demo.sessions` | compteur | `result` (launched, disabled, capacity, unavailable) | lancements publics |
| `fleetops.public_demo.controls` | compteur | `action` (START, PAUSE, RESET) | contrôles de session |

Les compteurs par tenant de `GET /api/v1/tracking/metrics` restent la surface de diagnostic tenant ; ils utilisent des incréments atomiques et distinguent désormais les points rejetés par le contrôle qualité des points en désordre.

## Traces

OpenTelemetry pour API, SQL, worker et appels externes à partir du sprint 09.
