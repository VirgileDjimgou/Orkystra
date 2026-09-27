# Rapport de qualité

## Sprint 27 — Hosted Demo Engine & Realistic Virtual Fleet

- Statut : `PASSED — 2026-09-27`; journal `.runtime/sprint27-quality-gate.log`.
- Backend : format/analyse sur 291 fichiers, build Release sans avertissement, 155 tests rapides, 1 test MinIO et 3 tests SQL Server passants.
- Preuves Sprint : 8 tests Demo couvrent catalogue, horloge, rejeu identique de 12 véhicules/120 événements, bornes, snapshot/reprise, ingestion et mission canoniques, et rejet inter-tenant.
- Simulation : 33 étapes multi-tenant passantes ; Northwind expose 12 véhicules/appareils synthétiques, les autres tenants restent isolés.
- Web/runtime : 28 Vitest, build, 6 Playwright, GPS et santé API passants.
- Android : lint, tests unitaires et APK application/instrumentation passants ; connecté non configuré et non requis pour ce sprint backend.
- Sécurité : moteur désactivé par défaut, activation limitée à `RuntimeMode=Demo`, sandbox obligatoire, flotte résolue par tenant synthétique, token Mission non journalisé, aucune écriture SQL directe.

## Sprint 27 — checkpoint partiel et méthodologie visible

- Statut : `PARTIAL/IDLE — 2026-09-27`; aucune gate complète n'est revendiquée et aucun worker ne tourne en arrière-plan.
- Backend ciblé : build Release de `FleetOps.Worker` passé avec 0 avertissement/0 erreur ; `DemoScenarioEngineTests` passés (3/3).
- Orchestration : compilation Python, parse des scripts PowerShell et `git diff --check` passés. La commande `sprint-dashboard.ps1` affiche sprint, reste à faire, verrou, dernière activité, diff et journal.
- Restant Sprint 27 : repository/fixtures réalistes, catalogue complet, intégration effective télémétrie/mission, reprise Worker, isolation tenant, sandbox des effets, preuve 10–20 véhicules et gate complète.

## Sprint 26 — Fleet Map Semantics & Workflow Integration

- Statut : `PASSED — 2026-09-26`.
- Web : marqueurs accessibles, légende, focus mission/exception, trail sélectionné et configuration de tuiles couverts par tests ; lint, Vitest, build et 6 parcours Playwright passants.
- Gate canonique : `.runtime/sprint26-quality-gate.log` confirme format/build/tests .NET, MinIO, SQL Server, GPS, simulation multi-tenant, Web/E2E, health/readiness et Android build. Android connecté non configuré.
- Sécurité : configuration de tuiles sans identifiant métier ni secret ; trail et contexte restent issus des réponses tenant-filtrées existantes.

## Sprint 25 — Map-First Operations Cockpit

- Statut : `PASSED — 2026-09-26`.
- Web : Prettier, ESLint, 26 tests Vitest, build de production et 6 parcours Playwright passants. Les nouvelles preuves couvrent la sélection cockpit, l'inspecteur, le dock redimensionnable/repliable, la connexion vers `/`, et le bookmark `/map?vehicleId=...&missionRef=...`.
- Gate canonique : `pwsh -ExecutionPolicy Bypass -File scripts/quality-gate.ps1` passée : format/build/tests .NET (fast, MinIO et SQL Server), GPS dry-run, simulation multi-tenant complète, Web format/lint/tests/build/Playwright, API health/readiness et Android lint/unit/APK build. Le test Android connecté est resté non configuré.
- Sécurité/multi-tenant : le cockpit ne reçoit que des stores et réponses API déjà tenant-filtrés ; l'identité reste l'unique source de l'organisation. Aucun identifiant libre transmis par l'UI ne produit d'autorisation ou d'accès à une nouvelle ressource.

## Sprint 24 — Real-Time Hardening

- Statut : `PASSED — 2026-09-26`.
- Backend : `dotnet build FleetOps.slnx --no-restore -c Release -m:1 /nodeReuse:false` passed with 0 warning and 0 error. The six `TrackingIntegrationTests` passed, including actual snapshot quality metadata and bounded retention batches.
- Web : four new Vitest regression tests passed (two-vehicle coalescing, reconnect catch-up ordering, live quality transition, stale update rejection); Prettier, ESLint, and production build passed.
- Security/multi-tenant : tenant-scoped SignalR group and snapshot filter remain unchanged; metadata is derived only from the canonical accepted current position. Retention does not accept client tenant input and only deletes points older than configured policy.
- Canonical gate : `scripts/quality-gate.ps1` passed after the formatter command was updated to `--no-restore --verbosity diagnostic`; it verified 278 files without changes, backend format/build/147 fast tests/MinIO/SQL Server, GPS, 33-step simulation, Web format/lint/25 Vitest tests/build/5 Playwright flows, API health/readiness, and Android lint/unit/APK build. Android connected tests were correctly skipped because no device was configured.

## Rebaseline Demo Readiness — 2026-09-26

- Statut : `PARTIAL — planning validation passed; full product quality gate not concluded in this execution`.
- Audit : Sprint 23 est confirmé `DONE` par le commit `94e4201`, les migrations `20260722235418_Sprint23RecipientStatus` / `20260913201321_Sprint23RecipientNotifications`, les tests `RecipientStatusIntegrationTests`, et la gate complète enregistrée le 2026-09-25. Les cases de sa fiche ont été réconciliées.
- Régressions confirmées et différées au Sprint 24 : buffer SignalR global avec perte inter-véhicules, metadata de qualité absente du push, store live incomplet, purge de rétention matérialisée dans le hot path, et couverture reconnect insuffisante.
- Modifications limitées au planning, documentation et orchestration ; aucune API, migration, schéma, logique métier ou comportement produit n'a été changé.
- Validations passées : JSON/Markdown/références, 33 contrats actifs sans doublon, 7 plans archivés explicitement superseded, compilation Python, 4 tests unitaires de l'orchestrateur, parse PowerShell et `git diff --check`.
- Gate complète : démarrée avec succès (Git/outil .NET/compose/MinIO/recovery/restore) mais sans sortie de fin exploitable après le formatage .NET; ne pas la considérer verte. Relancer `scripts/quality-gate.ps1` avant le prochain checkpoint produit.

## Sprint 23 — Statut destinataire et notifications contrôlées

- Statut : `PASSED` — 2026-09-25.
- Backend : format, build Release sans avertissement, tests rapides, 1 contrat MinIO et 3 preuves SQL Server/Testcontainers passants ; modèle EF synchronisé avec la migration `20260913201321_Sprint23RecipientNotifications`.
- Web : format, lint, tests Vitest, build Vite et 5 scénarios Playwright passants.
- Simulation : scénario multi-tenant complet et santé API/readiness passants.
- Android : lint, tests unitaires et APK compilés ; test connecté non requis car aucun code Android n'a changé.
- Sécurité : token public SHA-256 seul, lecture minimisée (ni adresse, ni conducteur, ni position), `404` uniforme, rate limit, `no-store`, indexation publique interdite par `robots.txt` ; révocation immédiate ; préférences et métriques Admin-only tenant-scoped ; e-mail destinataire protégé par Data Protection et jamais journalisé ; outbox dédupliquée par clé unique (organisation, clé) et reprise bornée avec dead-letter.
- Limites : le canal e-mail est un adaptateur de développement ; les clés Data Protection sont partagées par volume en compose pilote (pas de KMS externe).

## Sprint 22 — Sandbox Telematics Provider

- Statut : `PASSED` — 2026-07-22T23:42:00Z.
- Backend : format, build Release, 143 tests rapides, 1 MinIO et 3 SQL Server passants.
- Web : format, lint, 20 Vitest, build Vite et 5 Playwright passants.
- Démonstration : simulation multi-tenant 33 étapes, GPS dry-run, santé API/readiness et build Android passants.
- Sécurité : API key Device tenant-scoped, création/activation Admin-only, replay dédupliqué, curseur et santé isolés par `OrganizationId`; aucun secret journalisé.
- Android connecté ignoré conformément au périmètre Web/backend du sprint.

## Sprint 22 — exécution partielle

- API formatée et compilée sans avertissement après ajout de l'adaptateur HTTP `sandbox-telematics.v1`.
- 10 tests d'intégration Tracking/Integration existants passants.
- Le sprint n'est pas clôturé : état de connexion, reprise de curseur, dead-letter, contrôles Admin, simulateur et gate complète restent requis.

## Dernière exécution — Sprint 21

- Statut : `PASSED`
- Date UTC : `2026-07-22T15:45:00Z`
- Backend : format, build Release sans avertissement, 142 tests rapides, 1 MinIO et 3 SQL Server/Testcontainers passants.
- Simulation : 33 étapes passantes sur les trois tenants et neuf rôles fictifs, dont les trois preuves croisées `404`.
- Web : format, lint, 20 tests Vitest, build Vite et 5 E2E Playwright passants.
- Android : lint, tests unitaires et APK de test compilés; test connecté ignoré conformément au périmètre Web/backend du sprint.
- Sécurité : toutes les routes tracking publiques résolvent le tenant depuis l'identité; zones et recalcul sont Admin-only; les lectures Admin/Operator restent filtrées côté serveur.

## Dernière exécution — Sprint 21 (en cours)

- Statut : `PARTIAL — NOT A PASSING GATE`
- Date UTC : `2026-07-22`
- Backend : `dotnet format` ciblé, build API et 6 tests Tracking verts.
- Gate : Docker/MinIO privé a démarré correctement. La gate complète a ensuite dépassé la limite d'exécution de l'agent pendant les suites longues, sans erreur fonctionnelle retournée ; elle doit être relancée sans limite avant clôture.
- Sécurité : les nouveaux reads sont dérivés de l'identité et filtrés par `OrganizationId`; les écritures zones/recalcul sont Admin-only. Android non exécuté : aucun code Android n'a été modifié.

## Dernière exécution — Simulation complète Sprint 20

- Statut : `PASSED`
- Date UTC : `2026-07-18T14:25:01Z`
- Backend : format, build Release sans avertissement, 142 tests rapides, 1 contrat MinIO et 3 preuves SQL Server/Testcontainers passants.
- Simulation : 33 étapes passantes via les API réelles pour 3 tenants fictifs, 9 utilisateurs Admin/Operator/Driver, 9 véhicules/appareils, mission et preuve, maintenance, conformité, opérations, intégrations, consentement et agrégats.
- Web : format, lint, 20 tests Vitest, build Vite et 5 scénarios Playwright passants ; 4 captures de simulation régénérées, dont le flux SignalR actif Southridge.
- Android : lint, tests unitaires, APK et instrumentation compilés ; 5 tests connectés passants sur Samsung SM-G975F Android 12 ; connexion Driver et captures mission liste/détail validées par ADB.
- Runtime : GPS dry run et API health/readiness passants.
- Sécurité et multi-tenant : Operator/Driver refusés en `403` sur le pilot Admin ; découverte croisée de mission refusée en `404` dans les trois directions ; tenant issu exclusivement de l'identité ; aucun token dans les rapports.

Les rapports portent `SIMULATED DEVELOPMENT EVIDENCE — NOT PILOT OR COMMERCIAL PROOF`. Les critères externes d'adoption, d'amélioration opérationnelle et de volonté de payer restent `NON VÉRIFIÉS`.

## Dernière exécution — Revalidation Sprint 20

- Statut : `PASSED`
- Date UTC : `2026-07-18T12:17:52Z`
- Dépôt : état Git propre avant la gate ; Docker Compose standard et pilote, parse des scripts de recovery et stockage MinIO privé validés.
- Backend : format, build Release sans avertissement, 131 tests rapides, 1 contrat MinIO et 3 preuves SQL Server/Testcontainers passants.
- Web : format, lint, 20 tests Vitest, build Vite et 5 scénarios Playwright passants.
- Runtime : GPS dry run, API health/readiness et build Android passants. Android connecté non exécuté conformément au périmètre : Sprint 20 ne modifie aucun code Android.
- Sécurité et multi-tenant : les parcours E2E conservent l'isolation d'une mission Northwind face à un second tenant ; la couverture Sprint 20 vérifie consentement requis, rôle Admin, export tenant-scoped et agrégats sans PII.

Les critères d'adoption commerciaux externes restent `NON VÉRIFIÉS` et ne sont pas simulés par cette revalidation.

## Dernière exécution — Sprint 20 (capacité produit)

- Statut : `PASSED`
- Date UTC : `2026-07-17T22:46:07Z`
- Backend : format, build Release, 131 tests rapides, 1 contrat MinIO et 3 preuves SQL Server/Testcontainers passants ; modèle EF synchronisé avec la migration `20260717223900_Sprint20PilotDailyMetrics`.
- Web : format, lint, 20 tests Vitest, build Vite et 5 scénarios Playwright passants.
- Runtime : GPS dry run, API health/readiness et build Android passants; Android connecté non requis car Sprint 20 ne modifie pas l’application Android.
- Sécurité : routes pilote Admin-only, tenant issu exclusivement de l’identité, consentement requis avant collecte, agrégats sans données personnelles et export filtré par tenant.

## Démonstration Sprint 20

- Un Admin sans consentement reçoit `409` lorsqu'il tente de collecter une métrique; après consentement explicite, il enregistre le snapshot quotidien.
- Une seconde collecte le même jour rafraîchit le même agrégat tenant, sans doublon.
- L'Admin enregistre et résout un incident P1, consigne une décision `GO` et exporte uniquement consentement, agrégats, incidents et décisions de son organisation.
- Les deux autres organisations de test ne voient ni l'incident, ni les snapshots, ni la décision du tenant Northwind, et une résolution cross-tenant retourne `404`.

## Limites honnêtes Sprint 20

- Les prérequis externes (trois organisations onboardées, deux semaines d'usage, amélioration mesurée et intention commerciale) sont `NON VÉRIFIÉS`; ils ne sont pas simulés par les tests produit.
- Les compteurs de mission/preuve sont des totaux opérationnels au moment du snapshot; les compteurs d'activation, activité conducteur et synchronisation sont calculés pour le jour UTC courant.

## Dernière exécution — Sprint 19

- Statut : `PASSED`
- Date UTC : `2026-07-17T21:23:22Z`
- Backend : format, build Release, 129 tests rapides, 1 contrat MinIO et 3 preuves SQL Server/Testcontainers passants.
- Web : format, lint, 19 Vitest, build Vite et 5 scénarios Playwright passants.
- Runtime : GPS dry run, API health/readiness et build Android passants; Android connecté non requis car Sprint 19 ne modifie pas l’application Android.
- Sécurité : chaque template, import, vue, board et action bulk est filtré par l’organisation de l’identité; les affectations sont revalidées pour version, collisions, maintenance, conformité et rôle Admin pour un override.

## Démonstration Sprint 19

- L’opérateur enregistre un template tenant-scoped, le duplique à une date choisie et obtient un brouillon de mission avec ses arrêts.
- Il prévisualise un import, le confirme explicitement, puis le rejoue : la receipt tenant-scoped retourne `wasDuplicate` sans recréer de mission.
- Le workspace Web affiche le board jour/semaine, conserve les interactions clavier via champs et boutons natifs, et montre les erreurs avant confirmation.
- Une affectation bulk ne s’applique que lorsque toutes les missions sélectionnées passent les contrôles de concurrence, disponibilité, immobilisation, conformité et chevauchement; chaque écriture est auditée.

## Dernière exécution — Sprint 18

- Statut : `PASSED`
- Date UTC : `2026-07-17T20:53:42Z`
- Backend : format, build Release, 126 tests rapides, 1 contrat MinIO et 3 preuves SQL Server/Testcontainers passants.
- Web : format, lint, 19 Vitest, build Vite et 5 scénarios Playwright passants.
- Runtime : GPS dry run, API health/readiness, Android lint/unit/build et 5 tests d’instrumentation sur Samsung SM-G975F passants.
- Sécurité : politiques et documents scannés dans le tenant authentifié, média vérifié dans le tenant, revue/override audités, tâche Android idempotente persistée dans Room.

## Démonstration Sprint 18

- L’Admin configure un type de document et une politique bloquante, consulte les risques de la matrice et exporte l’audit CSV ; l’avertissement explique que la règle est une configuration client, non un conseil juridique.
- Une échéance à 13 jours produit une unique exception au palier 14 jours ; le scan rejoué ne la duplique pas.
- Le remplacement conserve le document historique mais seule la version nouvelle reste active dans la matrice.
- Une campagne ciblée est récupérée, conservée offline par l’app Driver, puis soumise avec une commande idempotente lors du retour réseau.
- Une affectation non conforme est refusée ; un Admin peut seulement la poursuivre avec un motif d’override audité.

## Dernière exécution

- Statut : `PASSED`
- Date UTC : `2026-07-17T20:00:00Z`
- Sprint validé : `SPRINT-17` (`DONE`).
- Backend : format, build Release, 122 tests rapides, 1 test MinIO réel et 3 tests SQL Server/Testcontainers passants.
- Web : format, lint, 19 Vitest, build et 5 scénarios Playwright passants.
- Runtime : GPS dry run, API health/readiness et build Android passants; Android connecté non requis, aucun code Android n’ayant changé.
- Sécurité : routes maintenance authentifiées Admin/Operator, `OrganizationId` exclusivement issu de l’identité, média vérifié dans le tenant, déduplication par index tenant/source et contrôle de concurrence par version.

## Rapport Sprint 16 précédent

- Statut : `PASSED`
- Date UTC : `2026-07-17T17:24:09Z`
- Sprint validé : `SPRINT-16` (`DONE`)
- Sprint sélectionné suivant : `SPRINT-17` (`IN_PROGRESS`, aucun travail commencé)
- Durée de la gate checkpoint : 119,6 s.

## Contrôles

| Contrôle | Statut | Notes |
|---|---|---|
| Git / compose / recovery | PASSED | compose standard/pilote, bucket privé, scripts recovery |
| Backend format / build | PASSED | Release, 0 warning, 0 erreur, modèle EF synchronisé |
| Backend tests rapides | PASSED | 120 tests, aucun skip |
| Contrat MinIO | PASSED | 1 test réel : reprise, checksum, copy/publish, lecture, suppression |
| SQL Server/Testcontainers | PASSED | 3 tests, migration et backup/restore du manifeste média |
| GPS dry run | PASSED | payload de télémétrie produit |
| Web format / lint / tests / build | PASSED | 19 Vitest, build Vite |
| Playwright | PASSED | 5 parcours critiques et isolation tenant |
| API health/readiness | PASSED | démarrage isolé et endpoints santé |
| Android lint/unit/build | PASSED | APK application et instrumentation compilés |
| Android connecté | NOT REQUIRED | aucun code Android modifié par Sprint 16 |

## Vérification sécurité et multi-tenant

- Production refuse le filesystem et une configuration S3 incomplète ; le pilote utilise un compte MinIO dédié limité au bucket privé, distinct du root ;
- les objets utilisent `tenants/{organizationId}/...` et des identifiants opaques, sans nom de fichier ni PII dans la clé ;
- une URL HMAC vit au plus 15 minutes, est liée au tenant et exige encore une identité authentifiée du tenant propriétaire ;
- révocation et rétention suppriment l’objet puis conservent le manifeste SQL référencé ; les opérations sont auditées ;
- quarantaine et objets temporaires/publiés abandonnés sont inaccessibles et nettoyés ;
- tests d’intégration : tenant étranger `404`, anonymous `401`, révocation `401`, panne objet sans fallback disque.

## Démonstration Sprint 16

- MinIO est démarré, le bucket est créé privé et la policy dédiée est attachée ;
- un upload interrompu reprend et accepte un replay identique sans doublon ;
- le checksum validé est persisté avant la copy de publication ;
- le conducteur propriétaire lit la preuve via capacité courte, un autre tenant ne la découvre pas, puis l’Admin la révoque ;
- migration legacy rejouée deux fois : `migrated`, puis `already-migrated`, source filesystem conservée ;
- rétention supprime l’objet, révoque la lecture et conserve le manifeste récupérable par backup SQL.

## Limites honnêtes

- MinIO local prouve le contrat S3 et SSE-S3 avec une clé KMS de développement ; la Production doit fournir secrets, sauvegarde objet et gestion KMS externes ;
- la migration source est volontairement non destructive ; sa suppression relève d’une opération ultérieure après fenêtre de rollback ;
- les avertissements npm/Rollup non bloquants préexistants restent inchangés.
