# Journal des décisions

| ID | Date | Décision | Statut |
|---|---|---|---|
| D-001 | 2026-07-15 | Monolithe modulaire au lieu de microservices | Acceptée |
| D-002 | 2026-07-15 | Une seule application Web pour Admin et Opérateur | Acceptée |
| D-003 | 2026-07-15 | Application Android native dédiée au Conducteur | Acceptée |
| D-004 | 2026-07-15 | SQL Server comme source transactionnelle principale | Acceptée |
| D-005 | 2026-07-15 | Matériel GPS propriétaire hors MVP ; simulateur et intégrations tierces | Acceptée |
| D-006 | 2026-07-15 | Télémétrie HTTP d'abord, MQTT seulement après besoin démontré | Acceptée |
| D-017 | 2026-07-22 | Utiliser un Sandbox Telematics Provider interne versionné pour Sprint 22 ; il est explicitement virtuel, ne constitue pas une intégration commerciale, et son adaptateur HTTP doit rester remplaçable par un fournisseur réel. Les futurs sprints peuvent prendre des décisions d’implémentation raisonnables et employer des simulateurs internes réalistes lorsqu’une dépendance externe manque, sans inventer de preuve ou d’intégration Production. | Acceptée par le Product Owner |
| D-018 | 2026-09-26 | Rebaser les plans non implémentés SPRINT-24 à SPRINT-30 en phase `Demo Readiness / Autonomous Fleet Simulation` avec exactement neuf sprints actifs SPRINT-24 à SPRINT-32. Préserver les plans précédents sous archive `SUPERSEDED — NOT IMPLEMENTED`, conserver le monolithe, Leaflet, SignalR et les frontières tenant, et interdire toute assimilation de preuves de démo synthétique à une preuve pilote ou commerciale. | Acceptée par instruction utilisateur |
| D-019 | 2026-09-27 | Restaurer l'exécution interactive et visible des sprints : `RUNNING` signifie qu'un tour Codex ou une commande identifiable est réellement actif ; toute interruption devient `PARTIAL/IDLE` avec journal et verrou libéré. `Start Next Sprint` reprend le plus petit sprint partiel. Un batch enregistré n'est jamais présenté comme un worker autonome. | Acceptée par instruction utilisateur |
| D-007 | 2026-07-15 | Nom FleetOps provisoire, vérification commerciale requise | Ouverte |
| D-008 | 2026-07-15 | Sous Windows, SQL Server local utilise un volume Docker nommé plutôt qu'un bind mount pour éviter les crashs de permission au Sprint 00 | Acceptée |
| D-009 | 2026-07-15 | Le renommage Zynro reste limité aux éléments visibles (`Zynro Fleet`, `Zynro Drive`) jusqu'à un sprint de renommage technique dédié | Acceptée |
| D-010 | 2026-07-15 | Sprint 01 utilise ASP.NET Core Identity avec JWT bearer signé localement comme baseline compatible avec une future fédération OIDC | Acceptée |
| D-011 | 2026-07-17 | La trajectoire post-MVP est initialement limitée aux Sprints 10 à 14 : sécurité et preuves d’abord, puis UX opérateur/conducteur et validation commerciale | Remplacée par D-012 |
| D-012 | 2026-07-17 | Après Sprint 10, ajouter exactement vingt sprints `11–30`, organisés par gates de valeur ; conserver le noyau mission–preuve–exception et les exclusions architecturales, commerciales et fonctionnelles de l’audit | Acceptée |
| D-013 | 2026-07-17 | Sprint 11 sépare explicitement gate rapide et preuves lourdes : tests SQL Docker, Playwright E2E, et instrumentation Android compilée en standard ; exécution Android connectée seulement avec émulateur/appareil déclaré | Acceptée |
| D-014 | 2026-07-17 | En Production, les preuves média utilisent un bucket S3 compatible MinIO privé avec identifiants dédiés ; le filesystem reste limité au développement, à la migration et au rollback, et toute lecture reste autorisée par l’API dans le tenant authentifié | Acceptée |
| D-015 | 2026-07-18 | Le dry-run Sprint 20 utilise un orchestrateur API modulaire sur base isolée avec trois tenants fictifs et tous les rôles ; ses rapports et captures sont explicitement des preuves de simulation et ne satisfont jamais les critères commerciaux externes | Acceptée |
| D-016 | 2026-07-22 | Le Product Owner accepte les retours qualitatifs convergents de plusieurs entreprises comme signal suffisant pour un `GO` vers Sprint 21. Les objectifs quantitatifs initiaux de Sprint 20 restent `NON VÉRIFIÉS` et sont reportés comme preuves commerciales à obtenir avant la bêta ; cette décision n'autorise ni faux métriques ni copie des interfaces de référence tierces | Acceptée |
