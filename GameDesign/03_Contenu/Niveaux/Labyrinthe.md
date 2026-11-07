---
type: niveau
status: in-progress
owner:
scene: Assets/Scenes/Labyrinthe.unity
tags: [gdd, contenu, niveau]
updated: 2026-11-07
---
# Niveau — Labyrinthe

## Objectif du joueur
Partir du [[Lobby]] central, explorer les **4 zones** (A, B, C, D), récupérer or et objets, voler les **4 fragments de clé** dans les salles des gardiens, puis ouvrir la **porte de sortie** avant la fin du jour 10 — [[Cycle_de_Partie]].

## Structure
- **Lobby** au centre (zone sûre).
- **4 petits labyrinthes interconnectés**, chacun avec :
  - un **thème sensoriel** (ex. : A = ennemis aveugles, B = ennemis sourds) — [[Discretion_et_Aggro]] ;
  - des **variantes visuelles** du kit modulaire (murs, sol, plafond, portes) ;
  - des **coffres** et des **spawners** placés aléatoirement ;
  - une **Salle des Gardiens** au fond (arène non procédurale) — [[Gardiens]].
- **Porte de sortie finale** — [[Fragments_et_Cle]].
- **Indices** laissés par les anciens bataillons — [[Univers]].
- Génération : [[Generation_Labyrinthe]].

| Zone | Thème sensoriel | Mobs | Gardien |
|---|---|---|---|
| A | Aveugles (bruit) — exemple | ? | ? |
| B | Sourds (mouvement / lumière) — exemple | ? | ? |
| C | ? | ? | ? |
| D | ? | ? | ? |

## Ennemis et objets présents
- Mobs : [[Bestiaire]] — configurés dans `SpawnerManager.waves` ([[Apparition_Ennemis]])
- Objets « Labyrinthe » : [[Catalogue_Objets]]

## Scènes du projet
| Scène | Usage |
|---|---|
| `Assets/Scenes/Labyrinthe.unity` | Niveau principal (seule scène des Build Settings) |
| `Assets/Scenes/Scene_Base.unity` | Scène de base |
| `Assets/Scenes/Demo_Market.unity` | Démo d'assets |
| `Gabin.unity`, `Kenny.unity`, `Lucas.unity`, `Nadir.unity` | Scènes de test personnelles |
