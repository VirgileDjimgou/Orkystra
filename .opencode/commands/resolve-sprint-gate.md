---
description: Résoudre explicitement une gate humaine FleetOps
agent: build
---

Lis @.agent/HUMAN_REQUIRED.json puis demande/résume la décision humaine exacte. Après décision explicite, exécute `scripts/sprint-runner.ps1 -Action ResolveGate -Note "<decision>"`. Ne code rien dans cette commande.
