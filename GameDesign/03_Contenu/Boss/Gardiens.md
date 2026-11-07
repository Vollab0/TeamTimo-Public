---
type: boss
status: draft
priority: high
owner:
scripts: []
tags: [gdd, contenu, boss]
updated: 2026-11-07
---
# Les Gardiens (boss de raid)

![[Boss.jpg]]

## Rôle dans le jeu
Chaque zone du labyrinthe se termine par une **Salle des Gardiens** où un boss garde un **fragment de clé** — [[Fragments_et_Cle]]. Il y a une **cohérence totale entre le boss et sa zone**.

## Règles (design)
- **Mécanique de raid :** le combat n'est pas une simple bagarre. C'est un raid unique qui demande une **stratégie basée sur la spécificité de la zone** (ex. : un boss aveugle surpuissant qu'il faut **leurrer par le son**) — [[Discretion_et_Aggro]].
- **Stats et préparation :** l'affronter sans l'équipement adéquat est **suicidaire** — [[Combat_Attaque]], [[Economie]].
- **Arènes non procédurales**, conçues à la main pour favoriser les stratégies de raid — [[Generation_Labyrinthe]].
- **La nuit** — [[Cycle_Jour_Nuit]] :
  - les gardiens **s'endorment** et sont **plus faibles** ;
  - le fragment est en fait **un pilier qui retient le boss** : les joueurs peuvent s'en approcher pendant son sommeil ;
  - **enlever le pilier réveille le boss**, qui peut alors **se promener partout** dans le labyrinthe.
  - Ex. tableau « Nuit » : *le taureau dort mais se réveille quand le fragment est récupéré*.
- Musique dédiée : thème Boss / Poursuite.

## Candidats — ⚠️ « À revoir » sur le tableau
| Boss | Description | Zone | Mécanique de raid |
|---|---|---|---|
| **Le Taureau** | Chef du troupeau de veaux | ? | ? |
| **Le Rat** | Chef du groupe des souris | ? | ? |
| **Le Coq** | Chef du poulailler | ? | ? |
| *4e gardien* | *à définir* | ? | ? |

## Implémentation technique
- « IA des Boss : scripter les phases spécifiques des 4 boss de salles » (liste de tâches).
- Phases à prévoir : endormi (nuit) / gardien (jour, dans l'arène) / réveillé et errant (après vol du fragment).

## Questions ouvertes
- [ ] Le 4e gardien ?
- [ ] Le Taureau-boss et le [[Taureau]]-mob coexistent-ils ?
- [ ] Un gardien réveillé et errant peut-il être tué ensuite ? Se rendort-il ?
- [ ] La cohérence boss ↔ zone : quel thème sensoriel pour chacun ?
