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
  - le [[Medecin]] ressuscite un joueur mort : **gratuitement** si ses alliés lui **ramènent son corps** au Lobby, **contre paiement** sinon (il va chercher le corps lui-même) ;
  - le **Kit de réanimation** relève sur place un allié **à terre**.

> [!success] Décidé le 2026-11-08 — déroulé de la mort
> 1. À 0 cœur, le joueur passe **à terre** quelques secondes : un allié peut le relever sur place avec un **Kit de réanimation**.
> 2. Passé ce délai, il est **mort**. Deux façons de le faire revenir chez le [[Medecin]] (précisé le 2026-11-08) :
>    - **corps ramené** : son **corps** est un objet transportable ; si les alliés le rapportent au Médecin, la résurrection est **gratuite** ;
>    - **corps laissé sur place** : les alliés **paient** le Médecin, qui va récupérer le corps lui-même en râlant (ex. : « Il va encore falloir que j'aille récupérer le corps moi-même… »). Le joueur **réapparaît au Lobby**.
> 3. En attendant, le joueur mort est **spectateur**. Son or porté tombe au sol — [[Economie]].
> 4. Game Over si tous les joueurs sont morts — [[Cycle_de_Partie]].
>
> **Médecin enlevé — la veillée** (décidé le 2026-11-08 : une mort définitive serait trop punitive) :
> - si les alliés **ramènent le corps à la grange** et y **dorment** (ce qui saute la nuit — [[Cycle_Jour_Nuit]]), le mort **se réveille au matin** ;
> - si personne ne ramène le corps, il se réveille quand même **à la grange au matin suivant**, mais en ayant perdu son **or porté** et son **inventaire**.
>
> **Séquelles** (précisé le 2026-11-08) : un joueur revenu **sans le Médecin** (veillée, réveil au matin, solo) ou relevé au **kit** perd **½ cœur max** à chaque fois. Les séquelles se cumulent, mais la vie max ne descend **jamais sous ½ cœur**. On les soigne chez le [[Medecin]] (½ cœur par soin) ou avec le **Baume de séquelle** du [[Pharmacien]]. Le Médecin enlevé, elles s'accumulent plus vite qu'on ne les soigne.
>
> **Chirurgie** (Médecin) : **+½ cœur max** par opération, **2 fois au maximum** (+1 cœur entier). La vie max va donc de ½ cœur à 4 cœurs.
>
> **Prix d'un demi-cœur — loi quadratique** (décidé le 2026-11-08) : `prix = k × n²`, où `n` est le nombre de demi-cœurs que le joueur a **avant** l'achat (sa vie actuelle pour un soin, sa vie max pour une séquelle ou une chirurgie). **Moins on a de cœurs, moins c'est cher.** `k` est à régler en playtest.
>
> **En solo** (personne pour ramener le corps ni payer) : le joueur mort **se réveille à la grange au matin suivant**, en perdant son **or porté** et le reste de la journée, avec une **séquelle**. Pas de Game Over immédiat.
>
> **Affichage :** la vie reste en **cœurs** (demi-cœurs), comme le prototype.
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
| *séquelle : vie max perdue par mort* | — | 1 demi-cœur | Voir « Séquelles » |
| *vie max minimale* | — | 1 demi-cœur | Les séquelles ne descendent pas plus bas |
| *chirurgie : gain par opération / opérations max* | — | 1 demi-cœur / 2 | Améliorations du [[Medecin]] |
| *coefficient de prix `k`* | — | ? | `prix = k × n²` (soin, séquelle, chirurgie) |

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
- [x] Si le Médecin est enlevé, un joueur mort peut-il revenir ? → **oui** : veillée à la grange, ou réveil au matin en perdant or et inventaire (2026-11-08)
- [x] Séquelles → **½ cœur max par mort sans Médecin** (ou relevé au kit), cumulables jusqu'à **½ cœur** de vie max ; soin au prix quadratique (2026-11-08)
- [ ] Valeur de `k` ; la loi quadratique s'applique-t-elle aussi à la résurrection payante ?
- [ ] Porter un corps : ralentit-il comme un fragment ? Bloque-t-il l'inventaire ?
- [ ] Résurrection payante : quel prix (fixe, croissant à chaque mort) ? Qui paie (or porté, banque commune) ? Réapparition immédiate ou au matin ?
- [x] Le HUD affiche-t-il des cœurs ou des barres de vie ? → **cœurs** (2026-11-08)
- [x] Comment afficher la stamina ? → **petite jauge discrète** près du personnage, visible quand on court / roule (2026-11-08) — [[Deplacement_Joueur]]
- [ ] À faire dans le code : stamina, mort → état « à terre », réanimation, perte des touches de debug.
