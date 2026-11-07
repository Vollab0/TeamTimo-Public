---
type: mecanique
status: in-progress
priority: high
owner:
scripts:
  - Assets/Scripts/PlayerController.cs
  - Assets/Scripts/AnimationStateController.cs
tags: [gdd, mecanique, joueur]
updated: 2026-11-07
---
# Déplacement du joueur

## 1. Intention de design
D'après le brainstorming :
- Déplacements : **marche, course, accroupissement** (l'accroupissement sert la discrétion — [[Discretion_et_Aggro]]).
- **Stamina** associée à la santé (cœurs) — limite la course.
- **Caméra : Top-Down ou Isométrique.**

> [!warning] Écart avec le prototype
> Le code actuel est en **3e personne** (Cinemachine FreeLook) avec option 1re personne, sans accroupissement ni stamina. Le choix de caméra change le level design, l'UI et les contrôles : à trancher avant d'aller plus loin.

## 2. Règles et comportement (état actuel du code)
- **Contrôleur :** `CharacterController`, déplacement relatif à la caméra.
- **Caméra :** 3e personne (Cinemachine FreeLook) par défaut, ou 1re personne (VirtualCamera) via `_isThirdPerson`. Une action `SwitchCamera` existe dans les inputs.
- **Saut :** avec *coyote time* et *jump buffer*.
- **Sprint :** maintenu ; ne s'active qu'une fois au sol. Multiplie la vitesse **et** la hauteur de saut. Pas de stamina.
- **Knockback :** `ApplyKnockback(source, force)` appelé par [[Sante_et_Degats]] ; le recul décroît progressivement.
- **Boost de vitesse :** `ActiverBoostVitesse(multiplicateur, durée)` existe — à brancher sur les **Graines de café** ([[Catalogue_Objets]]).
- **Ralentissements prévus :** toile d'[[Araignee]], porteur de fragment ([[Fragments_et_Cle]]).
- **Inputs (`Assets/Animations/Characters/PlayerInput.inputactions`, map `Gameplay`) :** `Move`, `Look`, `Jump`, `Sprint`, `Attack`, `Grab`, `Scroll`, `SwitchCamera` — schémas ZQSD, flèches, manette. Liés via les events du composant `PlayerInput`.

## 3. Paramètres de tuning (Inspector)
| Paramètre | Type | Défaut | Rôle |
|---|---|---|---|
| `speed` | float | 15 | Vitesse de marche |
| `speedBoost` | float | 2 | Multiplicateur de vitesse en sprint |
| `jump` | float | 4 | Impulsion de saut |
| `jumpBoost` | float | 2 | Multiplicateur de saut en sprint |
| `smoothTime` | float | 10 | Vitesse de rotation du perso |
| `gravityMultiplier` | float | 3 | Gravité appliquée (× -9.81) |
| `coyoteTimeDuration` | float | 0.1 | Délai de saut après avoir quitté le sol |
| `jumpBufferDuration` | float | 0.15 | Mémorisation de l'appui saut avant l'atterrissage |
| `_isThirdPerson` | bool | true | Caméra TPS / FPS |
| `knockbackDecay` | float | 5 | Vitesse d'amortissement du recul |
| *stamina max, coût du sprint, régénération* | — | à créer | |
| *vitesse accroupi* | — | à créer | |

## 4. Feedback
- VFX Graph : événements `StartSprint`/`StopSprint` (`SprintSmoke`), `OnJump` (`JumpSmoke`), `OnLand` (`LandSmoke`).
- Animator piloté par `AnimationStateController` via `PlayerController.GetState()`.
- Animations prévues : Idle, Walk, Run, Attack, Hit, Die — [[Direction_Artistique]].
- Sons de pas différents selon le sol.

## 5. Liens
- Utilisé par : [[Combat_Attaque]], [[Sante_et_Degats]], [[Ramassage_Objets]], [[Discretion_et_Aggro]]
- Personnages : [[Personnages_Jouables]]

## 6. Implémentation technique
- **Scripts :** `PlayerController.cs`, `AnimationStateController.cs`
- **Prefab :** `Assets/Prefabs/Characters/Player.prefab`
- **Notes :** dette technique listée dans [[Backlog]].

## 7. Questions ouvertes
- [ ] **Caméra : 3e personne (prototype) ou Top-Down / Isométrique (brainstorming) ?**
- [ ] Garde-t-on le saut ? (pas mentionné dans le brainstorming)
- [ ] Stamina partagée entre course et autres actions (attaque) ?
