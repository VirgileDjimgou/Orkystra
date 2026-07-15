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

## Traces

OpenTelemetry pour API, SQL, worker et appels externes à partir du sprint 09.
