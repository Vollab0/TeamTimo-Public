---
type: art
status: in-progress
tags: [gdd, art, audio]
updated: 2026-11-07
---
# Direction artistique et audio

## Intention
Animaux **3D stylisés, mignons** (grands yeux verts, formes rondes, rendu doux type figurine) dans un univers cartoon — qui cache une réalité sombre ([[Univers]]). La nuit, les monstres basculent vers des versions **inquiétantes** (yeux rouges, textures sombres).

Moodboard : [[Moodboard_Visuel.canvas|Moodboard visuel]] · images dans `99_Attachments/Brainstorming/`.

## Personnages (chara design)
| Élément | Liste | Note |
|---|---|---|
| Joueurs | Renard, Poule, Mouton | [[Personnages_Jouables]] |
| PNJ (6) | Forgeron Taureau, Médecin Chat, Marchand Blaireau, Cartographe Hibou, Arnaqueur Raton, Banquier Castor | [[PNJ]] |
| Mobs (5) | Taureau, Araignée, Loup, Taupe, Aigle + **variantes de nuit** | [[Bestiaire]] |
| Boss | Taureau, Rat, Coq (à revoir) — versions musclées | [[Gardiens]] |

## Objets (props)
Les 21 objets du [[Catalogue_Objets]].

## Environnement (level art)
- **Kit modulaire du labyrinthe :** murs, sol, plafond, portes — **variantes pour les 4 zones**.
- **Lobby :** stands / étals des vendeurs (forge, tente médecin, table du cartographe…), grange, maisons, feu de camp, enclos, murs d'enceinte — [[Lobby]].
- **Décor :** coffres, toiles d'araignées, arbres, panneaux explicatifs.

## Animations
- Communes : **Idle, Walk, Run, Attack, Hit, Die**.
- Spécifiques : charge du taureau, enfouissement de la taupe, vol de l'aigle.

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
