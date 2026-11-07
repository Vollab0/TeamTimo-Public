---
type: catalogue
tags: [gdd, contenu, ennemi]
updated: 2026-11-07
---
# Bestiaire

![[Mob_Taureau.jpg|140]] ![[Mob_Araignee.jpg|140]] ![[Mob_Loup.jpg|140]] ![[Mob_Taupe.jpg|140]] ![[Mob_Aigle.jpg|140]]

## Mobs du labyrinthe
| Mob | Rôle | Particularité | Fenêtre d'attaque | Statut |
|---|---|---|---|---|
| [[Taureau]] | Bloqueur de couloir | Charge rectiligne, prend toute la largeur du couloir | Étourdi après un impact | implemented (partiel) |
| [[Araignee]] | Piégeuse | Tisse des toiles qui ralentissent et l'alertent | ? | in-progress |
| [[Loup]] | Traqueur en meute | Appelle des renforts, fuit si en infériorité | ? | draft |
| [[Taupe]] | Embuscade souterraine | Invisible / invulnérable sous terre, émerge avec dégâts de zone | Immobile après émergence | draft |
| [[Aigle]] | Tireur en hauteur | Sur les murs, crache des boules de salive | Lance-pierre uniquement | draft |
| Souris | Mob de base ? | Illustre la variante jour → nuit ; liée au boss Rat | ? | idée |

## Comportement commun (IA de base)
Machine à états : **Patrouille → Détection → Poursuite → Attaque → Retour**.

## Variante de nuit
![[Mob_Souris_Jour.jpg|140]] ![[Mob_Souris_Nuit.jpg|140]]

La nuit, chaque mob devient plus dangereux — [[Cycle_Jour_Nuit]] :
- buff de **vitesse** et de **dégâts**, **rayon d'aggro** augmenté ;
- visuel : **yeux rouges, textures sombres**.

## Gardiens (boss)
Voir [[Gardiens]].

## Questions ouvertes
- [ ] Quels mobs dans quelle zone (thème sensoriel) ? — [[Discretion_et_Aggro]]
- [ ] Or lâché par mob — [[Economie]]
- [ ] La souris est-elle un mob à part entière ?
