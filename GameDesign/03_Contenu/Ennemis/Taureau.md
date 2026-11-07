---
type: ennemi
status: in-progress
priority: high
owner:
prefab: Assets/Prefabs/Characters/Bull.prefab
scripts:
  - Assets/Scripts/BullAI.cs
tags: [gdd, contenu, ennemi]
updated: 2026-11-07
---
# Taureau (Bull)

## Rôle dans le jeu
Mob qui **prend toute la largeur des couloirs** : dès qu'il aperçoit le joueur, il prend quelques secondes puis **charge** dans sa direction. Pousse le joueur à éviter les longs couloirs à découvert.

## Comportement — design (brainstorming)
- Il ne s'arrête que dans **3 cas** :
  1. il rentre dans un **mur** ;
  2. il rentre dans un **joueur** ;
  3. il se fait **parer par un bouclier** porté par un joueur.
- Une fois arrêté, il est **étourdi quelques secondes** puis reprend sa patrouille.
- Animation spécifique : charge.

> [!note] Taureau = mob **et** boss ?
> Le tableau « Boss » propose aussi un **Taureau, chef du troupeau de veaux**, et le tableau « Nuit » montre un taureau endormi qui garde un fragment. Voir [[Gardiens]].

## Comportement — état actuel du code
```mermaid
stateDiagram-v2
    [*] --> Patrouille : verrouillé sur l'axe du couloir
    Patrouille --> Preparation : joueur dans le cône + ligne de vue
    Preparation --> Charge : 1 s (s'aligne sur l'axe)
    Charge --> Etourdi : arrive au mur
    Etourdi --> Patrouille : stunDuration
```
- **Patrouille « sur rail »** : se verrouille sur l'axe du couloir et fait des allers-retours. `randomTurnChance` = probabilité de demi-tour chaque seconde.
- **Vision :** BoxCast droit devant + ligne de vue.
- **Charge :** ligne droite jusqu'au mur, sans tourner.
- **Étourdi :** recule de 4 m, immobile `stunDuration` s.
- **Dégâts pendant la charge (cumulatifs) :** zone « vapeur » (`zoneRadius`) + contact (`contactThreshold`).
- **Écarts avec le design :** la charge ne s'arrête **ni sur un joueur ni sur un bouclier** (traverse le joueur) ; pas de variante de nuit.

## Stats et tuning
| Paramètre | Défaut | Rôle |
|---|---|---|
| `visionWidthRatio` | 30 | Largeur de vision (× largeur du corps) |
| `visionHeight` | 5 | Hauteur de la boîte de vision |
| `maxVisionDistance` | 300 | Portée de vision / de charge |
| `patrolSpeed` | 8 | Vitesse de patrouille |
| `chargeSpeed` | 100 | Vitesse de charge |
| `stunDuration` | 5 | Durée d'étourdissement |
| `randomTurnChance` | 0 | % de demi-tour aléatoire / s |
| `zoneRadius` | 15 | Rayon des dégâts de zone |
| `contactThreshold` | 5 | Rayon des dégâts de contact |
| `zoneDamage` / `contactDamage` | 1 / 1 | Demi-cœurs retirés |
| `zoneCooldown` / `contactCooldown` | 1 / 1.5 | Délais entre deux dégâts |
| préparation de charge | 1 s (codé en dur) | « quelques secondes » dans le design |

## Comment le joueur le contre
- Sortir de l'axe du couloir.
- **Parer avec un bouclier** → étourdissement — [[Combat_Attaque]].
- Frapper pendant l'étourdissement.

## Implémentation technique
- **Prefab :** `Assets/Prefabs/Characters/Bull.prefab` · **Script :** `BullAI.cs`
- **Spawn :** couloirs larges uniquement — [[Apparition_Ennemis]]
- **À faire :** arrêt sur collision joueur / bouclier, variante de nuit, PV et loot.

## Questions ouvertes
- [ ] Le joueur percuté prend-il des dégâts et un knockback ?
- [ ] Feedback de préparation de charge (son de meuglement, anim) ?
