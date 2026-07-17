# SPRINT-16 — Stockage objet et médias de confiance

## Objectif

Remplacer la divergence entre stockage média local et infrastructure objet par une chaîne de preuve privée, résiliente et exploitable.

## État

`DONE` — quality gate complète verte le vendredi 17 juillet 2026 ; Sprint 17 sélectionné sans implémentation.

## Valeur et constats traités

Traite AUD-010 et sécurise les photos/signatures produites au Sprint 14. Une preuve doit rester accessible selon sa rétention, survivre au redéploiement et ne jamais devenir publique par défaut.

## Tâches principales

- implémenter l’adaptateur S3 compatible MinIO avec configuration Production fail-fast ;
- organiser les objets par tenant et identifiants opaques, chiffrement, métadonnées minimales et checksum ;
- conserver uploads multipart/reprise, idempotence et finalisation atomique ;
- intégrer quarantaine/scan, types autorisés, limites et rejet traçable ;
- gérer URLs signées courtes, autorisation à la lecture et révocation logique ;
- définir rétention, suppression différée, orphan cleanup et export tenant ;
- migrer les médias filesystem existants via commande rejouable et rapport.

## Composants concernés

Infrastructure Storage, Media API, Worker, compose, scripts, observabilité et tests.

## Dépendances

Sprints 12 et 14 pour sécurité upload et preuves réelles.

## Tests et preuves requis

Contrat filesystem/S3, reprise multipart, checksum, isolation tenant, expiration URL, panne objet, migration idempotente, purge/export et recovery des métadonnées SQL.

## Critères d’acceptation

- [x] aucune preuve Production ne dépend du disque éphémère de l’API ;
- [x] lecture d’un objet exige autorisation tenant même avec un identifiant connu ;
- [x] reprise d’upload ne corrompt ni ne duplique l’objet ;
- [x] quarantaine interdit l’accès avant validation ;
- [x] migration existante est rejouable avec rapport succès/erreur ;
- [x] rétention, suppression et restauration sont documentées et testées.

## Livrable démontrable

Une photo Android interrompue reprend, passe le contrôle, est consultée par l’opérateur via URL courte puis devient inaccessible après révocation/rétention.

## Risques et rollback

Risque de migration partielle et coût de stockage. Conserver l’adaptateur filesystem pour développement, écrire en S3 avant bascule de lecture et ne supprimer l’ancien média qu’après checksum.

## Estimation

L.
