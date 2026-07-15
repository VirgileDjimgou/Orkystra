# Modèle de domaine initial

```text
Organization
├── Users / Roles
├── Drivers
├── Vehicles
│   ├── GpsDevices
│   ├── TelemetryPoints
│   ├── Inspections
│   ├── Defects
│   └── MaintenanceSchedules
├── Missions
│   ├── MissionStops
│   ├── Assignments
│   ├── MissionEvents
│   └── DeliveryProofs
├── Alerts
├── Integrations
└── AuditEntries
```

## Invariants essentiels

- un appareil actif n'est affecté qu'à un véhicule actif à la fois ;
- un conducteur ne peut exécuter qu'une mission qui lui est affectée ;
- les transitions de mission suivent une machine d'état explicite ;
- une preuve appartient à une étape et à une organisation ;
- un point GPS possède un identifiant idempotent ou une clé appareil/horodatage ;
- les dates métier sont stockées en UTC et affichées dans le fuseau de l'organisation ;
- la suppression des données opérationnelles est logique ou auditée.
