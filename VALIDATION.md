# Validation Sprint 00

Validation réellement rejouée le `2026-07-15` sur Windows.

## Validé

- `docker compose --env-file .env config`
- démarrage Docker de SQL Server, MinIO, Mosquitto et Mailpit
- build .NET `Debug` et `Release`
- tests backend `Debug` et `Release`
- `dotnet list FleetOps.slnx package --vulnerable`
- dry-run du simulateur GPS
- migration EF initiale créée et appliquée
- `npm ci`
- `npm run format:check`
- `npm run lint`
- `npm run test`
- `npm run build`
- wrapper Gradle Android versionné
- build Android `testDebugUnitTest assembleDebug`
- quality gate PowerShell complète
- endpoints `GET /health`, `GET /api/system/info` et OpenAPI en développement
- flux démo `simulateur GPS -> API -> SignalR -> carte`

## Partiellement validé

- worker .NET: démarrage et arrêt vérifiés, mais comportement métier encore minimal par conception Sprint 00

## Conclusion

Le Sprint 00 est terminé localement. La quality gate est verte sur Docker Compose config, backend, simulateur GPS, Web, health check API et Android.
