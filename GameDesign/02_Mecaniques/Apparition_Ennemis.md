---
type: mecanique
status: implemented
priority: medium
owner:
scripts:
  - Assets/Scripts/SpawnerManager.cs
tags: [gdd, mecanique, ennemi, procedural]
updated: 2026-11-08
---
# Apparition des ennemis

## 1. Intention de design
D'après le brainstorming :
- **Spawners placés aléatoirement** dans le labyrinthe, avec des mobs **mélangés dans toutes les zones** (plus de thème par zone, décidé le 2026-11-08) — [[Bestiaire]].
- **La nuit**, les mobs passent en variante de nuit (plus rapides, plus forts, aggro plus large) — [[Cycle_Jour_Nuit]].
- **Escalade :** les mobs ne changent pas avec les fragments ; seule menace ajoutée, le **gardien dont on a pris le fragment rôde dans tout le labyrinthe** (décidé le 2026-11-08) — [[Fragments_et_Cle]].
- Le **Loup** appelle des renforts pour être en supériorité numérique — [[Loup]].
- Les **Aigles** se placent sur le haut des murs (zone NavMesh `WallTop` déjà prévue), dans **toutes les zones**, mais en **faible nombre** (mob rare) — [[Aigle]].

> [!question] Densité par zone, réapparition des mobs tués ?

## 2. Règles et comportement (état actuel du code)
- Lancé par `MazeNavMeshBuilder` **après** le bake du NavMesh (voir [[Generation_Labyrinthe]]).
- La liste `waves` (configurée dans l'Inspector) contient des entrées : nom, **zone NavMesh**, prefab, nombre.
- Malgré le nom « wave », **toutes les entrées apparaissent en même temps** au démarrage.
- **Taureau** (détecté si le nom du prefab contient `Bull`) : on tire 20 points et on garde le couloir le plus large (≥ 6 m), à au moins 8 m d'un autre taureau.
- **Autres ennemis** : point aléatoire sur le NavMesh de la zone.
- L'agent de chaque ennemi est restreint à sa zone (`areaMask`). ⚠️ À revoir : les **gardiens errants** doivent pouvoir parcourir **tout** le labyrinthe (sauf le Lobby), et la **Taupe** passer sous les murs.
- API : `AddWave(zone, prefab, nombre, nom)`, `ClearAllEnemies()`.

## 3. Paramètres de tuning
| Paramètre | Type | Défaut | Rôle |
|---|---|---|---|
| `waves` | liste | — | Ennemis à faire apparaître |
| `searchRadius` | float | 10 | Rayon de recherche du sol |
| `obstacleLayer` | LayerMask | — | Murs + piliers |
| largeur min. couloir (taureau) | float | 6 | **Codé en dur** |
| distance min. entre taureaux | float | 8 | **Codé en dur** |

## 5. Liens
- Ennemis : [[Bestiaire]] ([[Taureau]], [[Araignee]], [[Loup]], [[Taupe]], [[Aigle]])
- Dépend de : [[Generation_Labyrinthe]]

## 6. Implémentation technique
- **Script :** `SpawnerManager.cs`
