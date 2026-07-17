# SPRINT-17 — Ordres de maintenance, immobilisation et coûts

## Objectif

Transformer une alerte ou un défaut en intervention légère planifiée, chiffrée et clôturée, sans construire un logiciel d’atelier complet.

## Valeur et constats traités

Met en œuvre la recommandation « ordre maintenance léger + coûts ». La valeur est de réduire les oublis, rendre visible l’indisponibilité et relier dépenses simples à chaque véhicule.

## Tâches principales

- créer l’ordre de maintenance avec source, priorité, responsable, échéance et statut explicite ;
- convertir défaut d’inspection, seuil kilométrique ou alerte en ordre sans doublon ;
- gérer immobilisation, fenêtre prévue, remise en service et conflit d’affectation mission ;
- enregistrer fournisseur, main-d’œuvre, pièces en texte libre contrôlé, coût et devise tenant ;
- joindre devis/facture privée sans devenir une comptabilité ;
- afficher calendrier, backlog, véhicule indisponible et coût synthétique ;
- notifier retard/fin et auditer transitions, annulation et réouverture.

## Composants concernés

Maintenance, Fleet, Inspections, Dispatch, Alerts, Media, Web et API.

## Dépendances

Centre d’exceptions Sprint 13 et médias Sprint 16.

## Tests et preuves requis

Machine d’état, déduplication, concurrence, tenant/rôles, collision mission, coûts décimaux/devise, pièces jointes, filtres et E2E défaut→ordre→remise en service.

## Critères d’acceptation

- [ ] un défaut critique crée ou relie un seul ordre actionnable ;
- [ ] un véhicule immobilisé ne peut être affecté sans résolution explicite autorisée ;
- [ ] coût total est calculé de façon stable et exportable ;
- [ ] chaque transition possède auteur, date et raison ;
- [ ] ordre en retard apparaît dans le centre d’exceptions ;
- [ ] remise en service clôt le risque sans supprimer l’historique.

## Livrable démontrable

Une inspection bloque un véhicule, l’opérateur planifie une intervention, saisit coût et justificatif, puis remet le véhicule en service avec timeline complète.

## Risques et rollback

Risque d’élargissement vers stocks, achats et atelier. Exclure catalogue de pièces, bons de commande et facturation ; feature flag et migration additive permettent un rollback UI.

## Estimation

XL.
