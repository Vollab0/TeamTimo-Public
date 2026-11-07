---
type: mecanique
status: implemented
priority: high
owner:
scripts:
  - Assets/Scripts/MazeGenerator.cs
  - Assets/Scripts/MazeCell.cs
  - Assets/Scripts/MazeNavMeshBuilder.cs
  - Assets/Scripts/TerrainGenerator.cs
tags: [gdd, mecanique, niveau, procedural]
updated: 2026-11-08
---
# Génération procédurale du labyrinthe

## 1. Intention de design
L'environnement n'est pas un bloc uniforme mais **4 petits labyrinthes interconnectés** (zones A, B, C, D) autour du [[Lobby]] central.
- **4 zones, 4 ambiances :** chaque zone a une **thématique sensorielle** qui dicte le gameplay (ennemis aveugles, sourds…) — [[Discretion_et_Aggro]] — et des **variantes de kit modulaire** (murs, sol, plafond, portes).
- **Salle des gardiens** au fond de chaque zone : arène **non procédurale**, conçue pour favoriser les stratégies de raid — [[Gardiens]].
- **Placement aléatoire** des **coffres** (loot) et des **spawners** de monstres — [[Economie]], [[Apparition_Ennemis]].
- **Porte de sortie finale** à localiser, **placée à un endroit différent à chaque partie** (décidé le 2026-11-08) — [[Fragments_et_Cle]].
- **Zones en ordre libre** : toutes accessibles dès le jour 1 (décidé le 2026-11-08) ; la difficulté monte avec chaque fragment récupéré — [[Cycle_de_Partie]].
- Les **anciens bataillons** ont laissé des indices dans le labyrinthe — [[Univers]].
- Algorithme envisagé dans les tâches : **tile-based ou Wave Function Collapse** (le prototype utilise un parcours en profondeur sur grille).

## 2. Règles et comportement (état actuel du code)
Ordre de génération (`MazeGenerator`, coroutine au `Start`) :
1. Grille de `_mazeWidth × _mazeDepth` cellules, tous les murs fermés.
2. Placement d'un **Lobby** central + **une salle aléatoire par zone** (marge de 2 cases entre salles).
3. Creusage des salles (murs internes retirés).
4. **Piliers** aux intersections, hors salles.
5. Le terrain est découpé en **4 zones** (quadrants SW, NW, NE, SE) ; un labyrinthe indépendant est creusé dans chacune (parcours en profondeur).
6. **Connexions** : portes Lobby ↔ zones, portes des salles, passages directs entre zones adjacentes.
7. **Sortie** : un mur extérieur aléatoire est ouvert.
8. Centrage du labyrinthe sur l'origine.
9. `MazeNavMeshBuilder` pose des `NavMeshModifier` par zone / salle / lobby + une zone `WallTop` (haut des murs), bake le NavMesh puis lance [[Apparition_Ennemis]].

`TerrainGenerator` génère en parallèle des tuiles de terrain procédurales (bruit, textures, végétation) autour du labyrinthe, fusionnées avec le Lobby.

### Zones NavMesh (Navigation > Areas)
`SouthWest`, `NorthWest`, `NorthEast`, `SouthEast`, `SouthWestRoom`, `NorthWestRoom`, `NorthEastRoom`, `SouthEastRoom`, `Lobby`, `WallTop`, `Room`.
Ces noms sont utilisés par le spawner : **ne pas les renommer** sans mettre à jour le code.

## 3. Paramètres de tuning (Inspector — `MazeGenerator`)
| Paramètre | Type | Défaut | Rôle |
|---|---|---|---|
| `_mazeWidth` | int | 30 | Largeur en cellules |
| `_mazeDepth` | int | 30 | Profondeur en cellules |
| `_cellSize` | int | 50 | Taille d'une cellule (m) |
| `_lobbyWidth` / `_lobbyDepth` | int | 6 / 6 | Taille du lobby (cellules) |
| `_roomWidth` / `_roomDepth` | int | 4 / 4 | Taille des salles |
| `_interZoneDoors` | int | 2 | Passages directs entre zones |
| `_doorsPerLobbySide` | int | 1 | Portes du lobby par zone |
| `_doorsPerRoom` | int | 1 | Portes par salle |
| `_slowMotion` / `_stepDelay` | bool / float | false / 0.01 | Visualisation pas à pas (debug) |

> [!note] Échelle
> Avec les valeurs par défaut, le labyrinthe fait 30 × 50 = **1 500 m** de côté.

## 5. Liens
- Déclenche : [[Apparition_Ennemis]]
- Niveau : [[Labyrinthe]]
- Ennemis adaptés aux couloirs : [[Taureau]]

## 6. Implémentation technique
- **Scripts :** `MazeGenerator.cs`, `MazeCell.cs`, `MazeNavMeshBuilder.cs`, `TerrainGenerator.cs`
- **Prefabs :** cellule (`MazeCell`) + pilier, assignés dans l'Inspector
- **Scène :** `Assets/Scenes/Labyrinthe.unity`

## 7. Questions ouvertes
- [ ] Graine (seed) partagée pour le multijoueur / le debug ?
- [ ] Correspondance zones A/B/C/D ↔ quadrants SW/NW/NE/SE ?
- [ ] Les salles aléatoires actuelles (1 par zone) deviennent-elles les salles des gardiens, placées « au fond » de la zone ?
- [ ] Garde-t-on le parcours en profondeur ou passe-t-on au tile-based / WFC ?
- [ ] Échelle : 1 500 m de côté est-il cohérent avec ~13 min de jour et ~5 min de nuit (journée de 18 min) ?
- [ ] À faire dans le code : coffres, porte de sortie verrouillée, arènes de boss, variantes visuelles par zone.
