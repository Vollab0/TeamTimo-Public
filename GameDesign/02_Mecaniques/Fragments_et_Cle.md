---
type: mecanique
status: draft
priority: high
owner:
scripts: []
tags: [gdd, mecanique, coeur, objectif]
updated: 2026-11-08
---
# Fragments de clé et Clé Maîtresse

![[Boss_Taureau_Endormi.jpg|300]]
*Illustration du brainstorming : le principe du vol de nuit (le Taureau n'est plus gardien).*

## 1. Intention de design
L'objectif principal : pour s'enfuir, récupérer **4 fragments de clé**, un dans la **salle des gardiens** au fond de chacune des 4 zones. Chaque fragment est une prise de risque qui rend la suite plus dure.

## 2. Règles et comportement
- **Où :** un **pilier** dans chaque salle des gardiens — [[Gardiens]].
- **Comment — deux voies (décidé le 2026-11-08) :**
  - **de jour**, il faut vaincre le gardien en « raid » ;
  - **de nuit**, le gardien dort (et est plus faible) : on peut s'approcher du fragment. Mais le fragment **est le pilier qui retient le boss** : l'enlever **réveille le gardien, qui peut alors se promener partout** dans le labyrinthe.
- **Escalade de la difficulté :** à chaque fragment récupéré, le gardien de la zone dont on a pris le fragment **rôde désormais dans tout le labyrinthe** jusqu'à ce qu'on le vainque. Il n'y a pas d'autre hausse de difficulté (décidé le 2026-11-08) — [[Gardiens]]. Plus on a de fragments, plus il y a de gardiens en liberté.
- **Malédiction du porteur — encombrement (confirmé le 2026-11-08) :** le fragment est un objet lourd qui **bloque tous les slots d'inventaire et réduit la vitesse de déplacement**. Le porteur peut le **poser** ou le **passer à un allié** ; s'il meurt, le fragment **tombe au sol** (il ne se casse pas). Le retour au Lobby devient une **escorte**.
- **Stockage (décidé le 2026-11-08) :** les fragments rapportés au village sont posés sur un **socle dans la grange**, visible par tous (« 2/4 »), indépendant des PNJ : un enlèvement ne fait jamais perdre de fragment — [[Lobby]].
- **Reconstruction :** rapporter les fragments au **Forgeron** pour réassembler la **Clé Maîtresse** (contre des pièces).
  - **Clé forgée par le Forgeron** : un **objet normal** (1 emplacement d'inventaire), pas encombrante (décidé le 2026-11-08).
  - **Si le Forgeron a été enlevé :** les joueurs construisent la clé eux-mêmes ; cette clé bricolée est **encombrante comme un fragment** et **se casse si le porteur la lâche** — [[Forgeron]].
- **Sortie :** la porte de sortie finale **change d'emplacement à chaque partie** ; on la localise grâce aux **indices des anciens bataillons** ([[Univers]]) et au [[Cartographe]], puis on l'ouvre avant la fin du jour 10 — [[Cycle_de_Partie]].
- **Indices (décidé le 2026-11-08) :** les indices **vitaux** (porte de sortie, faiblesses des gardiens) sont **inscrits dans le labyrinthe** par les anciens bataillons ; les **figurants** ne donnent que des **raccourcis** en plus. Un figurant enlevé fait perdre du temps, **jamais une information indispensable** — [[Enlevement_des_PNJ]].

## 3. Paramètres de tuning
| Paramètre | Type | Défaut | Rôle |
|---|---|---|---|
| `fragmentCount` | int | 4 | Fragments nécessaires |
| `carrierSpeedMultiplier` | float | ? | Ralentissement du porteur |
| prix d'assemblage (Forgeron) | int | ? | Voir [[Economie]] |

## 4. Feedback
- Réveil du gardien : son, tremblement, alerte à tous les joueurs ?
- HUD : fragments `x/4`.

## 5. Liens
- [[Gardiens]] · [[Cycle_Jour_Nuit]] · [[Inventaire]] · [[Forgeron]] · [[Generation_Labyrinthe]] (porte de sortie)

## 6. Implémentation technique
- Fragment = `ItemData` spécial (à créer) qui bloque l'inventaire — voir [[Inventaire]].
- La sortie actuelle du labyrinthe (`MazeGenerator.CreateRandomExit()`) est un mur ouvert : à remplacer par une porte verrouillée.

## 7. Questions ouvertes
- [x] Encombrement confirmé → **oui**, fragment lâchable / transmissible (2026-11-08)
- [x] Le fragment se casse-t-il si le porteur meurt ? → **non**, il tombe au sol (2026-11-08)
- [x] Où sont stockés les fragments récupérés ? → **socle dans la grange** (2026-11-08)
- [x] La Clé Maîtresse est-elle encombrante ? → **non** si forgée par le Forgeron, **oui** si bricolée par les joueurs (2026-11-08)
- [x] La porte de sortie → **à trouver via les indices**, emplacement variable (2026-11-08)
- [x] Comment éviter qu'un indice vital disparaisse avec un figurant ? → indices vitaux **dans le labyrinthe**, figurants = raccourcis (2026-11-08)
- [ ] Forme des indices du labyrinthe (gravures, notes, cartes partielles) et combien faut-il en trouver ?