---
type: ennemi
status: draft
priority: medium
owner:
prefab:
scripts: []
tags: [gdd, contenu, ennemi]
updated: {{date}}
---
# {{title}}

## Rôle dans le jeu
Quelle pression il met sur le joueur, où on le rencontre, ce qui le rend unique.

## Comportement (machine à états)
```mermaid
stateDiagram-v2
    [*] --> Patrouille
    Patrouille --> Alerte : voit le joueur
    Alerte --> Poursuite
    Poursuite --> Patrouille : perd le joueur
```

## Stats et tuning
| Paramètre | Valeur | Rôle |
|---|---|---|
| Vitesse | | |
| Dégâts | | |
| Vision | | |

## Comment le joueur le contre
-

## Feedback (anim / son / VFX)
-

## Implémentation technique
- **Prefab :**
- **Scripts :**
- **Spawn :** voir [[Apparition_Ennemis]]

## Questions ouvertes
- [ ]
