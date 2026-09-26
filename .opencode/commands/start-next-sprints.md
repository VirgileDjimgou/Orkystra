---
description: Préparer une batch bornée de sprints atomiques FleetOps
agent: build
---

Demande un entier `N` entre 1 et 10 si absent. Lis @AGENTS.md et @docs/02-engineering/SPRINT_AUTOPILOT.md, puis exécute `scripts/sprint-batch-runner.ps1 -Count N -Owner opencode-batch`. N'implémente que le sprint sélectionné ici; chaque sprint suivant exige un contexte agent distinct et le runner atomique.
