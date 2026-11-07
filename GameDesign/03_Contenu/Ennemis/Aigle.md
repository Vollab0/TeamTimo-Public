---
type: ennemi
status: draft
priority: medium
owner:
prefab:
scripts: []
tags: [gdd, contenu, ennemi]
updated: 2026-11-07
---
# Aigle

![[Mob_Aigle.jpg|220]]

## Rôle dans le jeu
Tireur **perché sur les murs** du labyrinthe : menace verticale qui rend le **Lance-pierre** indispensable.

## Comportement (design)
- Situé **au-dessus des murs**, peut être placé **n'importe où** dans le labyrinthe.
- Lorsqu'il détecte un joueur **en contrebas** (check de hauteur), il lui lance des **boules de salive** qui font des dégâts.
- Le **seul moyen de l'attaquer** est d'utiliser le **Lance-pierre** pour le faire tomber du mur.
- Animation spécifique : vol.

## Stats et tuning
| Paramètre | Valeur | Rôle |
|---|---|---|
| Portée de détection | ? | |
| Cadence de tir | ? | |
| Dégâts d'une salive | ? | |
| Coups de lance-pierre pour le faire tomber | ? | |

## Implémentation technique
- La zone NavMesh **`WallTop`** (haut des murs) existe déjà dans `MazeNavMeshBuilder` — utile pour faire patrouiller les aigles.
- Projectiles : à mutualiser avec le Lance-pierre — [[Combat_Attaque]].

## Questions ouvertes
- [ ] Une fois tombé, combat au sol ou mort directe ?
- [ ] Les boules de salive peuvent-elles être esquivées / bloquées par le bouclier ?
