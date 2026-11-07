---
type: mecanique
status: draft
priority: medium
owner:
scripts: []
tags: [gdd, mecanique, ui, navigation]
updated: 2026-11-07
---
# Carte et navigation

## 1. Intention de design
Se repérer dans le labyrinthe est un défi en soi : la carte est **une ressource** qu'on améliore au village, et plusieurs objets aident à ne pas se perdre.

## 2. Règles et comportement
- **Minimap** avec **brouillard de guerre** (se dévoile en explorant).
- **Carte globale** mise à jour via le [[Cartographe]] : au retour d'une exploration, les joueurs paient quelques pièces pour mettre à jour leur carte.
  - Chaque joueur possède **sa propre carte** (note du tableau, texte coupé — à confirmer).
  - Le Cartographe dessine aussi la **carte globale du Lobby**.
  - S'il est enlevé : **carte mal dessinée → moins d'informations** — [[Enlevement_des_PNJ]].
- **Objets de navigation** — [[Catalogue_Objets]] :
  - **Boussole** : indique ton emplacement sur la carte / pointe vers le Lobby ou l'objectif ;
  - **Cailloux** : retrouver son chemin (et faire du bruit) ;
  - **Stylo / Craie** : laisser des tags sur les murs (système de decals) ;
  - **Rappel** : téléportation au Lobby (avec délai d'incantation).
- Les **anciens bataillons** ont laissé des indices dans le labyrinthe — [[Univers]].

## 4. Feedback
- UI : minimap, carte plein écran, flèche de boussole — [[UI_UX]].

## 5. Liens
- [[Cartographe]] · [[Generation_Labyrinthe]] · [[UI_UX]] · [[Multijoueur]] (cartes partagées ?)

## 7. Questions ouvertes
- [ ] La minimap révèle-t-elle automatiquement ce qu'on a vu, ou seulement après passage chez le Cartographe ?
- [ ] Les tags au stylo sont-ils visibles par les autres joueurs ?
