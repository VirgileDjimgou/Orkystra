# Audit complet FleetOps — 17 juillet 2026

## 1. Résumé exécutif

FleetOps est un MVP de gestion opérationnelle pour petites flottes de 5 à 30 véhicules, composé d’un Web Admin/Operator, d’une application Android Driver, d’une API ASP.NET Core, d’un worker, de SQL Server et d’un simulateur GPS.

Le produit couvre réellement le registre de flotte, le tracking, le dispatch, le workflow conducteur hors ligne, les inspections, les preuves de livraison, les alertes, la maintenance légère, les intégrations et l’audit. La quality gate initiale est verte : 93 tests backend, 15 tests Web, build/tests Android, builds .NET/Web et health checks.

L’architecture de monolithe modulaire est appropriée et doit être conservée. Le modèle multi-tenant est explicite, les index et migrations sont substantiels, et les endpoints sensibles sont généralement autorisés côté serveur.

Le projet n’est toutefois pas prêt pour un pilote contenant des données réelles. En Production, l’API retombe sur une clé JWT connue, le compose possède un fallback de clé média connu et le bootstrap crée des comptes de démonstration aux mots de passe publics. Le login n’est ni rate-limité ni verrouillé après échecs.

La restauration SQL annoncée n’est pas prouvée et les scripts PowerShell associés ne se parsèrent pas. Tous les tests d’intégration utilisent EF Core InMemory ; aucun test ne valide SQL Server, les migrations ou les contraintes relationnelles. Aucun E2E Playwright n’existe malgré le script npm. Docker Desktop étant indisponible pendant l’audit, les images, la restauration et le scénario conteneurisé complet restent non vérifiés.

L’UX Web est cohérente mais très documentaire, dense et peu responsive ; les écrans Alertes, Dispatch et Intégrations dépassent 700 lignes. Le dashboard duplique les notifications par canal. Android est fonctionnel et offline-first mais reste visuellement basique, sans caméra native, sans test instrumenté et sans icône applicative.

Le niveau réel est **6/10 — MVP utilisable**, et non « prêt production ». La recommandation initiale était **poursuivre après stabilisation**, en gardant le périmètre cœur et en limitant d’abord la roadmap totale à quinze sprints, Sprint 00 à Sprint 14. Cette limite de gouvernance a ensuite été remplacée par la décision produit D-012 : onze sprints terminés (`00–10`) et exactement vingt sprints planifiés (`11–30`) avec gates de valeur, sans élargir les exclusions architecturales.

Les trois priorités sont : sécuriser le bootstrap Production, prouver SQL/backup/E2E, puis enrichir l’expérience opérateur et conducteur autour des exceptions, de la prochaine action et de l’onboarding.

## 2. Fiche d’identité du projet

| Élément | Conclusion vérifiée |
|---|---|
| Nom | Orkystra FleetOps ; nom commercial encore provisoire selon `.agent/DECISIONS.md` |
| Type | SaaS B2B multi-tenant de gestion opérationnelle de flotte |
| Cible | PME de transport/service, 5 à 30 véhicules, encore sur Excel/papier/messagerie |
| Utilisateurs | Administrateur, opérateur/dispatcher, conducteur Android |
| Payeur probable | Dirigeant, responsable d’exploitation ou gestionnaire de flotte |
| Problème | Visibilité dispersée sur véhicules, missions, incidents, conformité et preuves |
| Valeur | Réduire appels et ressaisie, accélérer la clôture des missions, détecter les exceptions |
| Maturité | MVP utilisable ; pilote réel bloqué par sécurité et preuves d’exploitation |

Explication non technique : une petite entreprise configure ses véhicules et conducteurs, suit les positions, assigne une tournée, laisse le conducteur travailler même sans réseau, récupère inspection et preuve de livraison, puis traite les alertes dans une console unique. FleetOps doit être choisi pour sa simplicité et son flux métier complet, pas pour concurrencer les plateformes télématiques matérielles sur la profondeur analytique.

Proposition de valeur :

> Pour les responsables de petites flottes qui coordonnent encore les missions, incidents et preuves entre Excel, téléphone et messagerie, FleetOps permet de piloter une mission complète dans un espace partagé et traçable, contrairement à un simple tracker GPS, grâce à un workflow Web + conducteur offline-first centré sur les exceptions.

## 3. Fonctionnement général

Parcours confirmé dans le code et les captures :

```text
Administrateur configure organisation, utilisateurs, véhicules et appareils
    ↓
Appareil/simulateur envoie une position idempotente
    ↓
Opérateur consulte carte, alertes et missions
    ↓
Opérateur crée, affecte et suit une mission
    ↓
Conducteur synchronise la mission dans Android/Room
    ↓
Conducteur progresse hors ligne, inspecte et produit une preuve
    ↓
Worker scanne alertes et distribue les webhooks via outbox SQL
    ↓
Opérateur vérifie timeline, preuve et exception
```

Fonctions centrales : identité/tenant, registre, tracking, dispatch, conducteur offline, inspections/POD et alertes. Fonctions secondaires : intégrations partenaires, CSV, MFA, export/purge et observabilité. Il n’existe aucune fonction IA, et c’est adapté au besoin actuel.

## 4. Architecture et flux

```text
Vue Admin/Operator ───── REST + SignalR ─┐
Android Driver ───────── REST/WorkManager ├─ ASP.NET Core API ─ SQL Server
GPS Simulator/partenaire ─ REST/API key ─┘         │              │
                                                   ├─ média signé ├─ outbox
FleetOps Worker ─ alert scans + webhooks ──────────┘              └─ audit
```

Architecture actuelle : monolithe modulaire en projets Core, Infrastructure, API et Worker. Les vertical slices résident surtout dans l’API ; la persistance est EF Core. SignalR porte les positions, HTTP l’ingestion, et MQTT n’est qu’une infrastructure Docker non consommée par le code.

Avantages : déploiement simple, transactions locales, migrations uniques, coût faible, domaine lisible. Limites : endpoints très volumineux, absence de filtre tenant global, worker séquentiel, tests relationnels absents et dépendance à une seule base.

Architecture recommandée : conserver le monolithe, extraire progressivement des services applicatifs et composants UI autour des slices volumineuses, ajouter tests SQL Server conteneurisés, options de production validées et stockage objet réellement branché avant montée en charge. Ne pas introduire microservices, bus externe, CQRS/MediatR ou optimisation propriétaire.

## 5. Inventaire technique

| Composant | Chemin | Responsabilité | Technologie | État | Risques |
|---|---|---|---|---|---|
| API | `apps/backend/FleetOps.Api` | HTTP, auth, SignalR, composition | ASP.NET Core 10 | Fonctionnelle/testée InMemory | config Production, slices longues |
| Domaine | `apps/backend/FleetOps.Core` | Entités et invariants | C# 14/.NET 10 | Fonctionnel/testé | services applicatifs peu séparés |
| Infrastructure | `apps/backend/FleetOps.Infrastructure` | EF, Identity, stockage, webhooks | EF Core/SQL Server 10 | Fonctionnelle | aucun test SQL réel |
| Worker | `apps/backend/FleetOps.Worker` | scans alertes + outbox | BackgroundService | Fonctionnel/testé unitairement | traitements séquentiels |
| Web | `apps/web` | console Admin/Operator | Vue 3.5, Pinia 3, Vite 7, Bootstrap 5, Leaflet | Fonctionnel | densité, responsive, a11y, gros composants |
| Android | `apps/android-driver` | workflow conducteur offline | Kotlin 2.3, Compose, Room, WorkManager | Fonctionnel de démonstration | token Room, caméra absente, 0 test instrumenté |
| Base | migrations dans Infrastructure | données transactionnelles | SQL Server 2022/EF migrations | 9 migrations cohérentes | restauration non prouvée |
| Médias | `FileSystemPrivateMediaStorage` | preuves privées | système de fichiers + URL HMAC | Partiel | MinIO/S3 documenté mais non utilisé |
| Tracking | API + SignalR + simulateur | positions et historique | REST, SignalR | Fonctionnel/testé | alias historiques, MQTT absent |
| MQTT | `infrastructure/mosquitto` | broker local | Mosquitto 2 | Infrastructure seule | aucun adaptateur applicatif |
| Conteneurs | compose + Dockerfiles | local/pilote | Docker Compose/Nginx | Config valide | moteur indisponible, images non vérifiées |
| CI | `.github/workflows/ci.yml` | build/test | GitHub Actions | Baseline présente | manque format/lint/vuln/E2E/lint Android |
| Tests | `tests`, specs Web, tests Android | non-régression | xUnit, Vitest, JUnit | 93/15 + Android verts | InMemory, pas E2E/SQL/instrumentation |
| Observabilité | Program/worker/docs | logs/traces/métriques | JSON logs, OpenTelemetry | Câblée | backend d’alerting non vérifié |

Versions majeures : .NET/ASP.NET/EF 10.0.7, Node 22 en CI, Vue 3.5, Vite 7, TypeScript 5.9, Android target/compile SDK 35, AGP 8.13.2. Les scans NuGet et npm ne signalent aucune vulnérabilité connue. Android lint signale 25 avertissements de versions/cible et packaging ; les mises à niveau majeures doivent être planifiées, pas appliquées en bloc.

## 6. État réel d’avancement

| Fonctionnalité | État réel | Preuves | Blocages | Confiance |
|---|---|---|---|---|
| Identity, rôles, tenants | Fonctionnelle et testée | `AuthIntegrationTests`, `CurrentTenant.cs` | seed/config Production | Élevée |
| Registre flotte | Fonctionnelle et testée | endpoints Fleet + tests intégration | pas de pagination globale | Élevée |
| Tracking/carte | Fonctionnel et testé | `TrackingIntegrationTests`, `FleetMapView.spec.ts` | MQTT non implémenté | Élevée |
| Dispatch | Fonctionnel et testé | domaine/endpoints/`DispatchView.spec.ts` | UX dense, peu de filtres | Élevée |
| Android offline | Fonctionnel et testé unitairement | Room, WorkManager, 3 suites Kotlin | pas de test appareil | Moyenne |
| Inspection/POD | Partiellement opérationnel | endpoints + tests + UI Android | caméra native absente | Élevée |
| Alertes/maintenance | Fonctionnel MVP | scanner, worker, dashboard | maintenance sans ordre de travail/coûts | Élevée |
| API/webhooks/CSV | Fonctionnel et testé | Sprint 08 tests | pagination/monitoring limités | Élevée |
| MFA/export/purge | Fonctionnel et testé | Sprint 09 tests et UI | session/seed Production | Élevée |
| Déploiement pilote | Interface/config seulement | Dockerfiles, compose | moteur Docker indisponible, secrets faibles | Élevée |
| Backup/restore | Non fonctionnel sous PowerShell | erreurs de parseur confirmées | scripts à corriger + test SQL | Élevée |
| E2E complet | Non commencé | aucun fichier Playwright | infrastructure/test data | Élevée |

Évaluation observable : fonctionnalités 78 %, Web 70 %, backend 82 %, données 72 %, intégrations 70 %, tests 66 %, sécurité 45 %, DevOps 58 %, documentation 72 %, préparation Production 42 %. Le projet est au niveau **6 — MVP utilisable**.

## 7. Résultats des builds et tests

| Vérification | Commande | Résultat | Erreur/limite | Gravité |
|---|---|---|---|---|
| Gate complète | `scripts/quality-gate.ps1` | PASS, 88 s | Docker config seulement | Information |
| Backend | `dotnet build/test -c Release` | 0 warning, 93/93 | EF InMemory | Moyenne |
| Web | format/lint/test/build | 15/15, build PASS | aucun E2E | Élevée |
| Android | unit tests + assembleDebug | PASS | tests up-to-date, pas instrumentés | Moyenne |
| Android lint | `gradlew lintDebug` | 0 erreur, 25 avertissements | icône/cible/versions | Faible |
| Migrations | `ef migrations has-pending-model-changes` | aucun écart | application SQL non rejouée | Moyenne |
| NuGet vulnérable | `dotnet list package --vulnerable` | aucun résultat | dépend des sources actuelles | Information |
| npm audit | `npm audit --omit=dev` | 0 vulnérabilité | transitifs dépréciés au build | Faible |
| Docker runtime | `docker version`, `compose ps` | Échec | moteur Linux absent | Élevée |
| Scripts recovery | parser PowerShell | Échec | `param` après une instruction | Élevée |

Images Docker, SQL réel, sauvegarde/restauration, backend OTLP externe, charge et scénario E2E : **Non vérifié avec les éléments actuellement disponibles.**

## 8. Bugs et anomalies

| ID | Problème | Statut | Emplacement | Impact | Gravité | Correction | Effort | Test requis |
|---|---|---|---|---|---|---|---|---|
| AUD-001 | Clé JWT Production connue par défaut et non injectée par compose | Confirmé | `JwtOptions.cs`, `docker-compose.pilot.yml` | forge de tokens inter-tenant | Critique | secret obligatoire + fail-fast | S | démarrage Production refusé |
| AUD-002 | Comptes/mots de passe de démonstration seedés en Production | Confirmé | `FleetOpsSeedData.cs`, `Program.cs` | accès administrateur connu | Critique | seed opt-in hors Production | M | Production sans utilisateurs démo |
| AUD-003 | Fallback de clé média connu | Confirmé | `ObjectStorageOptions.cs`, compose pilote | lecture de médias signés si ID connu | Élevée | secret obligatoire | S | fail-fast Production |
| AUD-004 | Login sans rate limit ni lockout enregistré | Confirmé | `AuthEndpointExtensions.cs`, `Program.cs` | brute force | Élevée | rate limit + Identity lockout | M | 429/verrouillage |
| AUD-005 | Scripts backup/restore PowerShell invalides | Confirmé | `scripts/sql-*.ps1` | reprise impossible sous Windows | Élevée | déplacer `param` en tête + parse gate | S | parser + exercice SQL |
| AUD-006 | Aucun test SQL Server/migration | Confirmé | `FleetOpsApiFactory.cs` | écarts EF/contraintes non détectés | Élevée | Testcontainers SQL | L | suite relationnelle CI |
| AUD-007 | Aucun E2E Playwright | Confirmé | `package.json`, absence de specs | parcours critique non protégé | Élevée | 3 parcours E2E | L | login/dispatch/POD |
| AUD-008 | Token Web dans `localStorage`, token Android en Room | Confirmé | `session.ts`, `DriverDatabase.kt` | vol de session sur appareil/XSS | Élevée | cookie BFF ou stockage chiffré/Keystore | L | tests session/upgrade |
| AUD-009 | Dashboard duplique les notifications par canal | Confirmé | `DashboardView.vue`, capture | bruit et perte de priorité | Moyenne | grouper événement + canaux | M | composant dashboard |
| AUD-010 | MinIO présent mais stockage réel filesystem | Confirmé | `DependencyInjection.cs`, compose | divergence architecture/résilience | Moyenne | adaptateur S3 ou docs honnêtes | L | test stockage objet |
| AUD-011 | MQTT non consommé | Confirmé | aucun package/consumer, Mosquitto seul | promesse appareil incomplète | Faible | conserver reporté selon D-006 | M | test adaptateur futur |
| AUD-012 | États Sprint déclarés DONE avec critères décochés | Confirmé | `sprints/*.md`, `PROJECT_STATE.json` | décisions basées sur preuves fausses | Élevée | réconciliation factuelle | M | revue documentaire |
| AUD-013 | Caméra native Android absente | Confirmé | rapport qualité, code UI | POD non commercial | Élevée | CameraX/photo picker + permissions | L | test instrumenté |
| AUD-014 | CI moins stricte que gate locale | Confirmé | `.github/workflows/ci.yml` | régression format/lint/sécurité | Moyenne | aligner les gates | M | workflow vert |

## 9. Qualité du code et dette technique

| Critère | Note /10 | Justification |
|---|---:|---|
| Lisibilité | 7 | noms explicites, conventions stables |
| Maintenabilité | 6 | slices et vues de 500–870 lignes |
| Modularité | 7 | projets et modules clairs, logique encore dans endpoints/UI |
| Cohésion/couplage | 7 | monolithe raisonnable, worker couple deux traitements |
| Gestion d’erreurs | 7 | Problem Details fréquent, quelques `NotFound` nus |
| Testabilité | 7 | bonne factory et stores, moteur relationnel absent |
| Extensibilité | 7 | outbox, interfaces stockage, rôles explicites |
| Dette technique | 5 | sécurité Production, gouvernance, E2E et recovery |

Goulets : listes sans pagination, dashboard chargé, worker unique et SQL unique. Pour 5–30 véhicules, aucun découpage distribué n’est justifié. Les entités et index tenant-aware sont solides, mais l’absence de `HasQueryFilter` impose de ne jamais oublier `OrganizationId` dans chaque requête : ajouter des tests d’architecture/tenant plutôt qu’un filtre magique sans analyse Identity.

## 10. Données, API et intégrations

Les entités opérationnelles portent `OrganizationId`; les index couvrent unicité, télémétrie, alertes, outbox et audit. Les row versions protègent plusieurs agrégats. Les migrations 00–08 sont cohérentes avec le snapshot.

Risques : aucune validation SQL réelle, suppression lifecycle volumineuse chargée en mémoire, stockage média local, absence de partition/rétention automatisée de télémétrie observée, et listes administratives plafonnées sans curseur.

L’API externe `/api/v1` est généralement correctement versionnée, mais auth/admin utilisent `/api/auth` et `/api/admin` sans déclaration explicite « internal ». Les webhooks ont HMAC, retry/dead-letter et outbox. Les alias `/api/tracking/latest` et `/api/simulation/telemetry` sont une dette de compatibilité à déprécier. MQTT reste reporté conformément à D-006.

## 11. Sécurité et conformité

Forces : ASP.NET Identity, JWT signé, MFA admin, contrôle de rôle serveur, tenant issu des claims, API keys hashées/scopées, HMAC webhooks, URL média expirante, audit et tests inter-tenant.

Faiblesses prioritaires : AUD-001 à AUD-004 et stockage de tokens. Les logs structurés doivent continuer d’exclure positions complètes, tokens et secrets. Les uploads valident le flux applicatif mais nécessitent un scan de contenu avant données réelles.

RGPD potentiel : positions et identité conducteur, photos/signatures, finalité, information des salariés, limitation de conservation, droits d’accès/export/suppression, sous-traitants et transferts. Le produit possède export/purge et rétention partielle, mais une analyse professionnelle, un DPA et un registre de traitements sont requis. Ceci n’est pas un avis juridique.

## 12. Tests et assurance qualité

| Parcours critique | Test existant | Test manquant | Risque | Priorité |
|---|---|---|---|---|
| Login/MFA/tenant | xUnit InMemory | brute force + Production config + E2E | critique | P0 |
| Registre/import | xUnit + quelques specs | SQL contraintes + E2E import | élevé | P1 |
| Tracking temps réel | xUnit + spec carte | SignalR navigateur/reconnexion/charge | élevé | P1 |
| Mission complète | domaine/API/spec vue | Draft→Completed Web+Android E2E | élevé | P0 |
| Offline sync | tests repository Android | instrumentation réseau/perte processus | élevé | P0 |
| Inspection/POD | API + unit Android | caméra/upload interrompu appareil | élevé | P0 |
| Backup/restore | aucun exercice | SQL réel + contrôle données | critique | P0 |
| Webhooks | xUnit InMemory | panne réseau/SQL/redémarrage process | moyen | P1 |

MVP indispensable : tests Production config, SQL/migrations, trois E2E critiques, instrumentation Android offline/caméra et restauration. Avant vente générale : performance, sécurité dynamique, chaos ciblé et restauration périodique. Peuvent attendre : tests massifs MQTT, multi-région et dizaines de milliers de véhicules.

## 13. UX/UI

Web : design cohérent, bons états vides et cartes, mais navigation sans regroupement/icônes, topbar peu utile, pages longues, notifications redondantes et responsive limité à un empilement. Les composants Alertes (793 lignes), Dispatch (730) et Intégrations (872) freinent l’évolution. L’accessibilité existe surtout via labels ; navigation clavier, focus visible, live regions et réduction d’animation sont peu couverts.

Android : zones tactiles correctes et information « Synced », mais hiérarchie très basique, action suivante peu évidente, aucun guidage vers l’arrêt, aucune caméra native et aucun état global de dernière synchronisation/pending queue visible dans les captures.

Cinq améliorations impact/effort :

1. Shell Web responsive, navigation groupée, titres contextuels et accès rapide aux tâches.
2. Regrouper les alertes/notifications par événement avec priorité, propriétaire et prochaine action.
3. Vue conducteur « prochaine étape » avec progression, sync globale, navigation et contact.
4. Onboarding guidé par rôle avec checklist de préparation de flotte.
5. Design tokens, focus clavier, annonces d’erreur et composants partagés sur les vues longues.

## 14. Produit et fonctionnalités manquantes

| Priorité | Fonctionnalité | Problème résolu | Impact | Effort | Risque |
|---|---|---|---:|---:|---:|
| Must | Bootstrap tenant sécurisé et onboarding | passer de démo à client réel | Très fort | M | Faible |
| Must | Centre d’exceptions actionnable | réduire appels et oubli d’incidents | Très fort | L | Moyen |
| Must | Parcours conducteur prochaine étape + caméra | exécuter réellement une livraison | Très fort | L | Moyen |
| Must | SQL/E2E/recovery prouvés | protéger données et vente pilote | Très fort | L | Faible |
| Should | Ordres de maintenance légers + coûts | transformer alerte en action suivie | Fort | L | Moyen |
| Should | Recherche/filtres/vues enregistrées | exploiter quotidiennement sans friction | Fort | M | Faible |
| Should | Rapports opérationnels simples | démontrer temps gagné/taux de preuve | Fort | M | Faible |
| Could | ETA/lien public limité | informer un destinataire | Moyen | L | Élevé |
| Could | Adaptateur MQTT fournisseur | connecter matériel direct validé | Moyen | L | Moyen |
| Won’t now | Optimisation propriétaire, iOS, IA/RAG, WMS, paie | dispersion hors cœur | Faible maintenant | XL | Élevé |

La niche la plus crédible est une entreprise de livraison/service de 5–30 véhicules qui veut un workflow mission + preuve + exception sans équipe IT ni plateforme matérielle lourde.

## 15. Éléments à simplifier ou supprimer

| Élément | Décision | Conséquence |
|---|---|---|
| Microservices/queue externe | Reporter | préserver vitesse et coût |
| Broker MQTT sans consommateur | Isoler et documenter comme option | éviter fausse promesse |
| Alias API historiques | Déprécier puis supprimer après compatibilité | réduire surface non sécurisée |
| Notifications par canal dans dashboard | Fusionner par événement | réduire bruit |
| Formulaires de démo dans vues principales | Isoler en mode démo | interface commerciale plus claire |
| MinIO non utilisé | soit brancher, soit retirer du pilote | aligner architecture et exécution |
| Grandes vues Vue | Extraire par flux, pas réécrire | améliorer tests/maintenabilité |
| Fonctionnalités `FUTURE_SCOPE` | Conserver reportées | éviter dilution du MVP |

## 16. Potentiel commercial et concurrence

Le problème est fréquent et coûteux, mais le marché est concurrentiel. FleetOps ne doit pas se vendre comme un tracker GPS de plus : sa différence crédible est le flux mission–conducteur offline–inspection–preuve–exception pour petite flotte, avec intégration télématique ouverte.

| Solution | Cible | Fonction | Prix public observé | Forces | Faiblesses face au positionnement |
|---|---|---|---|---|---|
| Excel + WhatsApp + téléphone | TPE | coordination manuelle | coût logiciel faible | familier, flexible | non audité, doublons, aucune vue temps réel |
| [Fleetio](https://www.fleetio.com/pricing) | petites à grandes flottes | maintenance/actifs/intégrations | 4–10 USD/véhicule/mois selon plan affiché | produit mature, essais, reporting | tracking matériel/workflow local à intégrer |
| [Quartix](https://www.quartix.com/es-es/precios/) | PME avec télématique | tracking, trajets, comportement | 8,99–12,99 EUR/véhicule/mois sur page Espagne | hardware + télématique mature | moins centré mission/POD personnalisable |
| [Samsara](https://www.samsara.com/guides/fleet-faq) | opérations connectées | plateforme/hardware large | devis, généralement par actif/mois et contrats pluriannuels | profondeur et écosystème | coût/complexité pour très petite flotte |

Prix et disponibilité dépendent du pays, du matériel et du contrat ; ils doivent être revalidés au moment de vendre.

Trois positionnements maximum :

1. **Livraison locale fiable** : 5–20 véhicules, mission/POD/exceptions, acquisition via intégrateurs IT et réseaux locaux, abonnement par flotte.
2. **Flotte de services terrain** : artisans/maintenance, inspection départ et preuve d’intervention, acquisition partenaires métiers, abonnement + onboarding.
3. **Couche opérationnelle au-dessus d’un GPS existant** : entreprises déjà équipées, API/webhooks/dispatch/driver, acquisition via revendeurs télématiques.

Grille tarifaire à tester, matériel/connectivité exclus : Essentiel 79 €/mois jusqu’à 10 véhicules ; Opérations 149 €/mois jusqu’à 25 ; Partenaire 299 €/mois jusqu’à 50 avec API et support prioritaire. Hypothèse : onboarding facturé séparément 300–900 €. Ne pas figer ces prix avant 3 pilotes payants.

Notes commerciales /10 : problème 8, pertinence 7, différenciation 5, maturité technique 6, UX 6, monétisation 5, rétention 7, scalabilité 7, commercialisation 5, risque global 5.

## 17. Risques majeurs

| Risque | Catégorie | Probabilité | Impact | Criticité | Prévention | Secours |
|---|---|---:|---:|---:|---|---|
| compromission via secrets/comptes démo | cybersécurité | élevée | critique | critique | Sprint 10 fail-fast/seed opt-in | rotation clés, purge sessions |
| perte de données/restauration impossible | données | moyenne | critique | critique | exercice automatisé SQL | export et restauration hors ligne |
| parcours mobile non fiable sur appareil | opérationnel | moyenne | élevé | élevé | instrumentation + pilotes terrain | procédure papier contrôlée |
| produit trop large sans niche | commercial | élevée | élevé | élevé | vendre un seul flux cœur | réduire offres et intégrations |
| adoption faible des conducteurs | humain/UX | moyenne | élevé | élevé | une action suivante, offline visible | formation courte/support |
| fuite inter-tenant par requête oubliée | sécurité | faible/moyenne | critique | élevé | tests tenant systématiques | audit et suspension tenant |
| dépendance au fournisseur GPS | externe | moyenne | moyen | moyen | API ouverte + adaptateurs isolés | simulateur/import manuel |
| coût de support d’un solo dev | humain | élevée | moyen | élevé | runbooks, scope réduit, observabilité | limiter pilotes simultanés |

Les cinq risques d’échec sont les cinq premières lignes.

## 18. MVP recommandé

Le MVP commercialement testable doit contenir : onboarding d’une organisation, rôles, registre, import simple, tracking connecté ou simulé, mission/affectation/statuts, Android offline, inspection, photo/signature réelles, centre d’exceptions, preuve opérateur, sauvegarde/restauration et audit.

Il ne doit pas contenir : facturation transport, WMS, optimisation propriétaire, portail client complet, iOS, maintenance avancée, CO₂, IA/RAG, microservices ou matériel propriétaire.

Utilisateur cible : responsable d’exploitation d’une société locale de livraison/service avec 5–20 véhicules et conducteurs Android.

Hypothèse principale : un workflow partagé réduit d’au moins 30 % les appels de statut et permet de clôturer plus de 90 % des missions avec preuve complète le jour même.

Métriques : activation sous 1 jour, 80 % de conducteurs actifs en semaine 1, 95 % de commandes synchronisées, 90 % de preuves complètes, réduction des appels, temps de clôture, rétention à 8 semaines, satisfaction et volonté de payer ≥ 100 €/mois.

Décision : poursuivre si deux pilotes utilisent le flux au moins 4 jours/semaine et un accepte de payer ; simplifier si l’usage se concentre sur mission/POD ; pivoter vers une couche opérationnelle si le tracking matériel domine ; interrompre si aucun pilote n’adopte le workflow après deux cycles d’onboarding corrigés.

## 19. Recommandations priorisées

| Recommandation | Horizon | Preuve | Composants | Impact | Effort | Critère d’acceptation |
|---|---|---|---|---:|---:|---|
| secrets obligatoires, seed opt-in, lockout/rate limit | immédiat | AUD-001–004 | API/compose/Web | critique | M | Production refuse config faible et aucun compte démo |
| réparer et exercer recovery | immédiat | scripts invalides/Docker absent | scripts/SQL | critique | M | backup restauré et checksum métier |
| tests SQL + 3 E2E | court terme | InMemory/aucun Playwright | tests/CI | très fort | L | CI relationnelle et parcours verts |
| shell UX + centre d’exceptions | court terme | captures et grosses vues | Web/API | fort | L | tâches clés ≤ 3 actions |
| prochaine étape + caméra Android | court terme | média démo | Android/API | très fort | L | mission terrain complète hors ligne |
| onboarding et métriques pilote | moyen terme | aucun funnel | Web/docs/observabilité | fort | M | activation et valeur mesurées |
| ordre maintenance léger | moyen terme | alerte sans exécution | domaine/Web | moyen | L | alerte→ordre→coût→clôture |
| charge et stockage objet | moyen terme | filesystem/non vérifié | infra/API | moyen | L | baseline 30 véhicules + S3 |

## 20. Roadmap initiale et extension approuvée

Les blocs Sprint 10–14 ci-dessous conservent la recommandation courte formulée pendant l’audit. Après livraison du Sprint 10, la décision D-012 a remplacé l’ordre des Sprints 11–14 et étendu la trajectoire jusqu’au Sprint 30. `ROADMAP.md` et les fiches `sprints/SPRINT-11-*.md` à `sprints/SPRINT-30-*.md` sont désormais les sources de vérité d’exécution.

### Sprint 10 — Vérité Production et socle UX

**Objectif :** supprimer les risques bloquant un pilote et rendre les shells Web/Android plus clairs.

**Problèmes résolus :** AUD-001 à AUD-005, mode démo exposé, navigation et packaging Android.

**Tâches principales :** secrets fail-fast, seed opt-in, login rate limit/lockout, scripts recovery valides, CI/gate renforcée, shell Web responsive, thème/sync Android.

**Composants concernés :** API, Infrastructure, Web, Android, scripts, docs.

**Dépendances :** aucune migration ; secrets d’environnement requis en Production.

**Tests requis :** config Production, lockout, parsing scripts, tests UI, lint/build complet.

**Critères d’acceptation :** aucun secret faible accepté, aucun compte démo Production, login protégé, scripts parsables, deux clients restent utilisables.

**Livrable démontrable :** lancement dev explicite et refus Production faible, navigation Web/Android modernisée.

**Risques et rollback :** bootstrap trop strict ; rollback via options explicites sans rupture de données.

**Estimation :** L.

### Sprint 11 — Données prouvées et parcours E2E

**Objectif :** démontrer que le produit fonctionne sur son infrastructure réelle.

**Problèmes résolus :** AUD-006, AUD-007, recovery non prouvée.

**Tâches principales :** Testcontainers SQL, migrations, backup/restore automatisé, Playwright login/dispatch/tenant, instrumentation Android offline.

**Composants concernés :** tests, CI, SQL, Web, Android.

**Dépendances :** moteur Docker stable.

**Tests requis :** relationnels, migrations, E2E, redémarrage worker.

**Critères d’acceptation :** scénario mission complet et restauration vérifiés en CI dédiée.

**Livrable démontrable :** rapport automatique avant/après restauration.

**Risques et rollback :** CI lente ; séparer gate rapide et gate nightly.

**Estimation :** XL.

### Sprint 12 — Centre d’opérations actionnable

**Objectif :** permettre à l’opérateur de travailler par exception et non par lecture de listes.

**Problèmes résolus :** bruit dashboard, recherche/filtres absents, vues denses.

**Tâches principales :** boîte d’exceptions groupée, filtres/vues enregistrées, propriétaire/SLA, mission board compact, recherche globale, composants Vue extraits.

**Composants concernés :** Alerts, Dispatch, Web, SignalR.

**Dépendances :** Sprint 11 E2E.

**Tests requis :** tenant/roles, composants, E2E traitement exception.

**Critères d’acceptation :** une alerte critique est assignée et résolue en ≤ 3 actions.

**Livrable démontrable :** poste opérateur quotidien sur données pilote.

**Risques et rollback :** surcharge du dashboard ; activation progressive.

**Estimation :** XL.

### Sprint 13 — Conducteur terrain et preuve réelle

**Objectif :** rendre Android exploitable dans une vraie tournée.

**Problèmes résolus :** caméra absente, action suivante floue, token non protégé.

**Tâches principales :** prochaine étape, navigation/contact, CameraX/photo picker, permissions, stockage de session Keystore, reprise upload, accessibilité terrain.

**Composants concernés :** Android, Media, Operations.

**Dépendances :** tests instrumentés Sprint 11.

**Tests requis :** appareil/émulateur, offline/retry/idempotence, permission refusée, session expirée.

**Critères d’acceptation :** mission complète avec photo réelle malgré coupure réseau.

**Livrable démontrable :** tournée pilote enregistrée de bout en bout.

**Risques et rollback :** variations appareil ; fallback photo picker et file offline.

**Estimation :** XL.

### Sprint 14 — Pilote commercial mesuré

**Objectif :** onboarder trois organisations et décider sur des métriques réelles.

**Problèmes résolus :** valeur non mesurée, conformité et exploitation non prouvées.

**Tâches principales :** onboarding guidé, métriques activation/usage, stockage objet, baseline charge 30 véhicules, alerting opérationnel, DPA/rétention, offres et runbook support.

**Composants concernés :** tout l’écosystème, sans nouvelle grande verticale métier.

**Dépendances :** Sprints 10–13.

**Tests requis :** charge, sécurité, recovery périodique, trois scénarios tenant.

**Critères d’acceptation :** trois pilotes onboardés, deux actifs, un payant ou lettre d’intention.

**Livrable démontrable :** revue pilote avec métriques et décision go/simplify/pivot.

**Risques et rollback :** support excessif ; limiter cohortes et intégrations.

**Estimation :** XL.

### Addendum D-012 — vingt sprints de valeur après le Sprint 10

L’extension ne remet pas en cause le verdict 66/100 ni l’ordre des risques : les cinq premiers sprints restants ferment d’abord SQL/recovery/E2E, sessions, opérations, terrain et onboarding. Les extensions maintenance, télématique et commercialisation ne passent qu’après des gates mesurées.

| Sprint | Décision actuelle | Lien principal avec l’audit |
|---|---|---|
| 11 | SQL, E2E et recovery prouvés | AUD-005 à AUD-007, P0 données |
| 12 | Sessions, autorisations et données sensibles | AUD-008, sécurité et uploads |
| 13 | Centre d’opérations actionnable | AUD-009, recherche et vues denses |
| 14 | Conducteur terrain et preuve réelle | AUD-013, offline/caméra |
| 15 | Onboarding tenant, import et activation | Must onboarding, funnel absent |
| 16 | Stockage objet et médias de confiance | AUD-010, résilience des preuves |
| 17 | Ordres de maintenance et coûts | recommandation maintenance légère |
| 18 | Conformité et campagnes d’inspection | échéances et prévention |
| 19 | Dispatch productif et modèles | densité et temps de préparation |
| 20 | Pilote alpha et décision de niche | valeur, adoption et prix non mesurés |
| 21 | Qualité tracking, trajets et zones | confiance télématique et charge |
| 22 | Cadre de connecteurs et premier fournisseur | AUD-011, dépendance GPS |
| 23 | Statut destinataire contrôlé | réduction des appels, Could prudent |
| 24 | Rapports opérationnels | preuve du ROI et décision quotidienne |
| 25 | Hub d’intégrations fiable | API/webhooks/outbox exploitables |
| 26 | Administration appareils et support | coût support et variabilité terrain |
| 27 | Cycle de vie et performance | pagination, rétention et charge |
| 28 | Design system et accessibilité | dette UX Web/Android |
| 29 | Assurance Production | observabilité, sécurité dynamique, recovery |
| 30 | Bêta commerciale et décision générale | rétention, paiement et soutenabilité |

Les gates des Sprints 15, 20, 25 et 30 permettent de stopper ou simplifier une vague. WMS, paie, iOS, IA/RAG, optimisation propriétaire, portail client complet, microservices et matériel propriétaire restent exclus.

## 21. Verdict final

Il faut continuer le projet et concentrer les extensions sur le flux mission–preuve–exception. Le Sprint 10 a depuis corrigé secrets faibles, comptes de démonstration Production et protection du login ; la recovery SQL réelle reste à prouver au Sprint 11. Il ne faut ni pivoter sans données pilote ni confondre une roadmap détaillée avec des capacités livrées.

Trois prochaines actions après l’addendum :

1. exécuter Sprint 11 sur SQL Server/Docker avec restauration et E2E ;
2. protéger les sessions et autorisations au Sprint 12 ;
3. valider opérateur, conducteur et onboarding aux Sprints 13–15 avant la vague suivante.

### Tableau de synthèse

| Domaine | Note /10 | État | Problème principal | Action prioritaire |
|---|---:|---|---|---|
| Vision produit | 8 | claire | niche encore large | cibler livraison/service local |
| Valeur utilisateur | 7 | plausible | non mesurée | métriques pilote |
| Fonctionnalités | 8 | cœur présent | caméra/onboarding/exceptions | Sprints 13–15 |
| Frontend | 7 | fonctionnel | densité/responsive | socle UX |
| Backend | 8 | solide | endpoints longs/config | durcir et extraire ciblé |
| Données | 7 | modèle/index/migrations | pas de SQL test | Testcontainers |
| Architecture | 8 | adaptée | preuves infra | conserver monolithe |
| Qualité du code | 7 | cohérente | gros fichiers | composants/services ciblés |
| Tests | 6 | bonne quantité | InMemory, aucun E2E | Sprint 11 |
| Sécurité | 4 à l’audit | socle durci au Sprint 10 | sessions/uploads à durcir | Sprint 12 |
| DevOps | 6 | packaging présent | runtime/recovery non prouvés | exercice réel |
| UX/UI | 6 | cohérente | peu actionnable | refonte incrémentale |
| Documentation | 7 | riche | états contradictoires | vérité documentaire |
| Scalabilité | 7 | suffisante MVP | listes/worker/stockage | baselines ciblées |
| Potentiel commercial | 6 | réel mais concurrentiel | différenciation/prix non validés | pilotes payants |
| Préparation à la production | 4 à l’audit | stabilisation engagée | SQL/recovery/E2E | Sprint 11 |

### Note globale

**66/100.** La couverture fonctionnelle et l’architecture tirent la note vers le haut ; la sécurité Production, l’absence de SQL/E2E et la recovery non prouvée empêchent un score de produit commercial.

### Niveau de confiance

**Élevé** pour le code, les builds et les défauts statiques ; **moyen** pour exploitation/charge/commercial car Docker, restauration réelle et clients ne sont pas disponibles.

### Trois forces principales

1. flux métier large déjà exécutable avec isolation tenant testée ;
2. architecture simple et adaptée à une petite équipe ;
3. discipline de build et tests locaux déjà substantielle.

### Trois faiblesses principales

1. configuration Production dangereuse ;
2. preuves SQL/E2E/recovery absentes ;
3. UX encore démonstrative plutôt que quotidienne.

### Trois actions prioritaires

1. Sprint 11 preuves d’infrastructure ;
2. Sprint 12 sessions, autorisations et données sensibles ;
3. Sprints 13–15 expérience opérateur, conducteur et onboarding.

### Décision recommandée

**Poursuivre après stabilisation.** Le projet mérite d’être transformé en produit commercial, mais uniquement après fermeture des risques critiques et validation d’un pilote payant sur le noyau mission–preuve–exception.
