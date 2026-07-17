# SPRINT-26 — Administration des appareils et diagnostic support

## Objectif

Réduire le coût de support en donnant aux administrateurs et au support des diagnostics sûrs pour Android et appareils télématiques.

## Valeur et constats traités

Répond au risque de support d’une petite équipe et à la variabilité des appareils terrain. Le produit explique pourquoi un client ne synchronise plus sans exposer token, position précise ou contenu privé.

## Tâches principales

- inventorier installation Android, version, dernière sync, âge de file et état de permissions ;
- suivre appareil télématique, firmware déclaré, dernière communication et connecteur associé ;
- créer code de diagnostic exportable, redigé et expirant ;
- permettre révocation de session, désappairage et réappairage avec confirmation/audit ;
- ajouter compatibilité minimale, bannière de mise à jour et politique de version supportée ;
- fournir runbooks guidés pour offline, permission, horloge, stockage et credential expiré ;
- construire vue support cross-tenant uniquement pour rôle plateforme séparé et just-in-time, si validé.

## Composants concernés

Android, Devices, Integrations, Identity, Web Admin, audit et support docs.

## Dépendances

Sessions Sprint 12, terrain Sprint 14 et connecteurs Sprint 22.

## Tests et preuves requis

Redaction, rôles, tenant, révocation, appairage concurrent, version minimale, données obsolètes, export diagnostic et scénarios de résolution support.

## Critères d’acceptation

- [ ] administrateur identifie dernière sync et cause probable sans donnée sensible ;
- [ ] diagnostic exporté ne contient ni secret, ni token, ni position détaillée ;
- [ ] désappairage invalide immédiatement la relation active ;
- [ ] mise à niveau conserve file offline et session selon stratégie ;
- [ ] accès support plateforme est séparé, temporaire et audité s’il existe ;
- [ ] cinq incidents fréquents possèdent un chemin guidé testé.

## Livrable démontrable

Un conducteur ne synchronise plus ; l’administrateur identifie une permission, génère un diagnostic redigé et rétablit le service sans accès base.

## Risques et rollback

Risque de surveillance excessive et rôle support trop puissant. Minimiser les données, séparer les autorisations et désactiver toute vue plateforme jusqu’à validation sécurité.

## Estimation

L.
