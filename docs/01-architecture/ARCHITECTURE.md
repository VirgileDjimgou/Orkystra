# Architecture

## Vue générale

```text
Vue Web Admin/Operator ─┐
Android Driver ─────────┼─> ASP.NET Core API ─> SQL Server
GPS Simulator/Devices ──┘          │             │
                                   ├─ SignalR     ├─ Outbox
                                   ├─ Worker      └─ Audit
                                   ├─ MinIO/S3
                                   └─ Webhooks/External APIs
```

## Backend

- `FleetOps.Api` : composition root, HTTP, auth, SignalR.
- `FleetOps.Core` : modules métier, cas d'utilisation et contrats internes.
- `FleetOps.Infrastructure` : EF Core, stockage, fournisseurs et implémentations.
- `FleetOps.Worker` : outbox, notifications, imports et tâches différées.

## Modules

Identity, Organizations, Fleet, Tracking, Dispatch, Inspections, Documents, Alerts, Integrations et Audit.

## Simplicité volontaire

- un déploiement backend principal ;
- une base SQL ;
- pas de bus externe au début ;
- outbox SQL pour les effets externes ;
- séparation logique vérifiée par tests d'architecture ;
- extraction en service uniquement après mesure d'un problème réel.

## Multi-tenancy

Base partagée et schéma partagé. Toute donnée métier porte `OrganizationId`. Le tenant provient d'un claim signé. Les tests d'intégration vérifient qu'une organisation ne peut jamais lire ou modifier les données d'une autre.

## Temps réel

SignalR transporte uniquement les dernières positions, changements d'état et alertes. L'historique est obtenu par REST.

## Télémétrie

Le MVP accepte HTTP JSON idempotent. MQTT est un adaptateur optionnel. La télémétrie brute est append-only, la position actuelle est matérialisée séparément et la rétention est configurable.
