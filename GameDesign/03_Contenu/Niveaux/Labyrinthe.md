---
type: niveau
status: in-progress
owner:
scene: Assets/Scenes/Labyrinthe.unity
tags: [gdd, contenu, niveau]
updated: 2026-11-08
---
# Niveau — Labyrinthe

## Objectif du joueur
Partir du [[Lobby]] central, explorer les **4 zones** (A, B, C, D), récupérer or et objets, obtenir les **4 fragments de clé** dans les salles des gardiens (raid de jour ou vol de nuit), puis **trouver** et ouvrir la **porte de sortie** avant la fin du jour 10. Les zones se font **dans l'ordre voulu** — [[Cycle_de_Partie]].

## Structure
- **Lobby** au centre (zone sûre).
- **4 petits labyrinthes interconnectés**, chacun avec :
  - un **gardien** — [[Gardiens]] ;
  - des **mobs mélangés** (Taureau, Araignée, Loup, Taupe, et l'Aigle, plus rare) : pas de thème sensoriel (abandonné le 2026-11-08) — [[Bestiaire]] ;
  - des **variantes visuelles** du kit modulaire (murs, sol, plafond, portes) ;
  - des **coffres** et des **spawners** placés aléatoirement ;
  - une **Salle des Gardiens** au fond (arène non procédurale) — [[Gardiens]].
- **Porte de sortie finale** — [[Fragments_et_Cle]].
- **Indices** laissés par les anciens bataillons — [[Univers]].
- Génération : [[Generation_Labyrinthe]].

| Zone | Ambiance visuelle | Gardien |
|---|---|---|
| A | ? | tiré au sort |
| B | ? | tiré au sort |
| C | ? | tiré au sort |
| D | ? | tiré au sort |

Les 4 gardiens (Rat, Coq, Oie, Truie) sont **répartis aléatoirement entre les zones à chaque partie** (décidé le 2026-11-08) : la colonne « Gardien » change d'une partie à l'autre — [[Gardiens]].

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
