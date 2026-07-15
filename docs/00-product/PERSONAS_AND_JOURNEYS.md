# Personas et parcours

## Administrateur

Configure l'organisation, les utilisateurs, les rôles, les véhicules, les appareils, les politiques et les intégrations.

## Opérateur

Observe la carte, crée et affecte les missions, suit les retards, traite les exceptions et vérifie les preuves.

## Conducteur

Consulte ses missions, démarre/termine les étapes, réalise une inspection, signale un défaut et collecte une preuve, même avec un réseau instable.

## Parcours principal

```text
Admin crée la flotte
  → simulateur/appareil émet une position
  → opérateur voit le véhicule
  → opérateur crée et affecte une mission
  → conducteur reçoit la mission
  → conducteur exécute les étapes
  → position et statuts remontent
  → conducteur collecte photo/signature
  → opérateur clôture le dossier
```

## Principes UX

- une action principale par écran ;
- états et erreurs explicites ;
- pas de jargon technique dans les interfaces métier ;
- clavier et accessibilité Web ;
- boutons conducteur utilisables avec une main ;
- synchronisation hors ligne visible et compréhensible.
