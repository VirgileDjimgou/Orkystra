# Handoff — Sprint 29 terminé

## État courant

Sprint 29 est `DONE` après une quality gate complète verte le 2026-09-27. Le prochain sprint éligible est Sprint 30, qui n'a pas commencé. Trois sprints restent : 30 à 32.

## Livraison Sprint 29

- environnement `Demo` explicite avec validation fail-fast distincte de Development et Production ;
- lancement public sans mot de passe, session serveur courte, cookies HttpOnly/SameSite, CSRF, rate limit et plafond concurrent ;
- unique tenant synthétique et identité Operator sans mot de passe, capacités publiques en lecture seule et refus des surfaces sensibles ;
- contrôles de scénario isolés par session, nettoyage borné, label `SIMULATED DEMO` persistant et reprise après expiration ;
- overlay Compose Demo et parcours navigateur public complet.

## Preuves

Le journal complet est `.runtime/sprint29-quality-gate.log`. La gate a validé format/analyse sur 307 fichiers, build sans avertissement, 170 tests rapides, 1 test MinIO, 3 tests SQL Server, GPS, simulation 33 étapes, Web 28 tests et 7 E2E, santé API et Android. Dix-huit tests ciblés couvrent runtime, cookies/CSRF, TTL, rate limit, refus Admin, lecture seule, concurrence et expiration.

## Reprise

`Start Next Sprint` doit exécuter le runner atomique et sélectionner uniquement Sprint 30.
