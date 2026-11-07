---
type: mecanique
status: in-progress
priority: high
owner:
scripts:
  - Assets/Scripts/PlayerHealth.cs
  - Assets/Scripts/HeartUIManager.cs
  - Assets/Scripts/DamageDealer.cs
  - Assets/Scripts/RagdollController.cs
tags: [gdd, mecanique, joueur, combat]
updated: 2026-11-08
---
# Santé, mort et réanimation

## 1. Intention de design
La mort n'est pas la fin : en coopération, un joueur tombé peut être **réanimé**. La partie n'est perdue que si **tous les joueurs sont morts** ([[Cycle_de_Partie]]).

## 2a. Règles — design (brainstorming)
- **Santé en cœurs** + **stamina** (voir [[Deplacement_Joueur]]).
- **Soins :**
  - chez le [[Medecin]] (contre des pièces) ;
  - avec des **Baies** (soignent 1 cœur) — seul moyen de se soigner si le Médecin a été enlevé.
- **Mort & réanimation :**
  - le [[Medecin]] peut réanimer un joueur mort ; **gratuit (ou peu cher) si les autres joueurs ont ramené son corps** au Lobby ;
  - le **Kit de réanimation** permet de réanimer un allié sur place.

> [!success] Décidé le 2026-11-08 — déroulé de la mort
> 1. À 0 cœur, le joueur passe **à terre** quelques secondes : un allié peut le relever sur place avec un **Kit de réanimation**.
> 2. Passé ce délai, il est **mort** : son **corps** devient un objet transportable que les alliés ramènent au [[Medecin]] pour le ressusciter.
> 3. En attendant, le joueur mort est **spectateur**. Son or porté tombe au sol — [[Economie]].
> 4. Game Over si tous les joueurs sont morts — [[Cycle_de_Partie]].
- **Bouclier :** permet d'encaisser des coups — [[Combat_Attaque]].
- Les mobs de nuit font plus de dégâts — [[Cycle_Jour_Nuit]].

## 2b. Règles et comportement (état actuel du code)
- **Points de vie :** `nombreDeCoeurs × 2` (gestion en demi-cœurs). 3 cœurs = 6 PV.
- **Dégâts :** `Blesser(degats, positionSource)` → overlay de sang (UI Toolkit), clignotement rouge du mesh, knockback (voir [[Deplacement_Joueur]]).
- **Mort :** à 0 PV → ragdoll projeté depuis la source du coup (`knockbackForce × deathForceMultiplier`). Rien d'autre ensuite (pas d'écran de fin).
- **Soin :** `Soigner(soin)`, plafonné au maximum.
- **Sources de dégâts :** [[Taureau]] (zone + contact pendant la charge) et le composant générique `DamageDealer` (trigger ou collision avec le tag `Player`).
- **Pas d'invincibilité temporaire** après un coup : chaque source gère son propre cooldown.
- **Touches de debug :** `K` / `L` = 1 dégât, `H` = 1 soin (ancienne API `Input`).

## 3. Paramètres de tuning (Inspector)
| Paramètre | Type | Défaut | Rôle |
|---|---|---|---|
| `nombreDeCoeurs` | int | 3 | PV max = ×2 |
| `damageColor` | Color | rouge | Couleur du flash |
| `flashDuration` | float | 0.1 | Durée d'un flash |
| `numberOfFlashes` | int | 2 | Nombre de flashs |
| `overlayName` | string | `DamageOverlay` | Élément UI Toolkit |
| `fadeSpeed` | float | 0.5 | Vitesse de disparition de l'overlay |
| `maxOpacity` | float | 1 | Opacité de l'overlay à l'impact |
| `knockbackForce` | float | 15 | Force de recul |
| `deathForceMultiplier` | float | 1.5 | Force du ragdoll à la mort |
| `DamageDealer.damageAmount` | int | 1 | Dégâts d'un `DamageDealer` |

## 4. Feedback
- HUD : cœurs pleins / demi / vides dans `HeartsContainer` (`HeartUIManager`).
- Overlay sang plein écran + flash du mesh + recul.

## 5. Liens
- Utilisé par : [[Taureau]], [[Inventaire]] (consommables de soin), [[UI_UX]]

## 6. Implémentation technique
- **Scripts :** `PlayerHealth.cs` (événement `OnSanteChangee`), `HeartUIManager.cs`, `DamageDealer.cs`, `RagdollController.cs`

## 7. Questions ouvertes
- [ ] Ajouter des frames d'invincibilité ?
- [x] Le corps d'un joueur mort est-il transportable ? → **oui** (2026-11-08)
- [x] Que fait un joueur mort en attendant ? → **spectateur** (2026-11-08)
- [ ] Durée de l'état « à terre » ? Peut-on ramper / appeler à l'aide ?
- [ ] Si le [[Medecin]] est enlevé, un joueur mort ne peut plus revenir (seul le kit pendant l'état « à terre ») ?
- [ ] Porter un corps : ralentit-il comme un fragment ? Bloque-t-il l'inventaire ?
- [ ] Le HUD affiche-t-il des **cœurs** (prototype) ou des **barres de vie** (brainstorming, HUD) ?
- [ ] À faire dans le code : stamina, mort → état « à terre », réanimation, perte des touches de debug.
