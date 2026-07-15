# Sécurité et confidentialité

## Baseline

- authentification standard, aucun protocole maison ;
- mots de passe hachés par bibliothèque éprouvée ;
- access tokens courts et refresh tokens rotatifs si JWT ;
- MFA administrateur avant pilote commercial ;
- contrôle d'accès serveur systématique ;
- chiffrement TLS ;
- secrets hors Git ;
- validation stricte des fichiers ;
- limitation de débit sur auth et télémétrie ;
- audit des actions sensibles.

## Données sensibles

Positions, identité conducteur, photos et signatures nécessitent : finalité, durée de conservation, accès limité, export et suppression contrôlée.

## Menaces prioritaires

- fuite inter-tenant ;
- usurpation d'appareil GPS ;
- rejeu de télémétrie ;
- URL de fichier publique ;
- escalade de rôle ;
- injection CSV ;
- webhook forgé ;
- logs contenant tokens ou positions complètes.
