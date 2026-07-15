# API et intégrations

## Conventions REST

- préfixe `/api/v1` pour les contrats externes ;
- Problem Details pour les erreurs ;
- pagination par curseur pour télémétrie et événements ;
- `Idempotency-Key` pour ingestion et commandes externes ;
- ETag ou version de ligne pour les mises à jour concurrentes ;
- UTC ISO-8601 ;
- OpenAPI généré et testé.

## Endpoints MVP indicatifs

```text
POST /api/v1/auth/login
GET  /api/v1/vehicles
POST /api/v1/vehicles
POST /api/v1/telemetry
GET  /api/v1/tracking/latest
GET  /api/v1/tracking/history
POST /api/v1/missions
POST /api/v1/missions/{id}/assign
POST /api/v1/missions/{id}/events
POST /api/v1/inspections
POST /api/v1/delivery-proofs
POST /api/v1/webhooks/subscriptions
```

## Webhooks

Événements : `vehicle.position.changed`, `mission.status.changed`, `inspection.failed`, `delivery.proof.created`, `alert.raised`.

- signature HMAC ;
- retries exponentiels ;
- dead-letter interne ;
- journal de livraison ;
- secret remplaçable.

## Compatibilité

Les contrats externes sont append-only autant que possible. Toute rupture nécessite nouvelle version, période de migration et test de contrat.
