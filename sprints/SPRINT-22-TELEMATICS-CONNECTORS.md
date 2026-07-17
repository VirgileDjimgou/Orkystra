# SPRINT-22 — Cadre de connecteurs et premier fournisseur télématique

## Objectif

Connecter un fournisseur ou appareil validé sans coupler le domaine FleetOps à son protocole, son payload ou son modèle d’authentification.

## Valeur et constats traités

Traite AUD-011 de façon fondée sur un besoin réel. Le produit devient une couche opérationnelle au-dessus d’une télématique existante, tout en conservant HTTP comme voie par défaut et MQTT uniquement si le pilote l’exige.

## Tâches principales

- définir contrat d’adaptateur pour identité appareil, position, odomètre, heartbeat et diagnostics ;
- isoler normalisation unités/fuseaux/qualité et mapping fournisseur→device tenant ;
- créer onboarding de connexion, validation des credentials et test de flux ;
- implémenter un premier connecteur choisi après Sprint 20 (webhook/polling, ou MQTT justifié) ;
- gérer replay, ordre, rate limit, backoff, dead-letter et reprise curseur ;
- exposer santé, dernier succès, erreurs actionnables et commandes de resynchronisation ;
- fournir kit de contrat, simulateur fournisseur et procédure de rotation de secret.

## Composants concernés

Integrations, Tracking, Devices, Worker, secret configuration, Web Admin et tests de contrat.

## Dépendances

Sprint 21 et partenaire réel identifié ; décision D-006 reste applicable.

## Tests et preuves requis

Contract tests, payloads versionnés, auth/rotation, replay, panne réseau, throttling, poison message, tenant mapping, charge et test sandbox fournisseur.

## Critères d’acceptation

- [ ] le domaine reçoit un modèle canonique sans référence au SDK fournisseur ;
- [ ] un payload rejoué ne crée aucun point ni événement en double ;
- [ ] credentials sont chiffrés/référencés et jamais journalisés ;
- [ ] un administrateur comprend l’état et peut relancer sans accès serveur ;
- [ ] connecteur reprend après panne sans perdre son curseur ;
- [ ] désactiver le connecteur n’affecte ni simulateur ni API device existante.

## Livrable démontrable

Un flux sandbox du fournisseur alimente la carte et les zones, subit une panne simulée, reprend sans doublon et affiche sa santé dans l’administration.

## Risques et rollback

Dépendance et changements de contrat fournisseur. Encapsuler, versionner les fixtures et garder ingestion canonique ; feature flag et désactivation par tenant servent de rollback.

## Estimation

XL.
