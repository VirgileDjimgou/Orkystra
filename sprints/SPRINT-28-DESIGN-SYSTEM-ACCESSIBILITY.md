# SPRINT-28 — Design system, accessibilité et espaces de travail

## Objectif

Rendre toutes les surfaces cohérentes, rapides à apprendre et accessibles, tout en laissant Admin, Operator et Driver voir d’abord leurs tâches essentielles.

## Valeur et constats traités

Finalise la dette UX relevée dans l’audit : grands composants, responsive limité, focus/live regions incomplets et Android basique. Le résultat est un langage commun Web/Android, pas une réécriture visuelle.

## Tâches principales

- formaliser tokens couleur/espacement/typographie/états et composants Web documentés ;
- aligner les équivalents Compose sans forcer un rendu identique entre plateformes ;
- extraire formulaires, tableaux, filtres, timeline, empty/error/loading et confirmations ;
- atteindre navigation clavier, focus, annonces, contraste et réduction d’animation sur flux P0 ;
- proposer espaces par rôle, raccourcis et préférences de vue enregistrées côté serveur ;
- tester responsive mobile/tablette/desktop, zoom 200 % et données longues/localisées ;
- ajouter tests visuels stables et budget de performance frontend.

## Composants concernés

Web Vue, Android Compose, documentation UI, tests visuels, accessibilité et analytics UX.

## Dépendances

Flux métier stabilisés Sprints 13–26 et retours pilotes.

## Tests et preuves requis

Story/component tests, axe ou équivalent, clavier, TalkBack, contrastes, captures multi-viewport, performance, rôles et tests utilisateurs ciblés.

## Critères d’acceptation

- [ ] flux login, exception, dispatch, preuve et support sont utilisables au clavier ;
- [ ] aucun défaut critique WCAG 2.2 AA automatisable sur ces flux ;
- [ ] Android expose labels, ordre de lecture et tailles tactiles adaptés ;
- [ ] états loading/empty/error/success sont cohérents et actionnables ;
- [ ] préférences ne peuvent ni élever un rôle ni exposer un autre tenant ;
- [ ] nouveaux composants réduisent réellement la duplication des grandes vues.

## Livrable démontrable

Admin, Operator et Driver accomplissent chacun leur tâche principale avec leur technologie d’assistance et sur deux formats d’écran, avec snapshots de référence.

## Risques et rollback

Risque de refonte cosmétique coûteuse. Migrer flux par flux, conserver contrats métier et mesurer erreurs/temps ; chaque composant reste remplaçable indépendamment.

## Estimation

L.
