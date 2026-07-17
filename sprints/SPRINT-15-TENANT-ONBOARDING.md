# SPRINT-15 — Onboarding tenant, import et activation

## Objectif

Faire passer une petite flotte d’un espace vide à une première mission exécutée en moins d’une journée, sans intervention directe en base.

## Valeur et constats traités

Répond au besoin Must d’onboarding sécurisé et à l’absence de funnel d’activation mesuré. Il réduit le coût de mise en service et expose les erreurs de données avant le terrain.

## Tâches principales

- créer une checklist par rôle : organisation, administrateur, opérateurs, conducteurs, véhicules et appareils ;
- fournir assistant d’import CSV avec modèle, prévisualisation, validation, correction et idempotence ;
- guider invitation, activation MFA admin et appairage Android via code court à usage unique ;
- ajouter jeu d’essai optionnel isolé et suppression explicite avant passage réel ;
- vérifier préparation : affectations, documents, appareil, sync et mission test ;
- créer centre d’aide contextuel, tour court et diagnostics exportables sans données sensibles ;
- mesurer temps d’activation, abandons, erreurs et première valeur.

## Composants concernés

Identity, Fleet, Imports, Web Admin, Android, analytics produit et documentation support.

## Dépendances

Sprints 11–14 terminés ; aucune donnée de démonstration implicite en Production.

## Tests et preuves requis

Import volumique/partiel/idempotent, invitations expirées, appairage tenant, rôles, reprise d’assistant, accessibilité et E2E espace vide→mission terminée.

## Critères d’acceptation

- [ ] une organisation neuve atteint une première mission test sans accès base ;
- [ ] import invalide n’écrit rien avant confirmation et fournit des erreurs ligne par ligne ;
- [ ] relancer un import confirmé ne crée aucun doublon ;
- [ ] code d’appairage est court, expirant, mono-usage et tenant-safe ;
- [ ] au moins 80 % des étapes sont réalisables sans support synchrone lors d’un test utilisateur ;
- [ ] activation, abandon et première valeur sont mesurés sans donnée personnelle superflue.

## Livrable démontrable

Un nouvel administrateur importe dix véhicules/conducteurs, invite un opérateur, appaire Android et clôture une mission test avec preuve dans la même session d’onboarding.

## Gate de décision

Ne pas ouvrir la vague suivante tant que SQL/recovery, sessions, parcours Web/Android et onboarding ne forment pas un environnement pilote récupérable. Documenter `GO`, `FIX` ou `STOP`.

## Risques et rollback

Un assistant trop rigide peut bloquer les cas réels ; permettre sauvegarde/reprise et imports corrigibles, sans contourner validations, tenant ou audit.

## Estimation

L.
