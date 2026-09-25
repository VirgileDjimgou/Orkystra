# Audit produit et roadmap vers la commercialisation — FleetOps

Date : 2026-08-27.
Base factuelle : dépôt FleetOps (23 sprints, Sprint 23 partiel), `ROADMAP.md`, fiches `sprints/SPRINT-00..30`, `.agent/DECISIONS.md`, `.agent/HANDOFF.md`, `.agent/QUALITY_REPORT.md`, audit du 17 juillet 2026 (`docs/04-audit/2026-07-17-COMPLETE-AUDIT.md`), docs produit/architecture/commercial.
Ce document est une analyse ; il ne modifie ni `ROADMAP.md`, ni les fiches de sprint, ni `PROJECT_STATE.json`.

---

## 1. Résumé exécutif

FleetOps est un SaaS B2B multi-tenant de gestion opérationnelle de flotte pour petites entreprises (5 à 30 véhicules) : Web Admin/Opérateur, Android Conducteur offline-first, API ASP.NET Core, worker, SQL Server, stockage objet S3-compatible, SignalR et outbox. Son cœur différenciant est le flux **mission → conducteur offline → inspection/preuve → exception → action opérateur**, et non le tracking GPS en soi.

État réel : vingt-trois sprints livrés avec quality gates vertes (143 tests backend, 3 preuves SQL Server/Testcontainers, 1 contrat MinIO, 20 tests Vitest, 5 E2E Playwright, simulation multi-tenant de 33 étapes, build Android + 5 tests instrumentés sur appareil physique). Sprint 23 est exécuté à moitié : lien récipiendaire sécurisé en place ; préférences tenant, outbox de notifications, page publique et métriques restent à faire. Le niveau réel est **7,5/10 — MVP renforcé et sécurisé**, pas encore un produit commercial : les critères externes (adoption, volonté de payer, niche) sont **NON VÉRIFIÉS** (D-016), la facturation SaaS n'existe pas, le DPA/CGU n'existent pas, aucun pen test externe n'a été mené et la marque commerciale reste provisoire (D-007).

Verdict : **faisable techniquement, commercialement incertain jusqu'à preuve pilote payante**. Recommandation : poursuivre dans la forme actuelle, sans élargir le périmètre, avec des gates commerciales strictes, en exécutant les 20 sprints restants proposés en section 17 (8 correspondent aux fiches SPRINT-23b..30 existantes, 12 sont des ajouts de commercialisation à faire approuver).

---

## 2. Reformulation du brainstorming

### Concept en une phrase

> Pour un responsable d'exploitation d'une petite flotte (5 à 30 véhicules) qui coordonne encore missions, incidents et preuves entre Excel, téléphone et messagerie, FleetOps permet de piloter chaque mission de bout en bout dans un espace partagé et traçable, grâce à un workflow Web + conducteur offline-first centré sur les exceptions.

### Présentation simple

Une petite entreprise configure ses véhicules et conducteurs, voit ses véhicules sur une carte (GPS réel ou simulé), affecte une tournée, laisse le conducteur travailler même sans réseau, récupère inspection, photo et signature comme preuve de livraison, puis traite uniquement les incidents dans une console unique. Le produit vend la simplicité et la clôture des dossiers, pas la profondeur analytique des plateformes télématiques.

### Structure du concept

| Élément | Contenu |
|---|---|
| Problème principal | Visibilité dispersée sur véhicules, missions, incidents, conformité et preuves |
| Problèmes secondaires | Appels de statut, ressaisie, preuves incomplètes, immobilisations imprévues, échéances documentaires oubliées |
| Cible principale | Entreprises de livraison/service local, 5 à 30 véhicules, sans équipe IT |
| Utilisateurs secondaires | Opérateur/dispatcher, conducteur, support, intégrateur |
| Client payeur | Dirigeant ou responsable d'exploitation |
| Bénéficiaires | Client final de la livraison (destinataire informé, Sprint 23), équipe interne (moins d'appels) |
| Proposition de valeur | Un workflow mission + preuve + exception partagé et traçable, offline-first, simple à activer en moins d'une journée |
| Résultat attendu | -30 % d'appels de statut, ≥ 90 % de missions clôturées avec preuve complète le jour même |
| Produit central | Mission, dispatch, conducteur offline, preuve, exception, opérateur |
| Modules complémentaires | Registre flotte, tracking, maintenance légère, conformité, intégrations/API/webhooks, rapports, statut destinataire |
| Facteur de différenciation | Flux métier complet (pas un tracker de plus), multi-tenant, offline réel, preuves privées |
| Modèle économique envisagé | Abonnement SaaS par flotte (79/149/299 €/mois à tester) + onboarding facturé 300-900 € |

### Contradictions, ambiguïtés et éléments redondants relevés

- **Nom commercial** : « Orkystra FleetOps » provisoire (D-007 ouverte) ; un renommage partiel « Zynro Fleet/Zynro Drive » subsiste dans les surfaces visibles (D-009). À trancher avant toute publication.
- **Niche** : les trois positionnements (livraison locale, services terrain, couche au-dessus d'un GPS existant) restent ouverts ; D-016 a accepté un `GO` qualitatif alors que les critères quantitatifs restent `NON VÉRIFIÉS`.
- **État documentaire divergent** : `ROADMAP.md` déclare Sprint 23 « Planifié » alors que `PROJECT_STATE.json`/`CURRENT_SPRINT.md` le déclarent `IN_PROGRESS` avec du travail livré ; `HANDOFF.md` contient des paragraphes historiques successifs (Sprint 22 « bloqué » puis « clôturé ») qu'il faut lire comme un journal, pas comme l'état actuel.
- **Facturation** : `COMMERCIALIZATION_PATH.md` liste « facturation SaaS » avant vente générale, mais aucun sprint ne la construit ; Sprint 30 assume une souscription manuelle. Décision implicite à enregistrer.
- **Redondance** : MQTT reste une infrastructure Docker sans consommateur (assumé par D-006) ; le dashboard dupliquait les notifications par canal (corrigé par Sprint 13) ; les alias d'API historiques restent une dette de compatibilité.
- **Périmètre futur** : `FUTURE_SCOPE.md` liste des fonctions (portail client, ETA public, IA/RAG) dont deux sont désormais partiellement livrées (Sprint 23, ETA prudent sans portail) ; document à réconcilier.

---

## 3. Problème et utilisateurs

### Le problème existe-t-il ?

| Question | Réponse | Niveau de certitude |
|---|---|---|
| Le problème est-il réel ? | Oui — retours qualitatifs convergents de plusieurs entreprises (D-016) | Confirmé |
| Fréquence | Quotidienne pendant les tournées | Très probable |
| Intensité/urgence | Temps perdu, litiges clients, risque réglementaire ; pas de danger vital | Probable |
| Solutions actuelles | Excel, papier, WhatsApp/téléphone ; trackers GPS ; plateformes lourdes (Fleetio, Quartix, Samsara) | Confirmé |
| Pourquoi les solutions existantes ne suffisent pas | Pas de lien mission-preuve-exception ; plateformes matérielles coûteuses/complexes pour très petites flottes | Probable |
| Volonté de changer | Réelle pour les pilotes qualitatifs ; non mesurée à l'échelle | Possible |
| Volonté de payer | **NON VÉRIFIÉE** — c'est l'hypothèse critique restante | Non déterminé |

Réponses courtes : 1) oui ; 2) oui, à condition de rester sur la niche ; 3) quotidien ; 4) responsable d'exploitation et dispatcher ; 5) le dirigeant ; 6) temps, ressaisie et litiges ; 7) oui (Excel+WhatsApp) mais sans traçabilité ni preuve ; 8) oui pour les flottes au-delà de ~5 véhicules, non en dessous.

### Segments

| Segment | Problème principal | Fréquence | Urgence | Volonté de payer | Facilité d'accès | Priorité |
|---|---|---|---|---|---|---|
| Livraison locale 5-20 véh. | Preuves et appels de statut | Quotidienne | Haute | À tester | Haute (réseau local) | 1 |
| Services terrain 5-20 véh. | Inspection départ + preuve d'intervention | Quotidienne | Haute | À tester | Moyenne (partenaires métiers) | 2 |
| Flotte déjà équipée GPS | Dispatch/preuve au-dessus du tracker | Quotidienne | Moyenne | Moyenne (budget déjà engagé) | Moyenne (revendeurs) | 3 |
| TPE < 5 véhicules | Même problème | Hebdomadaire | Faible | Faible | Facile | Exclu MVP |
| Grandes flottes 50+ | Complexité, conformité | Quotidienne | Haute | Forte | Difficile (vente longue) | Exclu MVP |

### Personas

**Persona principal — Alex, responsable d'exploitation (payeur/décideur).**
Contexte : société locale de livraison, 8-15 véhicules, 2 dispatchers, pas d'IT. Objectif : savoir où sont les véhicules, qui a livré quoi, avec quelle preuve, sans appeler les chauffeurs. Difficultés : Excel obsolète, litiges sans preuve, échéances documentaires manquées. Outils actuels : WhatsApp, tableur, un tracker GPS basique. Niveau technique : faible. Raison d'adopter : clôture des dossiers le jour même, moins d'appels. Raison de refuser : « j'ai déjà un GPS », peur de l'adoption par les chauffeurs, prix. Critères de confiance : démo sur sa propre flotte, simplicité, conformité RGPD, support réactif. Capacité de payer : 79-299 €/mois plausible, non prouvé.

**Persona secondaire 1 — Sam, dispatcher/opérateur.**
Objectif : préparer, affecter et suivre les missions ; ne traiter que les exceptions. Difficultés : bruit des canaux, ressaisie. Outils : téléphone, tableur. Raison de refuser : courbe d'apprentissage, écrans denses. Critère : travail par exception ≤ 3 actions (déjà démontré Sprint 13/19).

**Persona secondaire 2 — Rico, conducteur.**
Objectif : sa prochaine action, même hors réseau. Difficultés : réseau instable, paperasse. Outils : téléphone perso. Raison de refuser : application intrusive, consommation de batterie, complexité. Critère : une action principale par écran, sync visible (Sprint 14).

Premier segment à lancer : **livraison locale 5-20 véhicules**, acquisition par intégrateurs IT locaux et réseaux professionnels.

---

## 4. Proposition de valeur

### Promesse actuelle

« Piloter une mission complète dans un espace partagé et traçable, contrairement à un simple tracker GPS, grâce à un workflow Web + conducteur offline-first centré sur les exceptions. »

Forces : claire, spécifique, démontrable en simulation et sur pilote, différenciante face aux trackers seuls. Faiblesses : non mesurée en conditions réelles, niche encore large, marque provisoire, « moins d'une journée d'onboarding » annoncé mais non prouvé sur cohorte réelle.

### Reformulation recommandée (à tester sur pilotes)

> « FleetOps remplace les appels et les tableurs : chaque mission est assignée, exécutée et prouvée — même hors réseau — et seuls les vrais problèmes arrivent sur votre écran. »

Promesse mesurable : **-30 % d'appels de statut et ≥ 90 % de missions clôturées avec preuve le jour même**.

### Positionnements possibles

| Positionnement | Cible | Problème | Promesse | Produit vendu | Difficulté commerciale |
|---|---|---|---|---|---|
| Livraison locale fiable | Livraison 5-20 véh. | Appels + preuves | Dossier livré et prouvé le jour même | Mission + POD + exceptions | Moyenne |
| Services terrain | Artisans/maintenance 5-20 véh. | Inspection départ + preuve d'intervention | Traçabilité d'intervention | Inspection + preuve + maintenance | Moyenne-élevée |
| Couche opérationnelle sur GPS existant | Flottes déjà équipées | Dispatch/preuve manquants | Votre GPS + notre workflow | API + dispatch + driver | Élevée (dépend revendeurs) |

**Positionnement recommandé : livraison locale fiable** ; le troisième peut devenir une offre partenaire après la bêta.

---

## 5. Produit et écosystème fonctionnel

| Produit ou module | Utilisateur | Problème résolu | Valeur | Dépendances | Complexité | Priorité |
|---|---|---|---|---|---|---|
| Identity/tenancy/rôles | Admin | Accès et isolation | Socle | — | M | Livré |
| Registre flotte | Admin | Référentiel véhicules/conducteurs/appareils | Socle | Identity | M | Livré |
| Tracking + carte | Opérateur | Où sont les véhicules | Visibilité | Télémétrie | L | Livré |
| Dispatch/missions | Opérateur | Préparer/affecter/suivre | Cœur | Registre, tracking | L | Livré |
| Android offline driver | Conducteur | Travailler sans réseau | Cœur | Dispatch | XL | Livré |
| Inspections/POD/preuves | Conducteur/Opérateur | Preuve de livraison | Cœur | Android, média objet | XL | Livré |
| Alertes et exceptions | Opérateur | Ne traiter que l'essentiel | Cœur | Outbox | L | Livré |
| Maintenance légère | Admin | Immobilisation et coûts | Complémentaire | Registre | L | Livré |
| Conformité documentaire | Admin | Échéances réglementaires | Complémentaire | Registre | L | Livré |
| API v1/webhooks/import | Intégrateur | Interopérabilité | Complémentaire | Outbox | L | Livré |
| Stockage objet médias | Tous | Preuves résilientes/privées | Complémentaire | MinIO/S3 | M | Livré |
| Sandbox télématique | Admin | Ingestion sans matériel | Démo/pilote | API | M | Livré (virtuel) |
| Statut destinataire | Destinataire | Réduire les appels | Complémentaire | Tracking | M | Partiel (Sprint 23) |
| Rapports opérationnels | Payeur | Preuve de ROI | Commercial | Tous | M | Planifié (24) |
| Hub d'intégrations | Admin/partenaire | Échanges exploitables | Partenaire | API | XL | Planifié (25) |
| Support/diagnostic | Admin/support | Réduire le coût de support | Exploitation | Sessions | M | Planifié (26) |
| Facturation SaaS | Payeur | Revenu récurrent | Commercial | Identité, offres | M | **Absent — à ajouter** |
| DPA/CGU/registre | Payeur | Conformité | Légal | — | S | **Absent — à ajouter** |
| Site public/onboarding | Prospect | Découverte/vente | Commercial | — | M | **Absent — à ajouter** |

### Cartographie de l'écosystème

```text
Destinataire (lien public limité)
    │
    ▼
Application Web Admin/Operator ── REST + SignalR ──┐
Android Driver ── REST/WorkManager/Room ────────────┤
Appareil/simulateur GPS ── REST/API key ────────────┤─ ASP.NET Core API ── SQL Server
                                                    │        │
FleetOps Worker (alertes, outbox, purges) ──────────┘        ├─ MinIO/S3 (médias)
                                                             ├─ Outbox (notifications/webhooks)
                                                             └─ Audit
```

### Indications

- **Produit central** : mission → offline → preuve → exception (livré, prouvé en simulation et partiellement en pilote).
- **Indispensables** : tout ce qui est « Livré » ci-dessus.
- **Prématurées/à supprimer** : portail client complet, IA/RAG, optimisation propriétaire, WMS, paie, iOS, matériel propriétaire, MQTT applicatif (déjà exclus par la roadmap).
- **À externaliser** : facturation (Stripe/équivalent, post-bêta), e-mail/SMS transactionnels, scan antivirus d'uploads, pen test.
- **À construire (manquants commerciaux)** : facturation SaaS, DPA/CGU, site public, analytics de funnel non intrusif.
- **Pouvant devenir produit distinct** : le hub d'intégrations (Sprint 25) comme kit partenaire en marque blanche.

---

## 6. Parcours utilisateurs

### Parcours principal (doit absolument fonctionner)

Admin crée la flotte → appareil/simulateur émet une position → opérateur crée et affecte une mission → conducteur la reçoit et l'exécute (même hors ligne) → positions et statuts remontent → conducteur collecte photo/signature → opérateur vérifie la preuve et clôture le dossier → seules les exceptions nécessitent une action.

| Étape | Action utilisateur | Réponse du système | Données | Friction possible | Amélioration |
|---|---|---|---|---|---|
| Onboarding | Admin crée l'organisation | Tenant + rôles prêts | Identité | Config manuelle | Import guidé (Sprint 15, livré) |
| Registre | Import/ajout véhicules, conducteurs, appareils | Entités validées, appareils appairés | Registre | Erreurs CSV | Prévisualisation (Sprint 19, livré) |
| Tracking | Appareil envoie une position | Position idempotente, carte mise à jour | Télémétrie | Données invalides | Qualité/fraîcheur (Sprint 21, livré) |
| Dispatch | Opérateur affecte une mission | Contrôles de concurrence, immobilisation, conformité | Mission | Conflits | Templates + actions bulk (Sprint 19, livré) |
| Exécution | Conducteur progresse hors ligne | Sync idempotente via Room/WorkManager | Statuts | Perte réseau | File offline visible (Sprint 14, livré) |
| Preuve | Photo/signature | Upload repris + média privé S3 | Preuves | Upload interrompu | Reprise + checksum (Sprint 16, livré) |
| Exception | Alerte levée | Exception groupée dans le centre d'opérations | Alertes | Bruit | Travail par exception (Sprint 13, livré) |
| Clôture | Opérateur vérifie et clôture | Timeline auditée, dossier complet | Audit | Dossier incomplet | Preuve complète (Sprint 06/16, livré) |

### Moments clés

- **Activation** : le conducteur termine une mission avec photo réelle après une coupure réseau (démontré en simulation ; à prouver sur pilote).
- **Conversion** : le responsable constate la baisse d'appels et la preuve complète sur deux semaines (métriques Sprint 24/20).
- **Rétention** : exceptions traitées à temps + rapports périodiques (Sprints 24/30).
- **Résiliation propre** : export + purge tenant (existant) ; à contractualiser (DPA/CGU).

---

## 7. Audit des fonctionnalités

| Fonctionnalité | Valeur utilisateur | Valeur commerciale | Complexité | Risque | Phase recommandée | Décision |
|---|---|---|---|---|---|---|
| Mission/dispatch/statuts | 10 | 10 | L | Faible | Livrée | Conserver |
| Conducteur offline + preuve | 10 | 10 | XL | Moyen | Livrée | Conserver |
| Centre d'exceptions | 9 | 9 | L | Faible | Livrée | Conserver |
| Registre + import | 8 | 8 | M | Faible | Livrée | Conserver |
| Tracking + qualité + trajets/zones | 8 | 7 | XL | Faible | Livrée | Conserver |
| Maintenance légère + immobilisation | 7 | 7 | L | Faible | Livrée | Conserver |
| Conformité documentaire | 6 | 6 | L | Moyen | Livrée | Conserver |
| Statut destinataire | 6 | 7 | M | Élevé (vie privée) | Fin Sprint 23 | Terminer, garder minimal |
| Rapports opérationnels | 7 | 9 | M | Moyen | Sprint 24 | Construire |
| Hub d'intégrations | 5 | 8 | XL | Moyen | Sprint 25 | Construire sous gate |
| Support/diagnostic | 6 | 8 | M | Moyen | Sprint 26 | Construire |
| Facturation SaaS | 3 | 10 | M | Élevé (fiscal) | Post-bêta | Construire après preuve de paiement |
| DPA/CGU/registre RGPD | 2 | 10 | S | Faible | Avant données réelles | Acheter (conseil juridique) |
| Site public/docs client | 4 | 8 | M | Faible | Pré-GA | Construire |
| Portail client complet | 5 | 4 | XL | Élevé | Exclu | Supprimer/reporter |
| IA/RAG, optimisation, WMS, paie, iOS | 2-4 | 2-4 | XL | Élevé | Exclu | Supprimer/reporter |

---

## 8. Étude de faisabilité

| Domaine | Note /10 | Principaux arguments | Blocage éventuel | Action nécessaire |
|---|---|---|---:|---|---|
| Produit | 8 | Besoin clair, parcours cohérent, cœur démontrable | Niche non tranchée | Décision de niche à la bêta |
| Technique | 8 | Stack éprouvée, gates vertes, migration cohérente | Aucun | Maintenir la discipline |
| Commerciale | 5 | Différenciation réelle mais volonté de payer non prouvée | **NON VÉRIFIÉ** | Pilote payant + lettres d'intention |
| Financière | 6 | Coûts d'infra faibles (monolithe, petit volume) ; support solo dev à chiffrer | Coût d'acquisition inconnu | Mesurer coût support/tenant à la bêta |
| Opérationnelle | 6 | Onboarding/runbooks documentés, simulation complète | Support solo, dépendance fournisseur GPS | Runbooks Sprint 26, connecteur réel Sprint 13 restant |
| Juridique | 5 | RGPD partiellement couvert (export/purge/rétention) ; DPA/CGU absents ; marque non vérifiée | DPA avant données réelles | Conseil juridique + INPI |

---

## 9. Hypothèses à valider

| Hypothèse | Importance | Incertitude | Méthode de validation | Critère de réussite |
|---|---|---:|---|---|
| Le workflow réduit ≥ 30 % les appels de statut | 10 | Élevée | Pilote 3 organisations, 2 semaines, 4 j/sem (PILOT_RUNBOOK) | Baisse mesurée sur baseline déclarée |
| ≥ 90 % de missions clôturées avec preuve le jour même | 9 | Élevée | Métriques pilote (Sprint 20/24) | Taux atteint sur 2 semaines |
| Volonté de payer ≥ 100 €/mois | 10 | Élevée | 2 pilotes payants ou lettres d'intention | 2 paiements au prix testé |
| 80 % de conducteurs actifs semaine 1 | 8 | Moyenne | Cohortes bêta (Sprint 30) | Taux atteint |
| Onboarding < 1 jour | 7 | Moyenne | Chronométrage bêta | Médiane < 1 jour |
| Le coût de support reste absorbable en solo | 8 | Élevée | Suivi tickets/tenant à la bêta | < x €/véhicule/mois (à fixer) |
| Un connecteur télématique réel est remplaçable sans couplage | 7 | Moyenne | Sprint 25 gate + adaptateur réel | Changement de fournisseur sans régression |
| Les destinataires utilisent le lien sans frictions de vie privée | 5 | Moyenne | Métriques minimisées Sprint 23 | Vues utiles sans plaintes |

Les expériences les moins coûteuses : (1) réactiver le pilote réel selon `PILOT_RUNBOOK.md` sans construire de billing ni de connecteur ; (2) vendre manuellement une offre Essentiel à 2 prospects ; (3) mesurer les appels avant/après sur 2 semaines.

---

## 10. POC, MVP commercial et produit mature

### POC (historique, validé)

Objectif : prouver qu'une mission simulée fonctionne de bout en bout. Réalisé par les Sprints 00-09 : simulateur GPS, dispatch, Android offline, POD, alertes. Critères atteints : scénario automatisé complet, 33 étapes de simulation. Éléments simulés assumés : GPS (simulateur), télématique (sandbox virtuelle D-017), organisations fictives. **Le POC est terminé ; inutile d'en refaire un.**

### MVP commercial (cible de la phase 1 de la roadmap restante)

Inclut : tout le cœur livré + fin du Sprint 23 (préférences/outbox/page publique/métriques), rapports (24), hub d'intégrations sous gate (25), support (26), cycle de vie/performance (27), design system (28), assurance Production (29), conformité légale (DPA/CGU) et bêta commerciale (30).
Exclut : facturation automatisée (manuelle pendant la bêta), site public, marketplace, IA, nouvelles verticales.

### Produit mature commercialisable (post-bêta)

Ajoute : facturation SaaS automatisée, site public et onboarding self-service, support professionalisé (tickets, statut de service, SLA), pen test externe + remédiation, charge prouvée à 100 véhicules/tenant, localisation, analytics de funnel non intrusif, partenariat télématique réel, stabilisation des retours et lancement GA avec décision documentée.

---

## 11. Architecture recommandée

### POC (validée) → MVP (actuelle, à conserver)

Monolithe modulaire : `FleetOps.Core` (domaine) / `Infrastructure` (EF, Identity, stockage) / `Api` (vertical slices) / `Worker` (alertes, outbox). SQL Server transactionnel unique, MinIO/S3 pour médias, SignalR pour positions, outbox pour notifications/webhooks, Vue 3 monobundle Admin/Operator, Android natif Compose/Room/WorkManager, Docker Compose, CI GitHub Actions. Décisions ADR-0001/0002/0003 et D-001/D-002/D-003 restent pertinentes.

### Produit mature (évolution, sans rupture)

- mêmes quatre projets, worker éventuellement scindé en boucles bornées dans le même déploiement (Sprint 29) ;
- observabilité OTLP externalisée (dashboards/alertes/runbooks) ;
- backups automatisés + restauration périodique prouvée ;
- facturation via fournisseur (Stripe/équivalent) en service applicatif isolé ;
- e-mail/SMS transactionnels via fournisseur, avec outbox local ;
- HTTPS/TLS, secrets managés, rotation automatisée ;
- montée en charge verticale d'abord ; aucun microservice, bus distribué ou Kubernetes tant que la charge cible (100 véhicules/tenant) ne l'exige pas.

### À développer / intégrer / acheter / ne pas construire

- **Développer** : fin Sprint 23, rapports, hub, support, lifecycle, design system, assurance Production.
- **Intégrer** : connecteur télématique réel, fournisseur de paiement, e-mail/SMS, scan antivirus.
- **Acheter** : conseil juridique (DPA/CGU), pen test externe.
- **Ne pas construire en v1** : billing custom complet, marketplace, moteur d'IA, iOS, WMS.

---

## 12. Données, IA et intégrations

### Entités principales

| Entité | Rôle | Relations | Sensibilité | Rétention/audit |
|---|---|---|---|---|
| Organization | Tenant racine | 1-N tout | Identifiants | Jusqu'à résiliation + fenêtre contractuelle |
| User/Driver | Identité, rôles | N-1 Organization | Élevée | Audit des accès |
| Vehicle/Device | Registre | N-1 Organization | Moyenne | Historique des affectations |
| Telemetry/Trip/Geofence | Positions/trajets | N-1 Device | Élevée (géoloc) | Rétention bornée configurable (Sprint 27) |
| Mission/Stop/Assignment | Cœur métier | N-1 Organization, N-1 Vehicle/Driver | Moyenne | Timeline auditée, row version |
| Inspection/Defect/Proof | Preuves | N-1 Mission | Élevée (photos/signatures) | Média S3 + manifeste SQL |
| Alert/Exception | Incidents | N-1 Organization | Moyenne | Cycle assigné/résolu |
| WorkOrder/Document/Policy | Maintenance/conformité | N-1 Organization | Moyenne | Versions historisées |
| Outbox/WebhookConnection | Livraison fiable | N-1 Organization | Moyenne | DLQ, replay auditée |
| RecipientStatusLink | Lien public | N-1 Mission | Élevée (token) | SHA-256 seul, expiration/révocation |

### IA

**Aucune fonction IA aujourd'hui — c'est adapté.** Les besoins actuels sont couverts par des règles déterministes (alertes, conformité, recommandations d'exception). L'IA resterait marketing au stade actuel ; candidats futurs (à n'évaluer qu'après GA) : détection d'anomalies télémétriques, OCR de documents, RAG documentaire — tous exclus par la roadmap jusqu'à nouvelle décision.

### Intégrations

API v1 versionnée, webhooks HMAC + retry/DLQ, CSV import/export, sandbox télématique virtuelle (`sandbox-telematics.v1`), MinIO S3. Dette : alias d'API historiques à déprécier (Sprint 25). MQTT reste hors code (D-006).

---

## 13. UX, sécurité et qualité

### Écrans

- **POC (existants)** : login, dashboard, carte, registre, dispatch, alertes, intégrations, Android missions/preuve.
- **MVP (existants + Sprint 23-28)** : page publique statut destinataire, rapports, hub d'intégrations, support/diagnostic, espaces par rôle, composants design system.
- **Mature (post-bêta)** : onboarding self-service, facturation/compte, statut de service public, docs client.
- Fusionnables : vues maintenance/conformité dans un espace « Véhicules » ; supprimables : formulaires de démonstration hors mode démo (déjà isolés).

### Sécurité (état et restes)

Déjà en place : MFA admin, lockout/rate limit, tenant exclusivement issu de l'identité, API keys scopées, HMAC webhooks, médias privés avec URL HMAC courte, token récipiendaire SHA-256, réponses publiques minimisées avec `no-store`, quarantaine d'uploads, tests inter-tenant systématiques.
Restent : DPA/CGU/registre, pen test externe + remédiation, DAST, scan antivirus des uploads, rotation automatisée des secrets, exercice d'incident chronométré (Sprints 29/14 restants).

### Tests (état et restes)

Existants : 143 tests backend, 3 SQL Server/Testcontainers, 1 contrat MinIO, 20 Vitest, 5 Playwright, 33 étapes de simulation, build Android + 5 tests instrumentés.
Restants : DAST/SAST, charge/soak à l'échelle cible, tests visuels et accessibilité automatisés, restore drill périodique, tests de contrat partenaire.

---

## 14. Modèle économique et commercialisation

| Offre | Cible | Fonctions | Limites | Prix indicatif | Objectif |
|---|---|---|---|---|---|
| Essentiel | 5-10 véhicules | Cœur mission/preuve/exceptions | 10 véh., 1 admin, support standard | 79 €/mois | Porte d'entrée |
| Opérations | 10-25 véhicules | + rapports, hub, conformité | 25 véh., utilisateurs illimités | 149 €/mois | Cœur de cible |
| Partenaire | 25-50 véhicules | + API/webhooks, support prioritaire | 50 véh. | 299 €/mois | Intégrateurs |

Onboarding facturé séparément 300-900 €. Prix à ne pas figer avant 3 pilotes payants. Facturation **manuelle pendant la bêta** (Sprint 30), automatisée ensuite (sprint restant R-10).

- Premier segment : livraison locale 5-20 véhicules.
- Première offre : Essentiel + onboarding.
- Canal principal : intégrateurs IT locaux + réseaux professionnels + démo sur flotte réelle.
- Preuve de valeur à présenter : deux semaines de métriques (appels, preuves complètes, temps de clôture).
- Objections probables : « j'ai déjà un tracker GPS » (réponse : le workflow mission/preuve, pas le tracking), « mes chauffeurs n'adopteront pas » (démo Android terrain), « trop cher vs Excel » (ROI en temps récupéré et litiges évités).

---

## 15. Priorisation globale (RICE simplifiée)

| Élément | Valeur | Effort | Risque | Priorité | Phase |
|---|---|---|---:|---|---|---|
| Finir Sprint 23 (outbox, préférences, page publique) | 8 | M | Moyen | 1 | Maintenant |
| DPA/CGU/registre avant données réelles | 10 | S | Faible | 1 | Maintenant |
| Sprints 24-29 (rapports→assurance Production) | 7-9 | M-XL | Faible-Moyen | 2 | Ensuite |
| Bêta commerciale (Sprint 30) | 10 | XL | Moyen | 3 | Ensuite |
| Facturation automatisée | 9 | M | Moyen | 4 | Ensuite |
| Site public, support pro, pen test, charge, i18n, analytics | 6-8 | M-L | Faible | 5 | Plus tard |
| Marketplace, IA, iOS, WMS, portail client | 2-4 | XL | Élevé | 6 | À exclure |

---

## 16. Verdict de faisabilité

**Faisable techniquement ; commercialement incertain jusqu'à preuve pilote payante.**

- Le produit recommandé : le noyau mission → preuve → exception actuel, terminé par les Sprints 23b-29, commercialisé manuellement à une cohorte de 5-10 organisations de livraison locale, puis durci commercialement.
- Périmètre exclu de la v1 : facturation automatisée avant preuve de paiement, site public avant bêta concluante, marketplace, IA, iOS, WMS, paie, portail client complet.
- Conditions de réussite : (1) au moins 2 organisations actives 4 j/sem pendant 2 semaines ; (2) au moins 2 paiements au prix testé ; (3) DPA/CGU signés ; (4) marque vérifiée (INPI) ; (5) coût de support/tenant mesuré et absorbable.

---

## 17. Roadmap — 20 sprints restants

R-01 à R-08 correspondent aux fiches existantes (fin Sprint 23 + SPRINT-24..30). R-09 à R-20 sont des sprints **nouveaux proposés** pour la commercialisation ; ils nécessitent une décision enregistrée dans `.agent/DECISIONS.md` avant démarrage. Gates : R-08 (bêta), R-12 (support viable), R-19 (GA), R-20 (revue de croissance).

### Phase 1 — Finaliser le MVP commercial (R-01 → R-07)

#### Sprint R-01 — Clôture Sprint 23 : statut destinataire complet (L)

- **Objectif métier** : un destinataire consulte une fenêtre ETA prudente et des transitions utiles, sans appeler, sans données excessives.
- **Résultat attendu** : lien opaque fonctionnel de bout en bout, préférences tenant, outbox dédupliquée, page publique et métriques minimisées ; quality gate verte.
- **Travaux fonctionnels** : préférences tenant (consentement/canal/langue/heures silencieuses), génération lien Admin-only, page publique sans compte, correction de coordonnées, mesure de vues utiles.
- **Backend** : entités de préférences, outbox transactionnelle avec déduplication par clé, dispatcher de canaux (email/SMS via fournisseur, logs en dev), révocation immédiate.
- **Frontend/UX** : page publique accessible (contraste, focus, `no-store`), écran Admin de gestion des liens, états loading/empty/error.
- **Données** : migration préférences + liens existants (migration additive, réversible).
- **Infra/DevOps** : configuration fournisseur de notifications, feature flag tenant.
- **Sécurité** : rate limit conservé, token SHA-256 seul, aucune position précise exposée, cache headers vérifiés.
- **Tests** : expiration/révocation, token guessing, minimisation, fuseaux/langues, déduplication, panne canal, accessibilité, charge de consultation.
- **Documentation** : fiche SPRINT-23 mise à jour, `HANDOFF.md`, `PROJECT_STATE.json`.
- **Critères d'acceptation** : les 6 critères de la fiche SPRINT-23 cochés ; une transition génère au plus une notification par canal ; révocation effective immédiatement.
- **Livrable démontrable** : lien reçu → fenêtre mise à jour → lien inutilisable après clôture/révocation.
- **Indicateurs** : notifications délivrées, vues utiles, appels évités estimés.
- **Dépendances** : slice `RecipientStatusLink` existante ; outbox existante.
- **Risques** : vie privée, ETA inexacte — mitigés par minimisation et fenêtres larges.
- **Protection de l'existant** : ne pas casser la lecture publique existante ni les routes Admin de création/révocation.
- **Rollback** : révocation globale des liens actifs ; migration additive.
- **Contexte agentique** : `apps/backend/FleetOps.Api/.../RecipientStatus*`, migration EF ; `dotnet build -c Release`, `dotnet test`, gate `scripts/quality-gate.ps1` ; interdit : stocker le token en clair, exposer position/adresse.

#### Sprint R-02 — Rapports opérationnels et indicateurs de valeur (SPRINT-24, L)

Reprise de la fiche `SPRINT-24-OPERATIONAL-REPORTING.md` : dictionnaire versionné d'indicateurs, tableaux jour/semaine/mois avec drill-down, temps de préparation/prise en charge/clôture, rapports maintenance/conformité, export CSV/PDF daté, envoi périodique via outbox, recommandations déterministes. Critères : définition/source/période/fuseau/fraîcheur visibles ; concordance Web/export/API sur dataset de référence ; budgets de latence tenus ; aucun classement comportemental opaque. Livrable : comparaison de deux semaines avec cause d'un recul et export daté. Rollback : feature flags d'agrégats. Effort : L. (Détail complet dans la fiche existante, qui reste la source de vérité.)

#### Sprint R-03 — Hub d'intégrations fiable (SPRINT-25, XL)

Reprise de la fiche `SPRINT-25-INTEGRATION-HUB.md` : catalogue de connexions, versionnage/dépréciation des contrats, webhooks (signatures rotatives, retry, DLQ, replay ciblé), mappings d'import/export, diagnostics corrélés et redigés, quotas par tenant/clé, pagination curseur, kit partenaire. **Gate Sprint 25** : poursuivre l'industrialisation seulement si un connecteur réel est exploitable et si les métriques montrent une réduction d'appels, de ressaisie ou du temps de clôture. Livrable : webhook en échec → DLQ → correction → replay unique avec timeline. Rollback : chaque intégration désactivable isolément. Effort : XL.

#### Sprint R-04 — Administration des appareils et diagnostic support (SPRINT-26, L)

Reprise de la fiche `SPRINT-26-DEVICE-SUPPORT.md` : inventaire Android (version, dernière sync, âge de file, permissions), appareil télématique (firmware, dernière communication, connecteur), code de diagnostic exportable redigé et expirant, révocation/désappairage audités, version minimale supportée + bannière, runbooks guidés pour 5 incidents fréquents, rôle support plateforme just-in-time seulement si validé. Livrable : conducteur qui ne synchronise plus → cause identifiée → diagnostic redigé → service rétabli sans accès base. Effort : L.

#### Sprint R-05 — Cycle de vie des données et performance cible (SPRINT-27, XL)

Reprise de la fiche `SPRINT-27-DATA-LIFECYCLE-PERFORMANCE.md` : inventaire des volumes/classifications, rétention configurable bornée (télémétrie, audit, médias, logs, exports), purge batchée avec dry-run/curseur/journal, pagination curseur sur toutes les listes volumineuses, profilage index/N+1, tests charge/soak 30 puis 100 véhicules, backup/restore respectant RPO/RTO. Critères : aucune purge volumique chargée en mémoire ; conservation légale respectée ; budgets p95 documentés et tenus. Livrable : dataset de plusieurs mois purgé/archivé, carte et rapports rapides, restauration conforme. Effort : XL.

#### Sprint R-06 — Design system, accessibilité et espaces de travail (SPRINT-28, L)

Reprise de la fiche `SPRINT-28-DESIGN-SYSTEM-ACCESSIBILITY.md` : tokens communs Web/Compose, composants partagés (tableaux, filtres, timeline, empty/error/loading), navigation clavier/focus/live regions sur les flux P0, WCAG 2.2 AA automatisable, espaces par rôle avec préférences serveur, responsive/zoom 200 %, tests visuels stables. Critères : flux login/exception/dispatch/preuve/support utilisables au clavier ; préférences sans élévation de rôle. Livrable : Admin, Operator et Driver accomplissent leur tâche principale avec technologie d'assistance sur deux formats d'écran. Effort : L.

#### Sprint R-07 — Résilience, observabilité et assurance sécurité (SPRINT-29, XL)

Reprise de la fiche `SPRINT-29-PRODUCTION-ASSURANCE.md` : SLI/SLO (disponibilité API, fraîcheur tracking, sync, outbox, uploads, latence), dashboards/alertes avec runbooks, séparation bornée des boucles worker, backup/rotation de secrets automatisés, threat model + SAST/DAST + scan images, tests de pannes (SQL, objet, fournisseur, réseau mobile, redémarrage), processus d'incident. Critères : chaque alerte a propriétaire/seuil/runbook/test ; restauration périodique prouvée ; aucune vulnérabilité critique ouverte non acceptée. Livrable : game day (coupures fournisseur + stockage, redémarrage worker, restauration SQL) sans perte métier. Effort : XL.

### Phase 2 — Conformité et validation commerciale (R-08 → R-09)

#### Sprint R-08 — Conformité légale commerciale : DPA, CGU, registre, rétention (L, nouveau)

- **Objectif métier** : pouvoir embarquer des clients réels sans risque juridique.
- **Résultat attendu** : CGU, DPA, registre de traitements, politique de rétention contractuelle, mention légale des simulateurs, vérification de marque (INPI) enclenchée.
- **Travaux fonctionnels** : consentements (RGPD) côté employeur/conducteur, information des salariés, droits d'accès/export/suppression couverts par l'API existante, matrice de sous-traitants.
- **Backend** : export/purge déjà présents ; combler les écarts identifiés (journalisation minimale, bornes de rétention par type).
- **Frontend** : pages légales, bandeau de consentement tenant.
- **Données** : classification et durées documentées par entité.
- **Sécurité** : revue des flux de données personnelles (positions, identités, photos) ; validation externe.
- **Tests** : parcours d'exercice des droits (export/suppression) E2E.
- **Documentation** : documents juridiques publiés (avec mention « à validation par conseil »), D-0xx.
- **Critères d'acceptation** : DPA/CGU signables ; registre à jour ; exercice des droits testé ; marque vérifiée ou renommage décidé.
- **Livrable démontrable** : dossier de conformité prêt pour la bêta.
- **Indicateurs** : délai de traitement des demandes de droits.
- **Risques** : dépendance à un conseil externe ; prévoir 2 semaines tampon.
- **Rollback** : réversible (documents), sauf décision de renommage.
- **Contexte agentique** : docs + API export/purge ; interdit : inventer un avis juridique.

#### Sprint R-09 — Bêta commerciale et décision de disponibilité générale (SPRINT-30, XL)

Reprise de la fiche `SPRINT-30-COMMERCIAL-BETA.md` : cohorte de 5-10 organisations dans la niche décidée, offres figées, onboarding payant, souscription **manuelle**, documentation client/SLA/DPA, instrumentation funnel sans analytics intrusif, opération du support, comparaison coûts/revenus, décision `GO`/`SIMPLIFY`/`PIVOT`/`STOP`. Critères : 5 organisations onboardées, 3 actives chaque semaine pendant 8 semaines, 2 payantes au prix testé, objectifs sync/preuve/disponibilité tenus ou écarts acceptés, coût de support connu par tenant/véhicule. Livrable : revue de bêta (funnel, usage, fiabilité, incidents, revenus/coûts, retours) avec décision signée. Effort : XL. **Gate finale** : la suite de la roadmap (R-10+) ne démarre qu'après un `GO` documenté.

### Phase 3 — Commercialisation (R-10 → R-13)

#### Sprint R-10 — Facturation SaaS automatisée (L, nouveau)

- **Objectif métier** : transformer la volonté de payer prouvée en revenu récurrent gérable.
- **Résultat attendu** : souscription self-service ou assistée, prélèvements, factures, limites d'usage par offre, gel/dégradation gracieuse, export comptable.
- **Backend** : intégration fournisseur de paiement (Stripe/équivalent) via service applicatif isolé, webhooks de paiement idempotents, état d'abonnement tenant, feature flags par offre.
- **Frontend** : écrans compte/facturation Admin, mise à niveau, historique de factures.
- **Données** : `Subscription`, `Invoice` (ou références fournisseur), limites par plan.
- **Sécurité** : jamais de PAN en base ; tokens fournisseur ; audit des changements de plan.
- **Tests** : parcours souscription/paiement/échec/résiliation en mode test fournisseur ; tenant isolation.
- **Documentation** : offres, prix, procédure comptable.
- **Critères d'acceptation** : un client souscrit, paie, reçoit une facture et est dégradé proprement en cas d'échec, sans intervention manuelle bloquante.
- **Livrable démontrable** : paiement réel en environnement de test fournisseur, de bout en bout.
- **Indicateurs** : MRR, délai de souscription, taux d'échec de paiement.
- **Risques** : fiscalité/obligations comptables — conseil externe ; rollback : retour au manuel (désactivation du module).
- **Contexte agentique** : interdit : stocker des numéros de carte, inventer des obligations fiscales.

#### Sprint R-11 — Site public, onboarding self-service et documentation client (M, nouveau)

Objectif : vendre sans être présent. Landing (positionnement livraison locale), pages tarifs/offres, essai ou démo guidée, onboarding self-service du tenant (réutilisant Sprint 15), docs client (guide Admin/Operator/Driver), FAQ, statut de service public. Critères : un prospect inconnu comprend l'offre, demande une démo et peut être onboardé sans intervention technique ; docs en français, nom vérifié. Livrable : parcours prospect → démo → tenant provisionné. Risques : marque provisoire → figer avant publication.

#### Sprint R-12 — Support client professionnel (M, nouveau)

Objectif : absorber la croissance sans casser la marge solo. Tickets (fournisseur ou interne léger), catégories/SLA par offre, base de connaissances réutilisant les runbooks Sprint 26, statut de service, revues hebdo des tickets. Critères : 5 incidents fréquents résolus par runbook ; temps de première réponse mesuré ; coût de support/tenant publié. Livrable : un client ouvre un ticket, est guidé par un runbook, clôt en autonomie ou avec une réponse datée.

#### Sprint R-13 — Premier connecteur télématique réel certifié (XL, nouveau)

Objectif : sortir de la sandbox virtuelle (D-017) avec un fournisseur réel sans couplage. Contrat signé, adaptateur HTTP (selon D-006), credentials avec rotation, ingestion réelle sur flotte pilote, documentation du remplacement (Sprint 25). Critères : positions réelles ingérées par le même chemin canonique que la sandbox ; changement de fournisseur sans régression ; rotation de clés sans indisponibilité. Risques : dépendance contractuelle ; rollback : retour à la sandbox/import CSV. Ne pas démarrer sans accord Product Owner sur le fournisseur.

### Phase 4 — Durcissement et échelle (R-14 → R-17)

#### Sprint R-14 — Pen test externe, DAST et remédiation (L, nouveau)

Objectif : prouver la sécurité avant GA. Pen test externe sur le périmètre (API, Web, public), SAST/DAST en CI, scan antivirus des uploads, revue des dépendances, remédiation P1/P2 avec tests de non-régression. Critères : aucune vulnérabilité critique/haute non acceptée ; rapport daté ; remédiations testées. Livrable : rapport + preuve de remédiation. Risque : découvre des failles transverses → buffer budgétaire.

#### Sprint R-15 — Charge et performance à l'échelle cible (M, nouveau)

Objectif : tenir 100 véhicules/tenant et plusieurs tenants sans dégradation. Baselines p95, profils N+1/index, pagination partout, tests de charge/soak en CI nightly, objectifs publiés (SLI/SLO). Critères : budgets tenus sur la volumétrie de référence ; aucune régression après R-14. Livrable : rapport de charge daté.

#### Sprint R-16 — Localisation et formats (M, nouveau)

Objectif : servir le marché initial proprement (français) et préparer l'anglais si le marché l'exige. Formats date/nombre/devise, fuseaux partout, chaînes i18n, langue par tenant, fallback. Décision à enregistrer : EN oui/non selon la bêta. Critères : aucun calcul de fuseau faux dans les rapports (dataset de référence). Livrable : changement de langue complet sur un tenant de test.

#### Sprint R-17 — Analytics produit non intrusif (M, nouveau)

Objectif : mesurer acquisition → activation → usage → rétention sans profilage. Événements anonymisés par tenant, tableau de funnel Admin, corrélation usage/valeur (appels évités, preuves complètes), zéro PII dans les événements. Critères : funnel lisible ; consentement respecté ; aucune donnée personnelle exportée. Livrable : revue mensuelle du funnel sur cohorte.

### Phase 5 — Lancement et consolidation (R-18 → R-20)

#### Sprint R-18 — Stabilisation des retours bêta (M, nouveau)

Objectif : éliminer les frottements avant GA. Backlog des retours clients priorisé, bugs P1/P2, améliorations UX ciblées (top 5), tests de non-régression sur parcours modifiés, revue de dette. Critères : aucun P1 ouvert ; les 5 premiers retours traités avec preuve. Livrable : release candidate figée. Protection de l'existant : aucun changement de contrat sans migration.

#### Sprint R-19 — Lancement commercial (GA) (L, nouveau)

Objectif : ouvrir les vannes. Offres finales, page de lancement, communication réseau, onboarding commercial, support en place, statut de service public, releases progressives, revue d'incidents. Critères : les 5 premières organisations hors bêta souscrivent sans développement spécifique. Livrable : lancement daté avec funnel et support opérationnels. Indicateurs : CAC, MRR, activation < 1 jour.

#### Sprint R-20 — Consolidation et décision de croissance (M, nouveau)

Objectif : décider de la suite sur des faits. Revue 90 jours : marge par tenant/véhicule, rétention, support, usage du hub, demande de nouvelles fonctions ; backlog v2 ; décision documentée `CROISSANCE`/`MAINTIEN`/`PIVOT`/`ARRÊT` ; mise à jour de la roadmap long terme. Critères : marge et rétention connues ; décision signée. Livrable : rapport de consolidation.

---

## 18. Risques et conditions de réussite

| Risque | Probabilité | Impact | Réduction | Critère d'arrêt |
|---|---|---:|---|---|
| Volonté de payer inférieure au prix testé | Élevée | Critique | Pricing test, lettres d'intention avant billing | Personne ne paie après 3 pilotes corrigés |
| Adoption conducteurs faible | Moyenne | Élevé | Android « prochaine action », formation courte | < 50 % d'actifs semaine 1 sur la bêta |
| Coût de support non absorbable en solo | Élevée | Moyen | Runbooks, self-service, cohortes limitées | Support > x €/véhicule/mois (seuil bêta) |
| Fuite inter-tenant | Faible | Critique | Tests tenant systématiques, revues | Incident avéré → gel + audit externe |
| Dépendance fournisseur GPS | Moyenne | Moyen | Adaptateurs isolés, API ouverte | Verrouillage contractuel sans alternative |
| Nom/marque invalide (INPI) | Possible | Élevé | Vérification R-08 avant publication | Opposition → renommage avant GA |
| Non-conformité RGPD (photos, géoloc) | Moyenne | Élevé | DPA/CGU, minimisation, rétention bornée | Avis juridique négatif → périmètre réduit |
| ETA destinataire inexact | Moyenne | Moyen | Fenêtres prudentes, fraîcheur affichée | Plaintes récurrentes → désactivation tenant |

Conditions indispensables : 2 organisations actives et 2 paiements à la bêta ; DPA/CGU ; marque vérifiée ; quality gate verte à chaque sprint ; aucune dette critique acceptée sans décision enregistrée.

---

## 19. Prochaines actions (immédiates)

1. Terminer Sprint 23 (préférences, outbox dédupliquée, page publique, métriques) et passer la quality gate complète.
2. Vérifier la marque (INPI) et trancher le nom commercial (D-007) avant toute publication publique.
3. Relancer le pilote réel selon `docs/02-engineering/PILOT_RUNBOOK.md` (2-3 organisations, 2 semaines) pour transformer les critères `NON VÉRIFIÉS` en preuves.
4. Préparer DPA/CGU/registre avec un conseil juridique (sprint R-08) avant données réelles de clients.
5. Enregistrer dans `.agent/DECISIONS.md` la décision d'ajouter les sprints R-09 à R-20 et de réconcilier `ROADMAP.md`/`PROJECT_STATE.json` (Sprint 23 en cours).

---

## Tableau de synthèse

| Domaine | Note /10 | Évaluation | Risque principal | Action prioritaire |
|---|---|---:|---|---|---|
| Clarté du problème | 9 | Confirmé et documenté | Aucun | — |
| Importance du besoin | 8 | Fréquent, coûteux | Marché concurrentiel | Niche stricte |
| Valeur utilisateur | 8 | Cœur démontrable | Non mesurée en réel | Pilote payant |
| Proposition de valeur | 7 | Claire, différenciante, non mesurée | Promesse non prouvée | Métriques bêta |
| Cohérence fonctionnelle | 8 | Parcours complet cohérent | ETA/destinataire sensible | Fin Sprint 23 |
| Faisabilité technique | 8 | Stack éprouvée, gates vertes | Aucun blocage | Maintenir la discipline |
| UX et facilité d'adoption | 7 | Bonne mais dense ; a11y partielle | Adoption conducteur | Sprint R-06 |
| Sécurité et conformité | 7 | Solide socle, DPA absent | Pen test jamais réalisé | R-08 + R-14 |
| Modèle économique | 6 | Offres définies, pas de billing | Volonté de payer | Bêta payante |
| Volonté probable de payer | 4 | NON VÉRIFIÉE | Prix peut-être trop haut | 2 paiements bêta |
| Facilité de commercialisation | 5 | Pas de site, marque provisoire | Canal non prouvé | R-11 + R-19 |
| Différenciation | 6 | Workflow vs trackers | Concurrents riches | Restreindre la niche |
| Coût de développement | 7 | Déjà largement investi, reste ~20 sprints | Dérive de périmètre | Gates strictes |
| Coût d'exploitation | 7 | Monolithe léger, petit volume | Support humain | Runbooks + mesure |
| Scalabilité | 7 | Suffisante jusqu'à 100 véh./tenant | Base unique | R-05 + R-15 |
| Risque global | 5 | Maîtrisé techniquement, commercial incertain | Volonté de payer | Bêta avant billing |

- **Problème principal identifié** : coordination missions/incidents/preuves dispersée entre Excel, téléphone et messagerie.
- **Utilisateur principal** : responsable d'exploitation d'une petite flotte.
- **Client payeur** : dirigeant / responsable d'exploitation.
- **Produit central recommandé** : workflow mission → conducteur offline → preuve → exception.
- **Fonction la plus importante** : dispatch + preuve de livraison numérique.
- **Fonction la plus différenciante** : conducteur offline-first avec preuve photo/signature.
- **Fonction la plus risquée** : statut destinataire public (vie privée, ETA).
- **Fonctions à supprimer ou reporter** : portail client complet, IA/RAG, WMS, paie, iOS, optimisation propriétaire, marketplace.
- **Architecture recommandée** : monolithe modulaire actuel, sans microservices.
- **Modèle économique recommandé** : abonnement par flotte, facturation manuelle en bêta puis automatisée.
- **Marché initial recommandé** : livraison locale, 5-20 véhicules.
- **Prix initial à tester** : 79 €/mois (Essentiel, 10 véhicules) + onboarding 300-900 €.
- **Niveau de difficulté** : modéré (base saine, commercial incertain).
- **Budget indicatif** : faible en infra ; principal coût = temps de développement restant + conseil juridique + pen test (~5-15 k€).
- **Durée indicative jusqu'au MVP commercial** : 8 sprints (R-01 → R-09).
- **Durée indicative jusqu'au produit commercial** : 20 sprints (R-01 → R-20).
- **Nombre de sprints recommandé** : 20 restants.

## Verdict global

**Poursuivre le concept dans sa forme actuelle**, sans élargir le périmètre, avec des gates commerciales strictes.

Réponses aux 15 questions :

1. **Le problème mérite-t-il d'être résolu ?** Oui.
2. **Le concept est-il cohérent ?** Oui — 23 sprints livrés avec gates vertes le démontrent.
3. **Le produit proposé est-il la bonne solution ?** Oui pour la niche 5-30 véhicules ; non en dehors.
4. **Partie la plus précieuse du brainstorming ?** Le noyau mission → preuve → exception.
5. **Partie à abandonner/reporter ?** Portail client, IA, marketplace, iOS, WMS, paie ; MQTT applicatif.
6. **Réalisable avec les ressources disponibles ?** Oui — développement agentique encadré, une verticale à la fois.
7. **POC obligatoire ?** Non — le POC est déjà validé ; ce qui manque est la preuve commerciale externe.
8. **Périmètre exact du MVP ?** Cœur livré + R-01..R-09 (fin Sprint 23, rapports, hub, support, lifecycle, design system, assurance Production, conformité, bêta).
9. **Segment à cibler en premier ?** Livraison locale 5-20 véhicules.
10. **Modèle économique à tester ?** Abonnement par flotte, souscription manuelle d'abord.
11. **Architecture à choisir ?** Monolithe modulaire actuel.
12. **Trois plus grands risques ?** Volonté de payer non prouvée ; coût de support solo ; nom/marque non vérifiés.
13. **Conditions d'arrêt ?** Aucun paiement après bête corrigée ; adoption conducteurs < seuil ; support économiquement insoutenable ; avis juridique bloquant.
14. **Niveau commercial en ~20 sprints ?** Oui, avec les 12 sprints de commercialisation ajoutés (R-09..R-20).
15. **Trois prochaines actions ?** Finir Sprint 23 + quality gate ; vérifier la marque ; relancer le pilote payant.
