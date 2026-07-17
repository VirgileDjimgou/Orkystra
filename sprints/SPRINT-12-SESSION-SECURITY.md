# SPRINT-12 — Sessions, autorisations et données sensibles

## Objectif

Réduire le risque de vol de session et fermer les ambiguïtés d’autorisation avant l’entrée de données pilote réelles.

## Valeur et constats traités

Traite AUD-008, la surface auth/admin non explicitement versionnée, les uploads non analysés et le risque de requête tenant oubliée. Le résultat attendu est une authentification utilisable sans conserver de jeton exploitable en clair dans les stockages applicatifs.

## Tâches principales

- migrer le Web vers un modèle de session protégé (cookie HttpOnly/BFF ou équivalent documenté) avec CSRF ;
- stocker la session Android via Keystore/EncryptedSharedPreferences et migrer sans déconnexion destructive ;
- définir rotation, révocation, expiration, déconnexion globale et récupération après session expirée ;
- déclarer les routes auth/admin internes ou les versionner, puis déprécier les alias historiques ;
- ajouter une matrice centralisée rôles × opérations et des tests d’architecture tenant ;
- valider type, taille, signature et contenu des uploads avec quarantaine/scan configurable ;
- renforcer CSP, en-têtes de sécurité, redaction des logs et audit des opérations sensibles.

## Composants concernés

API/Auth, Infrastructure, Web, Android, Media, tests sécurité et documentation.

## Dépendances

Suite SQL/E2E du Sprint 11 et stratégie de compatibilité des sessions existantes.

## Tests et preuves requis

CSRF/XSS de session, rotation/révocation, upgrade Android, expiration offline, matrice d’autorisations, fuzz léger des uploads, tenant négatif sur chaque rôle et scan de dépendances.

## Critères d’acceptation

- [ ] aucun token long terme n’est lisible depuis `localStorage` ou une table Room ;
- [ ] révocation et déconnexion globale prennent effet dans le délai documenté ;
- [ ] les routes sensibles ont version, statut interne et politique d’autorisation explicites ;
- [ ] fichiers invalides ou suspects sont refusés/quarantinés avant accès ;
- [ ] la suite négative tenant/rôle couvre toutes les opérations sensibles ;
- [ ] les logs et artefacts E2E ne contiennent ni secret, ni token, ni position complète.

## Livrable démontrable

Un administrateur révoque une session ; le Web et Android demandent une reconnexion sûre, tandis qu’un upload malformé et une requête inter-tenant sont bloqués et audités.

## Observabilité, sécurité et rollback

Mesurer sessions révoquées, échecs CSRF et quarantaine sans exposer les charges utiles. Conserver une fenêtre de compatibilité courte et réversible pour la migration, jamais un fallback permanent vers le stockage faible.

## Estimation

XL.
