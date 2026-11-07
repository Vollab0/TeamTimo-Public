---
type: roadmap
tags: [production]
updated: 2026-11-07
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
- [ ] Caméra (TPS vs Top-Down / iso)
- [ ] Multijoueur : nombre de joueurs, réseau → ADR — [[Multijoueur]]
- [ ] Durée et ratio du jour
- [ ] Les 4 gardiens et les 4 thèmes de zone

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
- [ ] 4 zones avec thèmes sensoriels — [[Discretion_et_Aggro]]
- [ ] 5 mobs + variantes de nuit — [[Bestiaire]]
- [ ] 4 gardiens
- [ ] 6 PNJ + enlèvements — [[Enlevement_des_PNJ]]
- [ ] 21 objets — [[Catalogue_Objets]]
- [ ] Carte / minimap — [[Carte_et_Navigation]]
- [ ] Multijoueur

## Jalon 3 — Polish & release *(date : ?)*
- [ ] Playtests du cycle complet jour 1 → 10 (`Playtests/`)
- [ ] Équilibrage économie et difficulté
- [ ] Audio complet, menus, tutoriel diégétique
- [ ] Build finale
