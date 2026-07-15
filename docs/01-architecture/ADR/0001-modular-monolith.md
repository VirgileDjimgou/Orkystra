# ADR-0001 — Monolithe modulaire

## Statut

Accepté.

## Décision

Utiliser un monolithe modulaire ASP.NET Core avec modules logiques et une base SQL partagée.

## Raisons

- développeur unique ;
- débogage et déploiement simples ;
- transactions métier locales ;
- coûts faibles ;
- extraction future possible grâce aux frontières de modules.

## Conséquences

Des tests d'architecture doivent empêcher les dépendances cycliques et l'accès direct non autorisé entre modules.
