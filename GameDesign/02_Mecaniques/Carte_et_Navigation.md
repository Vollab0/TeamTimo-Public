---
type: mecanique
status: draft
priority: medium
owner:
scripts: []
tags: [gdd, mecanique, ui, navigation]
updated: 2026-11-08
---
# Carte et navigation

## 1. Intention de design
Se repérer dans le labyrinthe est un défi en soi : la carte est **une ressource** qu'on améliore au village, et plusieurs objets aident à ne pas se perdre.

## 2. Règles et comportement
> [!success] Décidé le 2026-11-08 — la carte se dessine avec la Plume
> 1. Il faut **acheter ou trouver une Plume** ([[Catalogue_Objets]]). Elle **occupe un emplacement d'inventaire** (l'équipe décide qui cartographie). Tant qu'un joueur la porte sur lui, **sa carte se dessine toute seule, de manière artisanale** (traits tremblés, approximatifs) là où il passe. Sans Plume, rien ne se dessine.
> 2. De retour au [[Lobby]], **tous les joueurs donnent leur carte au [[Cartographe]]**, qui en fait une **version propre** et la met à jour avec **tous les chemins trouvés par tous les joueurs**.
> 3. Chaque joueur repart avec cette carte commune, plus lisible.

- **Minimap** avec **brouillard de guerre** : affiche la carte du joueur (croquis artisanal + dernière version du Cartographe).
- **Carte globale** mise à jour via le [[Cartographe]], qui fusionne les cartes de tous les joueurs (coût en pièces : à confirmer).
  - Chaque joueur possède **sa propre carte** (confirmé le 2026-11-08).
  - Le Cartographe dessine aussi la **carte globale du Lobby**.
  - S'il est enlevé : plus de mise au propre ni de fusion, chacun garde son croquis artisanal (à confirmer) — [[Enlevement_des_PNJ]].
- **Objets de navigation** — [[Catalogue_Objets]] :
  - **Boussole** : indique ton emplacement sur la carte / pointe vers le Lobby ou l'objectif ;
  - **Cailloux** : retrouver son chemin (et faire du bruit) ;
  - **Stylo / Craie** : laisser des tags sur les murs (système de decals) ;
  - **Rappel** : téléportation au Lobby (avec délai d'incantation).
- Les **anciens bataillons** ont laissé des indices dans le labyrinthe — [[Univers]].

## 4. Feedback
- UI : minimap, carte plein écran, flèche de boussole — [[UI_UX]].

## 5. Liens
- [[Cartographe]] · [[Generation_Labyrinthe]] · [[UI_UX]] · [[Multijoueur]] (cartes fusionnées par le Cartographe)

## 7. Questions ouvertes
- [x] Qu'est-ce qui remplit la carte ? → **la Plume** dessine un croquis artisanal, le **Cartographe** fusionne et met au propre (2026-11-08)
- [x] La Plume occupe-t-elle un emplacement ? → **oui**, l'un des 3 : l'équipe choisit qui cartographie (2026-11-08)
- [ ] La Plume peut-elle se perdre / s'user ?
- [ ] Si le Cartographe est enlevé : chacun garde son croquis, sans fusion ? Peut-on s'échanger les cartes entre joueurs ?
- [ ] La mise à jour chez le Cartographe est-elle payante ?
- [ ] Les tags au stylo sont-ils visibles par les autres joueurs ?
