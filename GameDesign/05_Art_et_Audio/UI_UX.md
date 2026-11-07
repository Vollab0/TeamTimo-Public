---
type: ui
status: in-progress
tags: [gdd, ui]
scripts:
  - Assets/Scripts/HeartUIManager.cs
  - Assets/Scripts/InventoryUI.cs
  - Assets/Scripts/PlayerHealth.cs
updated: 2026-11-07
---
# UI / UX

Toute l'UI utilise **UI Toolkit** (UXML + USS dans `Assets/UI/`), pas uGUI.

## HUD principal (brainstorming)
| Élément | Statut | Note |
|---|---|---|
| Vie (« barres de vie ») | ✅ en cœurs | ⚠️ cœurs ou barres ? — [[Sante_et_Degats]] |
| Stamina | à faire | [[Deplacement_Joueur]] |
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
- Interfaces des PNJ (achat, soin, carte, banque…) — [[PNJ]].

## Carte
- **Minimap** avec brouillard de guerre + **carte globale** mise à jour via le Cartographe — [[Carte_et_Navigation]].

## Tutoriel
- **Panneaux tuto** du Lobby : textures / illustrations à créer (tutoriel diégétique) — [[Lobby]].

## Contrôles
Actions listées dans [[Deplacement_Joueur]] (`PlayerInput.inputactions`).
