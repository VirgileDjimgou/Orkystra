# SPRINT-14 — Conducteur terrain et preuve réelle

## Objectif

Permettre au conducteur d’exécuter une tournée complète d’une main, avec photo/signature réelles et réseau intermittent.

## Valeur et constats traités

Traite AUD-013, la prochaine action peu visible, la reprise d’upload et le manque d’instrumentation appareil. Le conducteur doit toujours comprendre quoi faire, ce qui est enregistré localement et ce qui reste à synchroniser.

## Tâches principales

- concevoir l’accueil « prochaine étape » avec progression, priorité, consignes et état de sync global ;
- ajouter navigation externe, contact masqué selon rôle et confirmation d’arrivée ;
- intégrer CameraX et photo picker fallback, permissions progressives et compression contrôlée ;
- capturer signature/nom/preuve avec métadonnées minimales, consentement et aperçu ;
- reprendre uploads segmentés, actions offline et synchronisation après mort du processus ;
- gérer expiration de session, conflit métier et commande rejetée sans perdre la saisie ;
- améliorer contraste, tailles tactiles, lecteur d’écran, mode sombre et messages utilisables au soleil.

## Composants concernés

Android Driver, Operations, Inspections, Media, API et tests instrumentés.

## Dépendances

Sprints 11–12 pour instrumentation, sessions protégées et uploads sûrs ; contrats d’exception stables du Sprint 13.

## Tests et preuves requis

Tests sur émulateur et au moins un appareil, offline/retry/idempotence, permissions refusées, stockage saturé, upload interrompu, expiration session, rotation/process death et accessibilité.

## Critères d’acceptation

- [ ] photo réelle et signature autorisée rejoignent une preuve privée consultable ;
- [ ] aucune commande ou preuve n’est dupliquée après répétition/réseau intermittent ;
- [ ] la prochaine action et la file en attente sont compréhensibles sans formation longue ;
- [ ] refus de permission et stockage insuffisant ont un chemin de récupération ;
- [ ] la reprise après mort du processus est démontrée ;
- [ ] une tournée complète fonctionne avec une coupure réseau contrôlée.

## Livrable démontrable

Une mission multi-arrêts est exécutée sur appareil réel, passe hors ligne, capture inspection et POD, puis se réconcilie avec le centre opérateur.

## Observabilité, sécurité et rollback

Mesurer réussite sync, âge de la file, échecs upload et temps par étape sans journaliser photo, signature ou position précise. CameraX dispose du picker fallback et la file offline existante reste récupérable.

## Estimation

XL.
