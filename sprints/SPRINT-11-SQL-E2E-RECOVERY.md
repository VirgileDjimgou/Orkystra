# SPRINT-11 — SQL, E2E et recovery prouvés

## Objectif

Démontrer que FleetOps fonctionne sur SQL Server avec ses migrations réelles, survit à une restauration et protège ses trois parcours critiques de bout en bout.

## Valeur et constats traités

Supprime le principal risque de perte de données et traite AUD-005, AUD-006, AUD-007 ainsi que les lacunes P0 login/tenant, mission complète et backup/restore. Aucune capacité pilote ne doit dépendre uniquement d’EF InMemory.

## Tâches principales

- créer une factory d’intégration SQL Server avec Testcontainers et migrations uniquement ;
- tester contraintes, index uniques tenant, row versions, transactions, outbox et suppressions ;
- automatiser backup, restauration dans une base neuve et checksum métier avant/après ;
- versionner trois parcours Playwright : authentification/MFA, dispatch complet et isolation tenant ;
- ajouter une instrumentation Android minimale pour offline, reprise WorkManager et non-duplication ;
- séparer gate rapide, gate relationnelle de PR et gate nightly/release avec artefacts ;
- documenter diagnostic, durée, prérequis Docker et procédure de reprise.

## Composants concernés

Tests backend, CI, SQL Server, scripts recovery, Web, Android et documentation d’exploitation.

## Dépendances

Sprint 10 terminé ; moteur Docker Linux stable pour la preuve SQL et émulateur/appareil pour l’instrumentation Android.

## Tests et preuves requis

Tests relationnels et de migrations, restauration avec comparaison de comptes/hachages, Playwright multi-rôle, instrumentation offline, redémarrage worker/outbox et vérification d’absence de fuite inter-tenant.

Preuves obtenues au vendredi 17 juillet 2026 :
- les quatre scénarios Playwright critiques passent localement ;
- `connectedDebugAndroidTest` passe sur téléphone Android réel ;
- les preuves SQL relationnelles et recovery restent implémentées mais non rejouées localement faute de moteur Docker Linux.

## Critères d’acceptation

- [ ] le schéma d’essai est créé exclusivement par migrations depuis une base vide ;
- [ ] les contraintes SQL et la concurrence optimiste sont exercées ;
- [x] login, création/affectation de mission et consultation d’une preuve passent en E2E ;
- [x] un tenant ne peut ni découvrir ni modifier les données d’un autre en E2E ;
- [ ] une sauvegarde est restaurée dans une base neuve avec checksum métier identique ;
- [x] les gates rapides et lourdes publient des diagnostics exploitables.

## Livrable démontrable

Un pipeline produit un rapport avant/après restauration et les traces des parcours Web/Android, puis redémarre le worker sans perte ni double livraison.

## Observabilité, sécurité et rollback

Les artefacts excluent secrets et données personnelles. Les conteneurs sont éphémères et isolés. Si la gate lourde devient instable, elle reste obligatoire avant release mais n’affaiblit jamais la gate rapide ; rollback limité au harnais de test.

## Estimation

XL.
