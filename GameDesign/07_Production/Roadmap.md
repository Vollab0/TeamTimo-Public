---
type: roadmap
tags: [production]
updated: 2026-11-08
---
# Roadmap

> [!question] Proposition de découpage — dates et contenu à valider par l'équipe.
> Le détail des tâches est dans le [[Backlog]] (sections 1 à 8, issues du brainstorming).

## Jalon 0 — Prototype technique ✅ (en grande partie fait)
- [x] Déplacement 3e personne, saut, sprint — [[Deplacement_Joueur]]
- [x] Labyrinthe procédural en 4 zones + NavMesh par zone — [[Generation_Labyrinthe]]
- [x] Taureau (charge + stun) et araignée (vision / poursuite) — [[Taureau]], [[Araignee]]
- [x] Santé en cœurs, ragdoll — [[Sante_et_Degats]]
- [x] Inventaire + ramassage — [[Inventaire]], [[Ramassage_Objets]]
- [x] Cycle jour / nuit visuel — [[Cycle_Jour_Nuit]]

## Jalon 0.5 — Décisions structurantes *(avant de coder la suite)*
- [x] Caméra → 3e personne (2026-11-08)
- [x] Multijoueur : 1-4 joueurs en ligne (2026-11-08) — [[Multijoueur]]
- [ ] Technologie réseau → ADR — [[Multijoueur]]
- [x] Durée et ratio du jour → 18 min, 70/30 (2026-11-08)
- [x] Les 4 gardiens → Rat, Coq, Oie, Truie ; thèmes de zone abandonnés (2026-11-08)

## Jalon 1 — Vertical slice *(date : ?)*
Objectif : **une boucle complète sur une seule zone**.
- [ ] Cycle de partie (jours, game over, victoire) — [[Cycle_de_Partie]]
- [ ] Jour/nuit avec mobs de nuit — [[Cycle_Jour_Nuit]]
- [ ] Lobby minimal : 2-3 PNJ (Médecin, Marchand, Forgeron) — [[Lobby]], [[PNJ]]
- [ ] Économie de base (or des mobs et coffres) — [[Economie]]
- [ ] 1 gardien + 1 fragment + porte de sortie — [[Gardiens]], [[Fragments_et_Cle]]
- [ ] Combat (hitbox, Pelle) — [[Combat_Attaque]]
- [ ] Mort / réanimation — [[Sante_et_Degats]]

## Jalon 2 — Contenu complet *(date : ?)*
- [ ] 4 zones (ambiance + gardien), mobs mélangés — [[Labyrinthe]]
- [ ] 5 mobs + variantes de nuit — [[Bestiaire]]
- [ ] 4 gardiens
- [ ] 6 PNJ + Pharmacien + figurants + enlèvements — [[Enlevement_des_PNJ]]
- [ ] 23 objets (21 du brainstorming + Plume + Herbes) + 3 remèdes du Pharmacien — [[Catalogue_Objets]]
- [ ] Carte / minimap — [[Carte_et_Navigation]]
- [ ] Multijoueur

## Jalon 3 — Polish & release *(date : ?)*
- [ ] Playtests du cycle complet jour 1 → 10 (`Playtests/`)
- [ ] Équilibrage économie et difficulté
- [ ] Audio complet, menus, tutoriel diégétique
- [ ] Build finale
