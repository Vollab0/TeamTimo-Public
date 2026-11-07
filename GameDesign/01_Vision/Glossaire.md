---
type: reference
tags: [gdd, vision]
updated: 2026-11-07
---
# Glossaire

Vocabulaire commun à l'équipe (et à Claude Code). Le code mélange français et anglais : ce tableau fait le lien.

## Termes de design
| Terme | Définition |
|---|---|
| **Le Fermier** | Scientifique qui retient les animaux-cobayes. Enlève des PNJ chaque matin — [[Univers]] |
| **La Moisson** | Les 10 jours de la partie ; après le 10e coucher de soleil, il ne reste plus personne — [[Cycle_de_Partie]] |
| **Lobby / Village** | Zone sûre au centre de la carte, avec les PNJ — [[Lobby]] |
| **Grange** | Bâtiment du Lobby où apparaissent les joueurs |
| **Zone (A, B, C, D)** | Un des 4 petits labyrinthes interconnectés, chacun avec son thème sensoriel — [[Labyrinthe]] |
| **Salle des Gardiens** | Arène non procédurale au fond d'une zone, gardée par un boss — [[Gardiens]] |
| **Gardien / Boss** | Boss d'une zone ; combat en « raid » — [[Gardiens]] |
| **Fragment de clé** | Pièce de la clé, gardée par un gardien ; c'est un **pilier** qui retient le boss — [[Fragments_et_Cle]] |
| **Clé Maîtresse** | Clé réassemblée à partir des 4 fragments, ouvre la sortie |
| **Malédiction du porteur** | Effets négatifs subis par celui qui porte un fragment (encombrement, à confirmer) |
| **PNJ** | Habitant du village offrant un service — [[PNJ]] |
| **Mob / Mob de nuit** | Monstre du labyrinthe ; sa variante de nuit est plus forte — [[Bestiaire]] |
| **Pièces d'or** | Monnaie (monstres, coffres) — [[Economie]] |
| **Aggro** | Cible actuelle d'un monstre — [[Discretion_et_Aggro]] |

## Correspondance design ↔ code
| Terme (design) | Dans le code | Définition |
|---|---|---|
| Lobby | `Lobby` (zone NavMesh), `GetLobby()` | Salle centrale du labyrinthe |
| Zone | `SouthWest`, `NorthWest`, `NorthEast`, `SouthEast` | Un des 4 quadrants (A/B/C/D : correspondance à fixer) |
| Salle | `Room`, `*Room` | Espace ouvert dans une zone (future salle des gardiens ?) |
| Cœur / demi-cœur | `nombreDeCoeurs`, `vieActuelle` | 1 cœur = 2 PV |
| Blesser / Soigner | `PlayerHealth.Blesser()` / `Soigner()` | Infliger / rendre des PV |
| Taureau | `Bull`, `BullAI` | Mob chargeur — [[Taureau]] |
| Araignée | `Spider`, `EnemySight` | Mob tisseur de toiles — [[Araignee]] |
| Objet (données) | `ItemData` (ScriptableObject) | Définition d'un objet |
| Objet (au sol) | `ItemWorld` | Instance ramassable dans la scène |
| Vague | `WaveSettings` | Groupe d'ennemis à faire apparaître — [[Apparition_Ennemis]] |
| Rail | `_isAxisX`, `_fixedAxisCoordinate` | Axe de couloir sur lequel le taureau est verrouillé |
