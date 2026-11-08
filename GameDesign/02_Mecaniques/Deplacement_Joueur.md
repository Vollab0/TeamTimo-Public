---
type: mecanique
status: in-progress
priority: high
owner:
scripts:
  - Assets/Scripts/PlayerController.cs
  - Assets/Scripts/AnimationStateController.cs
tags: [gdd, mecanique, joueur]
updated: 2026-11-08
---
# Déplacement du joueur

## 1. Intention de design
D'après le brainstorming :
- Déplacements : **marche, course, accroupissement** (l'accroupissement sert la discrétion — [[Discretion_et_Aggro]]).
- **Stamina** — limite la course et la roulade. Affichée par une **petite jauge discrète** près du personnage, visible seulement quand on la consomme (décidé le 2026-11-08).
- **Saut conservé** et **roulade ajoutée** (décidé le 2026-11-08) : une esquive au sol en plus du saut du prototype.
- **Roulade (précisé le 2026-11-08) :** coûte de la **stamina** et donne une **courte invincibilité** (i-frames) : elle sert à esquiver la charge du [[Taureau]] ou un coup. La stamina sert **seulement à courir et rouler**, pas à attaquer.
- ~~Caméra : Top-Down ou Isométrique.~~ → **Décidé le 2026-11-08 : 3e personne**, comme le prototype (murs hauts qui bouchent la vue : le labyrinthe reste oppressant et la carte précieuse).

> [!success] Caméra tranchée (2026-11-08)
> On garde la **3e personne** (Cinemachine FreeLook) du prototype. La vue 1re personne n'a pas été retenue comme option joueur. Restent à coder : accroupissement, stamina et roulade.

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
| *roulade : distance, durée, coût en stamina* | — | à créer | |
| *roulade : durée d'invincibilité* | — | à créer | Fraction de seconde |

## 4. Feedback
- VFX Graph : événements `StartSprint`/`StopSprint` (`SprintSmoke`), `OnJump` (`JumpSmoke`), `OnLand` (`LandSmoke`).
- Animator piloté par `AnimationStateController` via `PlayerController.GetState()`.
- Animations prévues : Idle, Walk, Run, Jump, Roll (roulade), Attack, Push (poussée), Hit, Die — [[Direction_Artistique]].
- Sons de pas différents selon le sol.

## 5. Liens
- Utilisé par : [[Combat_Attaque]], [[Sante_et_Degats]], [[Ramassage_Objets]], [[Discretion_et_Aggro]]
- Personnages : [[Personnages_Jouables]]

## 6. Implémentation technique
- **Scripts :** `PlayerController.cs`, `AnimationStateController.cs`
- **Prefab :** `Assets/Prefabs/Characters/Player.prefab`
- **Notes :** dette technique listée dans [[Backlog]].

## 7. Questions ouvertes
- [x] **Caméra** → **3e personne** (2026-11-08)
- [ ] La vue 1re personne du prototype : la retirer, ou la garder pour le debug ?
- [x] Garde-t-on le saut ? → **oui**, et on ajoute une **roulade** (2026-11-08)
- [x] Roulade : stamina ? invincibilité ? → **coûte de la stamina**, **i-frames courtes** (2026-11-08)
- [ ] Roulade : quelle touche (manette et clavier) ?
- [x] Stamina partagée avec l'attaque ? → **non** : course et roulade seulement (2026-11-08)
