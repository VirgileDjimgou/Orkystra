# Stratégie de simulation

## Objectif

Tester l'écosystème sans clients, véhicules ni appareils physiques.

## Simulateur GPS

- plusieurs appareils concurrents ;
- routes configurables ;
- vitesse, direction et précision ;
- pause, perte réseau et rattrapage ;
- points dupliqués ou désordonnés ;
- batterie faible et ignition simulées ;
- HTTP au début, MQTT en option.

## Scénarios

1. flotte normale de trois véhicules ;
2. véhicule immobile inattendu ;
3. appareil hors ligne puis reprise ;
4. mission avec retard ;
5. conducteur hors ligne ;
6. inspection avec défaut critique ;
7. preuve de livraison synchronisée tardivement.

## Données

Toutes les données de démonstration sont fictives, déterministes et recréables à partir d'un seed.
