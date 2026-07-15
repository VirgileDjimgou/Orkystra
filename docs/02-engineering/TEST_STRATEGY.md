# Stratégie de test

## Pyramide

1. tests unitaires sur invariants, transitions et calculs ;
2. tests d'intégration avec SQL Server réel via conteneur ;
3. tests de contrat API et webhooks ;
4. tests composants Web/Android ;
5. parcours E2E critiques ;
6. scénarios de simulation multi-véhicules.

## Cas obligatoires

- isolation multi-tenant ;
- autorisations par rôle ;
- idempotence télémétrie ;
- ordre et duplication d'événements ;
- perte et retour réseau mobile ;
- conflit de synchronisation ;
- upload interrompu ;
- fuseaux horaires ;
- données invalides ;
- retry webhooks ;
- reprise après redémarrage du worker.

## Definition of Done

Une capacité est terminée quand :

- critères d'acceptation satisfaits ;
- tests automatisés pertinents verts ;
- scénario démontrable documenté ;
- erreurs et observabilité couvertes ;
- autorisation et tenant vérifiés ;
- documentation et état du projet mis à jour.
