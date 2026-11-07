---
type: vision
status: in-progress
tags: [gdd, vision]
source: Brainstorming (tableau blanc, 2026-11)
updated: 2026-11-08
---
# Pitch — TeamTimo

> [!abstract] Le jeu en une phrase
> Des petits animaux mignons, cobayes d'un « Fermier » scientifique, ont **10 jours** pour vaincre les gardiens de 4 labyrinthes, réassembler une clé et s'évader, pendant que le Fermier enlève chaque matin les habitants de leur village.

## Fiche
| | |
|---|---|
| **Titre (provisoire)** | TeamTimo |
| **Genre** | Survie / évasion **coopérative** en labyrinthe, avec compte à rebours |
| **Joueurs** | Coopératif **en ligne, 1 à 4 joueurs** (jouable en solo, pensé pour 2-4) — voir [[Multijoueur]] |
| **Durée d'une partie** | 10 jours in-game de ~18 min (~3 h réelles), **sauvegarde automatique chaque matin** — voir [[Cycle_de_Partie]] |
| **Personnages jouables** | Renard, Poule, Mouton + un 4e à définir — choix **purement cosmétique** — [[Personnages_Jouables]] |
| **Caméra** | **3e personne** (comme le prototype ; décidé le 2026-11-08) — [[Deplacement_Joueur]] |
| **Moteur** | Unity 6.3 LTS (6000.3), URP |
| **Ton** | Mignon en surface, sombre en dessous. Ressenti visé : **tension + humour** (horreur coop légère à la *Lethal Company*) — [[Univers]] |

## La partie en bref
1. Les joueurs apparaissent dans la **grange** du [[Lobby]], un village sûr peuplé de [[PNJ|PNJ]] marchands et artisans.
2. **Le jour**, ils explorent les 4 zones du [[Labyrinthe]], récupèrent de l'or et des objets (coffres, monstres) et s'équipent au village.
3. **La nuit**, les monstres sont plus forts, mais les [[Gardiens|boss]] dorment : c'est le moment de voler les **fragments de clé** (on peut aussi les arracher de jour en battant le gardien en raid) — [[Cycle_Jour_Nuit]].
4. Chaque matin, le **Fermier enlève un PNJ important** (son service disparaît pour le reste de la partie) **et des villageois figurants** : on voit le village se vider — [[Enlevement_des_PNJ]].
5. Avec les 4 fragments, le Forgeron reconstruit la **Clé Maîtresse** ; il faut ensuite **trouver** la porte de sortie (grâce aux indices des anciens bataillons) et l'ouvrir avant la fin du 10e jour — [[Fragments_et_Cle]].

## Conditions de fin
- ✅ **Victoire :** clé assemblée + porte de sortie atteinte et ouverte avant le coucher du soleil du jour 10.
- ❌ **Défaite :** fin du jour 10, ou tous les joueurs morts.

## Références et inspirations

### 🎬 *Le Labyrinthe* (*The Maze Runner*, film, 2014)
![Affiche du film Le Labyrinthe|180](https://upload.wikimedia.org/wikipedia/en/b/be/The_Maze_Runner_poster.jpg)

Des adolescents se réveillent sans mémoire au centre d'un labyrinthe géant, sujets d'une expérience menée par une organisation.
- **Le Bloc → notre [[Lobby]]** : une zone sûre au centre, entourée du labyrinthe.
- **Jour / nuit** : on explore le labyrinthe de jour, la nuit il devient mortel → [[Cycle_Jour_Nuit]].
- **Les coureurs qui cartographient le labyrinthe** → le [[Cartographe]] et la [[Carte_et_Navigation|carte]].
- **Des cobayes observés par des scientifiques** → le Fermier et ses animaux-cobayes ([[Univers]]).
- La sortie à trouver pour s'évader → [[Fragments_et_Cle]].

### ⚔️ *L'Attaque des Titans* (*Shingeki no Kyojin*, manga / anime)
![Couverture du tome 1|180](https://upload.wikimedia.org/wikipedia/en/d/d6/Shingeki_no_Kyojin_manga_volume_1.jpg) ![Carte des Murs Maria, Rose et Sina|260](https://upload.wikimedia.org/wikipedia/commons/thumb/2/2b/Walls_of_Paradis.svg/500px-Walls_of_Paradis.svg.png)

L'humanité survit derrière d'immenses murs, menacée par des géants qui la dévorent.
- **Une ville fortifiée encerclée de murs percés de portes** → notre village entouré de murailles ([[Lobby]]).
- **Les expéditions du Bataillon d'exploration hors des murs** → nos sorties dans le labyrinthe… et les **anciens bataillons** qui ont laissé des indices ([[Univers]]).
- **Le sentiment d'être une proie** face à des créatures bien plus grandes → [[Gardiens]], [[Bestiaire]].
- **Une vérité sombre cachée derrière le décor** → « mignon dehors, sombre dedans » ([[Piliers_de_Design]]).

### 🎮 *Lethal Company* (jeu vidéo, 2023)
![Jaquette de Lethal Company|180](https://upload.wikimedia.org/wikipedia/en/f/fb/Lethal_Company_Steam_cover.jpg) ![Capture de gameplay de Lethal Company|320](https://upload.wikimedia.org/wikipedia/en/c/c3/Lethal_Company_gameplay_screenshot.png)

Horreur coopérative : une équipe part récupérer des objets dans des complexes infestés de monstres pour atteindre un quota avant une échéance.
- **Le vaisseau comme base de départ** → chez nous, la **grange** du [[Lobby]].
- **Une échéance stricte** qui met la pression → les 10 jours de [[Cycle_de_Partie]].
- **La coopération et la peur partagée** → [[Multijoueur]].

### 🎮 *Content Warning* (jeu vidéo, 2024)
![Jaquette de Content Warning|180](https://upload.wikimedia.org/wikipedia/en/6/63/Content_Warning_cover.jpg) ![Un monstre de Content Warning|320](https://upload.wikimedia.org/wikipedia/en/2/2c/A_monster_featured_in_Content_Warning.png)

Horreur coopérative et décalée : des amis filment des monstres pour devenir viraux.
- **La maison comme base de départ** → chez nous, la **grange**.
- **Un ton léger sur fond d'horreur** → « mignon dehors, sombre dedans » ([[Piliers_de_Design]]).


> [!note] Images
> Les affiches, jaquettes, captures et la carte sont affichées depuis Wikipédia / Wikimedia (connexion internet nécessaire). Elles ne sont pas copiées dans le dépôt : ce sont des visuels sous droits, utilisés ici comme simple référence interne.

Voir aussi : [[Piliers_de_Design]] · [[Boucle_Gameplay.canvas|Boucle de gameplay]]
