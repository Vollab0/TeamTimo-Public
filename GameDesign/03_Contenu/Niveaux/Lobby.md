---
type: niveau
status: draft
owner:
scene:
tags: [gdd, contenu, niveau]
updated: 2026-11-07
---
# Le Lobby — le village en sursis

![[Lobby_Vue.jpg]]

## Objectif du joueur
Zone de **sécurité absolue** au centre de la carte : se préparer, dépenser son or, se soigner, préparer l'expédition suivante.

## Contenu
- **Grange** : point d'apparition des joueurs (équivalent du vaisseau de *Lethal Company* / de la maison de *Content Warning*)

  ![[Lobby_Grange.jpg|260]]
- **Stands des PNJ** : forge, tente du médecin, étal du marchand, table du cartographe, table de l'arnaqueur, banque — [[PNJ]].
  - Quand un PNJ est enlevé, **son stand ferme** — [[Enlevement_des_PNJ]].
- **Coffre du Lobby** (améliorable) — [[Economie]].
- **Tutoriel diégétique** : les mécaniques sont expliquées par des **panneaux et fresques** dans le village.
- **Portes** vers les 4 zones du labyrinthe.

## Direction artistique (concept)
Village cartoon entouré de hauts murs de pierre percés de portes : chemins de terre, arbres, maisons aux toits colorés, feu de camp central, étals, enclos — [[Direction_Artistique]].

![[Lobby_Batiments.jpg|300]] ![[Lobby_Decor.jpg|300]] ![[Lobby_Etals.jpg|180]] ![[Lobby_Bois_Panneau.jpg|90]]

## Implémentation technique
- **Scène assemblée à la main** (liste de tâches : « Assembler la scène du Lobby, placer les PNJ de manière logique »).
- Aujourd'hui, le lobby est une zone vide de 6 × 6 cellules générée par `MazeGenerator` (zone NavMesh `Lobby`) entourée du terrain de `TerrainGenerator` — [[Generation_Labyrinthe]].

## Questions ouvertes
- [ ] Les monstres peuvent-ils entrer dans le Lobby (gardien réveillé) ?
- [ ] Le Lobby est-il dans la même scène que le labyrinthe ?
