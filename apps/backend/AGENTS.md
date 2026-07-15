# Instructions backend

Appliquer `AGENTS.md` et `docs/02-engineering/ENGINEERING_STANDARDS.md`. Les modules métier ne dépendent pas de l'API. L'infrastructure implémente les ports définis dans Core. Toute donnée tenant-aware porte `OrganizationId`. Les endpoints utilisent Problem Details, autorisation serveur, cancellation et tests d'intégration.
