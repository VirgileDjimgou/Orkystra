# Roadmap FleetOps

## Principe

L’état réel du dépôt compte dix-sept sprints terminés, `SPRINT-00` à `SPRINT-16`. La trajectoire approuvée ajoute exactement vingt sprints denses, `SPRINT-11` à `SPRINT-30`, pour transformer le MVP audité à 66/100 en produit de flotte exploitable, mesurable et commercialisable.

L’extension reste centrée sur le noyau **mission → conducteur offline → inspection/preuve → exception → action opérateur**. Elle consolide ensuite maintenance, conformité, télématique ouverte, communication destinataire, reporting et exploitation SaaS. Chaque sprint laisse le système exécutable et possède un résultat démontrable, des métriques, des tests, une vérification sécurité/tenant et un rollback.

Les sprints futurs sont une séquence de décisions, pas une promesse de tout construire sans validation. Les gates des Sprints 15, 20, 25 et 30 autorisent à poursuivre, simplifier ou arrêter une vague selon les preuves techniques et l’usage pilote.

## Vue d’ensemble

| Sprint | Résultat démontrable | Valeur principale | Statut | Taille |
|---|---|---|---|---:|
| 00 | Dépôt, architecture, CI et environnement local fiables | Reproductibilité | Terminé | L |
| 01 | Organisations, utilisateurs, rôles et shell Web | Isolation et accès | Terminé | L |
| 02 | Véhicules, conducteurs et appareils gérés de bout en bout | Référentiel flotte | Terminé | L |
| 03 | Positions simulées visibles sur une carte interactive | Visibilité temps réel | Terminé | XL |
| 04 | Missions planifiées, affectées et suivies | Dispatch | Terminé | XL |
| 05 | Application Android conducteur synchronisée hors ligne | Continuité terrain | Terminé | XL |
| 06 | Inspections, défauts et preuves de livraison numériques | Traçabilité terrain | Terminé | XL |
| 07 | Alertes, maintenance et conformité légère | Prévention | Terminé | L |
| 08 | API, webhooks, import/export et audit | Ouverture | Terminé | XL |
| 09 | Packaging, observabilité, MFA et pilote de démonstration | Baseline pilote | Terminé | XL |
| 10 | Configuration Production sûre et socle UX Web/Android | Réduction du risque critique | Terminé | L |
| 11 | SQL Server, migrations, recovery et parcours E2E prouvés | Confiance dans les données | Terminé | XL |
| 12 | Sessions, autorisations et données sensibles durcies | Sécurité exploitable | Terminé | XL |
| 13 | Centre d’opérations piloté par exception | Productivité opérateur | Terminé | XL |
| 14 | Parcours conducteur terrain avec photo réelle | Adoption et preuve terrain | Terminé | XL |
| 15 | Onboarding tenant, import et activation guidés | Temps de mise en service | Terminé | L |
| 16 | Stockage objet, médias sûrs et cycle de vie | Résilience des preuves | Terminé | L |
| 17 | Ordres de maintenance, immobilisation et coûts | Disponibilité véhicule | Terminé | XL |
| 18 | Conformité documentaire et campagnes d’inspection | Réduction du risque réglementaire | Terminé | L |
| 19 | Dispatch productif, modèles et actions en masse | Capacité opérationnelle | En cours | XL |
| 20 | Pilote alpha mesuré et décision de niche | Validation de la valeur | Planifié | L |
| 21 | Qualité du tracking, trajets et zones métier | Confiance télématique | Planifié | XL |
| 22 | Cadre de connecteurs et premier fournisseur télématique | Écosystème matériel ouvert | Planifié | XL |
| 23 | Statut destinataire, ETA prudent et notifications contrôlées | Réduction des appels | Planifié | L |
| 24 | Rapports opérationnels et indicateurs de valeur | Pilotage et ROI | Planifié | L |
| 25 | Hub d’intégrations fiable et exploitable | Interopérabilité durable | Planifié | XL |
| 26 | Administration des appareils, support et diagnostic distant | Réduction du support | Planifié | L |
| 27 | Rétention, archivage et performance à l’échelle cible | Coûts et vitesse maîtrisés | Planifié | XL |
| 28 | Design system, accessibilité et espaces de travail | Cohérence de tous les clients | Planifié | L |
| 29 | Résilience, observabilité et assurance sécurité de release | Exploitation Production | Planifié | XL |
| 30 | Bêta commerciale, packaging et décision de disponibilité générale | Passage au marché | Planifié | XL |

## Vagues et gates de décision

### Vague A — Baseline fonctionnelle et stabilisation (`00–10`)

Architecture, identité, flotte, tracking, dispatch, Android offline, preuves, alertes, intégrations et durcissement initial sont livrés. Ces sprints restent historiques ; leurs limites sont décrites dans l’audit et le rapport qualité.

### Vague B — Confiance technique et expérience quotidienne (`11–15`)

Prouver SQL/recovery/E2E, protéger les sessions, traiter les exceptions, fiabiliser le conducteur terrain et réduire l’onboarding à moins d’une journée. La gate Sprint 15 exige un environnement pilote récupérable et un parcours complet démontré sur Web et Android.

### Vague C — Contrôle opérationnel de la flotte (`16–20`)

Rendre les preuves résilientes, transformer alertes et échéances en actions, accélérer le dispatch et conduire un pilote alpha. La gate Sprint 20 choisit une niche principale et exige un usage régulier par au moins deux organisations.

### Vague D — Écosystème télématique et valeur mesurée (`21–25`)

Améliorer la qualité du tracking, connecter un fournisseur réel sans couplage, informer un destinataire avec contrôle, mesurer le ROI et industrialiser les intégrations. La gate Sprint 25 exige un connecteur exploitable et des métriques confirmant la réduction des appels ou du temps de clôture.

### Vague E — Industrialisation commerciale (`26–30`)

Réduire le coût de support, maîtriser le cycle de vie des données, unifier l’UX, prouver la résilience et tester une bêta commerciale. La gate Sprint 30 produit une décision `GO`, `SIMPLIFY`, `PIVOT` ou `STOP` documentée.

## Noyau produit non négociable

- organisation, utilisateurs, rôles et isolation tenant issue de l’identité ;
- registre des véhicules, conducteurs, appareils et affectations historisées ;
- tracking fiable, historique, qualité des données et alertes réellement actionnables ;
- mission, affectation, arrêts, statuts et timeline auditée ;
- Android offline-first centré sur la prochaine action ;
- inspection, défaut, photo/signature et preuve privée ;
- maintenance légère et conformité reliées aux opérations ;
- API/webhooks/connecteurs simples, versionnés et observables ;
- sécurité, restauration, rétention et observabilité prouvées ;
- reporting directement relié à la valeur client.

## Éléments explicitement hors roadmap

Restent exclus jusqu’à nouvelle décision fondée sur un pilote : facturation métier transport, WMS complet, paie, optimisation propriétaire de tournées, portail client complet, application iOS, gestion d’atelier avancée, comptabilité générale, calcul CO₂ réglementaire, IA/RAG, microservices, bus distribué et matériel GPS propriétaire.

MQTT n’entre dans le produit qu’avec un fournisseur ou appareil réellement validé. Les Sprints 21–22 privilégient un contrat d’adaptateur indépendant du transport ; HTTP reste le chemin par défaut.

## Règles de passage communes

- corriger toute régression et tout risque critique avant d’ajouter du périmètre ;
- exécuter un seul sprint à la fois et maintenir sa fiche comme source de vérité ;
- préserver les contrats opérationnels ou fournir migration, compatibilité et rollback ;
- exiger pagination, idempotence et contrôle de concurrence sur les flux volumineux ;
- exiger tests d’autorisation et d’isolation tenant pour chaque capacité sensible ;
- exiger tests unitaires, relationnels, Web/Android et E2E proportionnés au risque ;
- mesurer adoption, latence, erreurs et résultat métier, pas seulement la présence d’écrans ;
- distinguer explicitement `non vérifié`, `testé`, `piloté` et `prouvé en Production` ;
- ne jamais déclarer un sprint `DONE` avec critères obligatoires non satisfaits sans dette et décision enregistrées.

## Traçabilité audit → sprints

| Constat ou recommandation de l’audit | Sprints de traitement |
|---|---|
| AUD-006, AUD-007, recovery et preuves SQL absentes | 11, 29 |
| AUD-008, autorisations, données sensibles et uploads | 12, 16, 29 |
| AUD-009, vues denses, recherche et travail par exception | 13, 19, 28 |
| AUD-010, stockage média local | 16, 27 |
| AUD-011, télématique fournisseur non branchée | 21, 22 |
| AUD-013, caméra et instrumentation Android absentes | 14, 26 |
| Onboarding et mesure de l’activation | 15, 20, 30 |
| Ordres de maintenance et conformité actionnable | 17, 18 |
| Rapports opérationnels et preuve du ROI | 20, 24, 30 |
| Charge, rétention, observabilité et sécurité dynamique | 27, 29 |

Le détail factuel des constats est conservé dans `docs/04-audit/2026-07-17-COMPLETE-AUDIT.md`. Les fiches `sprints/SPRINT-11-*.md` à `sprints/SPRINT-30-*.md` sont les sources de vérité d’exécution.
