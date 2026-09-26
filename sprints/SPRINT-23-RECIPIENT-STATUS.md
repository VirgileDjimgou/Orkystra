# SPRINT-23 — Statut destinataire et notifications contrôlées

## Statut

`DONE — 2026-09-25`

## Preuves de clôture

- commit `94e4201` ; migrations `20260722235418_Sprint23RecipientStatus` et `20260913201321_Sprint23RecipientNotifications` ;
- `RecipientStatusIntegrationTests` couvre le lien public minimisé, révocation, consentement, correction de contact, isolation tenant et déduplication ;
- `.agent/QUALITY_REPORT.md` enregistre la gate complète du 2026-09-25 : build/format/tests backend, MinIO, SQL Server, simulation, Web/Vitest/Playwright, santé API et Android ;
- la présente réconciliation corrige uniquement les cases laissées décochées malgré les preuves existantes.

## Objectif

Réduire les appels de statut en permettant à un destinataire de consulter une information minimale et temporaire sur une mission.

## Valeur et constats traités

Met en œuvre prudemment l’option ETA/lien public de l’audit. Ce n’est pas un portail client : une page sans compte, limitée à une mission, ne montre ni flotte, ni conducteur détaillé, ni historique précis.

## Tâches principales

- générer lien opaque, expirant, révocable et limité à un statut de mission ;
- afficher fenêtre estimée prudente, progression autorisée et preuve de clôture minimale ;
- configurer consentement/canal, langue, horaires silencieux et modèles par tenant ;
- envoyer notifications transactionnelles via outbox avec déduplication ;
- masquer adresse, identité, position et contact selon politique de minimisation ;
- permettre correction de coordonnées sans exposer l’administration ;
- mesurer vues utiles, notifications délivrées et appels évités.

## Composants concernés

Dispatch, Tracking dérivé, Notifications, Web public minimal, Worker, audit et configuration tenant.

## Dépendances

Qualité tracking Sprint 21 et outbox fiable existante/Sprint 25.

## Tests et preuves requis

Expiration/révocation, token guessing, cache headers, minimisation, fuseaux/langues, déduplication, panne canal, accessibilité et charge de consultation.

## Critères d’acceptation

- [x] lien ne donne accès qu’à une mission et expire selon la politique ;
- [x] aucune position temps réel précise ni donnée d’un autre destinataire n’est exposée ;
- [x] ETA est présentée comme fenêtre avec fraîcheur et fallback sans tracking ;
- [x] une transition génère au plus une notification par canal configuré ;
- [x] révocation est effective immédiatement côté lecture ;
- [x] efficacité est mesurable sans profilage inutile du destinataire.

## Livrable démontrable

Un destinataire reçoit un lien, voit une fenêtre mise à jour, puis le lien devient inutilisable après livraison ou révocation sans exposer la carte flotte.

## Risques et rollback

Risque vie privée et ETA inexacte. Désactivation par tenant, fenêtres larges, données minimales et aucun indexage ; rollback par révocation globale des liens actifs.

## Estimation

L.
