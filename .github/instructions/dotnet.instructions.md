---
applyTo: "apps/backend/**/*.cs,apps/backend/**/*.csproj,simulators/**/*.cs,tests/backend/**/*.cs"
---

Utiliser C# moderne, nullable activé et avertissements en erreurs. Préférer les vertical slices, EF Core direct dans les handlers/services applicatifs et les types métier explicites. Utiliser `CancellationToken`, UTC/TimeProvider et Problem Details. Toute requête tenant-aware doit être filtrée côté serveur et testée contre une fuite inter-tenant.
