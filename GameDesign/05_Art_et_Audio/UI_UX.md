---
type: ui
status: in-progress
tags: [gdd, ui]
scripts:
  - Assets/Scripts/HeartUIManager.cs
  - Assets/Scripts/InventoryUI.cs
  - Assets/Scripts/PlayerHealth.cs
updated: 2026-11-08
---
# UI / UX

Toute l'UI utilise **UI Toolkit** (UXML + USS dans `Assets/UI/`), pas uGUI.

## HUD principal (brainstorming)
| Élément | Statut | Note |
|---|---|---|
| Vie (« barres de vie ») | ✅ en cœurs | ✅ **cœurs** retenus (2026-11-08) — [[Sante_et_Degats]] |
| Stamina | à faire — **petite jauge discrète** près du personnage, visible quand on la consomme (2026-11-08) | [[Deplacement_Joueur]] |
| Inventaire rapide | ✅ | [[Inventaire]] |
| Timer jour / nuit | à faire | [[Cycle_Jour_Nuit]] |
| Compteur de jours (x/10) | à faire | [[Cycle_de_Partie]] |
| Argent | à faire | [[Economie]] |
| Fragments (x/4) | idée | [[Fragments_et_Cle]] |
| Minimap (brouillard de guerre) | à faire | [[Carte_et_Navigation]] |

### Existant
| Élément | Nom UXML | Script |
|---|---|---|
| Cœurs | `HeartsContainer` | `HeartUIManager` (classe USS `heart`) |
| Overlay de dégâts | `DamageOverlay` | `PlayerHealth` |
| Barre d'inventaire | `InventoryContainer` | `InventoryUI` |

> [!warning] Ces noms UXML sont cherchés par chaîne dans le code : les renommer dans UI Builder casse le HUD.

## Menus
- Menu principal, Pause, **Game Over**, **Victoire**.
- Héberger / rejoindre une partie en ligne, reprendre une sauvegarde — [[Multijoueur]].
- **Mode spectateur** d'un joueur mort ; indicateur « à terre » visible par les alliés — [[Sante_et_Degats]].
- **Archive du lore** débloqué entre les parties — [[Univers]].
- Interfaces des PNJ (achat, soin, carte, banque…) — [[PNJ]].

## Carte
- **Minimap** avec brouillard de guerre : croquis artisanal dessiné avec la **Plume**, puis version propre et fusionnée du Cartographe — [[Carte_et_Navigation]].

## Tutoriel
- **Panneaux tuto** du Lobby : textures / illustrations à créer (tutoriel diégétique) — [[Lobby]].

## Contrôles
Actions listées dans [[Deplacement_Joueur]] (`PlayerInput.inputactions`).
