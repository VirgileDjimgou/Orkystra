# SPRINT-25 — Hub d’intégrations fiable et exploitable

## Objectif

Permettre à un administrateur de comprendre, tester et récupérer ses échanges externes sans intervention développeur ni accès aux données d’un autre tenant.

## Valeur et constats traités

Consolide API keys, webhooks, imports et connecteurs existants. La valeur est une interopérabilité prévisible, observable et supportable, pas une marketplace de centaines d’intégrations.

## Tâches principales

- unifier catalogue de connexions, scopes, propriétaire, environnement et statut ;
- versionner contrats, calendrier de dépréciation et compatibilité des alias API ;
- améliorer webhooks : signatures rotatives, retry, DLQ, replay ciblé et historique filtrable ;
- ajouter mappings de champs simples et validation sandbox pour imports/exports ;
- fournir diagnostics corrélés, payload redigé, reçus et test de connectivité ;
- appliquer quotas par tenant/clé, pagination curseur et idempotency keys publiques ;
- publier guide partenaire, exemples et tests de contrat automatisables.

## Composants concernés

Integrations, API v1, Worker/outbox, Web Admin, audit, docs et observabilité.

## Dépendances

Sprints 22–24 et décision sur les intégrations réellement demandées.

## Tests et preuves requis

Rotation/revocation, scopes, quota, versioning, replay DLQ, redaction, panne process/SQL, tenant, compatibilité et E2E sandbox→livraison.

## Critères d’acceptation

- [ ] un administrateur teste et diagnostique une connexion sans accès serveur ;
- [ ] replay ciblé reste idempotent et audité ;
- [ ] clé limitée ne dépasse jamais ses scopes ni son tenant ;
- [ ] dépréciation expose délai, remplacement et télémétrie d’usage ;
- [ ] erreurs externes n’empêchent pas le cœur mission/driver de fonctionner ;
- [ ] un partenaire valide son contrat via kit automatisé.

## Livrable démontrable

Un webhook échoue, passe en DLQ, est corrigé/testé puis rejoué une fois depuis le hub avec signature rotative et timeline complète.

## Gate de décision et rollback

Poursuivre l’industrialisation seulement si un connecteur réel est exploitable et si les métriques montrent une réduction d’appels, de ressaisie ou du temps de clôture. Chaque intégration reste désactivable isolément.

## Estimation

XL.
