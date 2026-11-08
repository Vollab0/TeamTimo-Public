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
> 1. À 0 cœur, le joueur passe **à terre** pendant **~30 s** (précisé le 2026-11-08) : il peut **ramper** lentement et **appeler à l'aide** (cri + marqueur visible des alliés). Un allié peut le relever sur place avec un **Kit de réanimation**.
> 2. Passé ce délai, il est **mort**. Deux façons de le faire revenir chez le [[Medecin]] (précisé le 2026-11-08) :
>    - **corps ramené** : son **corps** est un objet transportable, **encombrant comme un fragment** (ralentit et bloque tout l'inventaire du porteur — précisé le 2026-11-08) ; si les alliés le rapportent au Médecin, la résurrection est **gratuite** et le joueur **garde ses objets** ;
>    - **corps laissé sur place** : un allié **paie avec son or porté** un prix **qui augmente à chaque mort de la partie** (précisé le 2026-11-08) ; le Médecin va récupérer le corps lui-même en râlant (« Il va encore falloir que j'aille récupérer le corps moi-même… »). Le joueur **réapparaît tout de suite au Lobby**, mais **sans ses objets** : « Je n'ai pas pu prendre ces objets avec moi. »
> 3. En attendant, le joueur mort est **spectateur**.
>
> **Ce que l'on perd en mourant** (décidé le 2026-11-08) :
> - **l'or porté** tombe **toujours** à côté du corps et **disparaît au bout de 15 s** : les alliés doivent le ramasser vite — [[Economie]] ;
> - **les objets** sont **conservés** si les alliés ramènent eux-mêmes le corps (au Médecin ou à la grange pour la veillée). Sinon, ils restent **au sol sur le lieu de la mort jusqu'au matin suivant**, puis disparaissent.
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
| *durée « à terre »* | — | ~30 s | Le joueur rampe et peut appeler à l'aide |
| *durée de vie de l'or tombé* | — | 15 s | Puis il disparaît |
| *résurrection payante : prix de base / hausse par mort* | — | ? / ? | Payée en or porté |

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
- [x] Durée de l'état « à terre » ? Peut-on ramper / appeler à l'aide ? → **~30 s**, **ramper + appel à l'aide** (2026-11-08)
- [x] Si le Médecin est enlevé, un joueur mort peut-il revenir ? → **oui** : veillée à la grange, ou réveil au matin en perdant or et inventaire (2026-11-08)
- [x] Séquelles → **½ cœur max par mort sans Médecin** (ou relevé au kit), cumulables jusqu'à **½ cœur** de vie max ; soin au prix quadratique (2026-11-08)
- [ ] Valeur de `k`.
- [x] Porter un corps : ralentit-il comme un fragment ? Bloque-t-il l'inventaire ? → **oui, comme un fragment** (2026-11-08)
- [x] Résurrection payante : prix, qui paie, quand ? → **croissant à chaque mort**, payé avec l'**or porté** d'un allié, **réapparition immédiate** sans les objets (2026-11-08). La loi `k × n²` ne s'y applique pas.
- [ ] Résurrection payante : prix de départ et augmentation (ex. 20, +10 par mort) ? Le compteur est-il par joueur ou pour toute l'équipe ?
- [ ] En solo, le réveil au matin fait-il aussi perdre les objets (ils disparaissent au matin selon la règle générale) ? Le texte « En solo » ne mentionne que l'or.
- [x] Le HUD affiche-t-il des cœurs ou des barres de vie ? → **cœurs** (2026-11-08)
- [x] Comment afficher la stamina ? → **petite jauge discrète** près du personnage, visible quand on court / roule (2026-11-08) — [[Deplacement_Joueur]]
- [ ] À faire dans le code : stamina, mort → état « à terre », réanimation, perte des touches de debug.
