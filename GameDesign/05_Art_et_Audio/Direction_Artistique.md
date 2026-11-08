---
type: art
status: in-progress
tags: [gdd, art, audio]
updated: 2026-11-08
---
# Direction artistique et audio

## Intention
Animaux **3D stylisés, mignons** (grands yeux verts, formes rondes, rendu doux type figurine) dans un univers cartoon — qui cache une réalité sombre ([[Univers]]). La nuit, les monstres basculent vers des versions **inquiétantes** (yeux rouges, textures sombres).

Moodboard : [[Moodboard_Visuel.canvas|Moodboard visuel]] · images dans `99_Attachments/Brainstorming/`.

## Personnages (chara design)
| Élément | Liste | Note |
|---|---|---|
| Joueurs | Renard, Poule, Mouton, Lapin (à illustrer) | [[Personnages_Jouables]] |
| PNJ (6 + 1) | Forgeron Cheval, Médecin Chat, Marchand Blaireau, Cartographe Hibou, Arnaqueur Raton, Banquier Castor + **Pharmacien Hérisson** (caché, à illustrer) | [[PNJ]], [[Pharmacien]] |
| Mobs (5) | Taureau, Araignée, Loup, Taupe, Aigle + **variantes de nuit** | [[Bestiaire]] |
| Boss | Rat, Coq, Oie, Truie — versions musclées (Oie et Truie à illustrer) | [[Gardiens]] |

## Objets (props)
Les 23 objets du [[Catalogue_Objets]] (dont la Plume et les Herbes) + 3 remèdes du Pharmacien.

## Environnement (level art)
- **Kit modulaire du labyrinthe :** murs, sol, plafond, portes — **variantes pour les 4 zones** (indépendantes du gardien, tiré au sort).
- **4 arènes de gardiens** faites à la main (Rat, Coq, Oie, Truie) — [[Gardiens]].
- **Lobby :** stands / étals des vendeurs (forge, tente médecin, table du cartographe…), grange, maisons, feu de camp, enclos, murs d'enceinte — [[Lobby]].
- **Décor :** coffres, toiles d'araignées, arbres, panneaux explicatifs.

## Animations
- Communes (joueurs) : **Idle, Walk, Run, Jump, Roll, Attack, Push, Hit, Die**, état « à terre » et corps porté.
- Spécifiques : charge du taureau, enfouissement de la taupe, vol de l'aigle, phases des 4 gardiens (terriers du Rat, chant du Coq…).
- Cinématiques : silhouette du Fermier à l'aube (chaque matin), fin (fuite et révélation) — [[Univers]].

## Déjà en place dans le projet
- URP (`Assets/URP/`, `Assets/Settings/`), skybox jour/nuit (`MySkybox.mat`, [[Cycle_Jour_Nuit]]).
- VFX Graph : fumées de sprint, saut, atterrissage (`Assets/VFX/`).
- Outline jaune sur les objets ramassables, flash rouge + overlay sang aux dégâts.

## Audio (sound design)
### Musique
| Thème | Ambiance | Statut |
|---|---|---|
| Lobby | Calme / village | à faire |
| Exploration — jour | | à faire |
| Exploration — nuit | Stressant | à faire |
| Boss / poursuite | | à faire |

### SFX
| Événement | Statut |
|---|---|
| Bruits de pas (selon le sol) | à faire |
| Cris des monstres (meuglement, hurlement du loup…) | à faire |
| Interface (clic, achat, loot) | à faire |
| Sonnerie du Réveil (objet) | à faire |

Aucun son n'est encore branché dans les scripts.
