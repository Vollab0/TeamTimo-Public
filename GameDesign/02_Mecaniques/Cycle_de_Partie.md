---
type: mecanique
status: draft
priority: high
owner:
scripts:
  - Assets/Scripts/GameManager.cs
tags: [gdd, mecanique, coeur]
updated: 2026-11-08
---
# Cycle de partie — le compte à rebours des 10 jours

## 1. Intention de design
La « **Moisson** » : la partie dure exactement **10 jours in-game** de **~18 min** chacun (~3 h réelles). Le temps est la ressource principale ; il oblige à prioriser (voir [[Piliers_de_Design]]).

## 2. Règles et comportement
- **Timer global :** 10 jours, chacun découpé en jour / nuit — [[Cycle_Jour_Nuit]].
- **Chaque matin :** enlèvement de figurants ; **un matin sur deux** (jours 2, 4, 6, 8, 10), d'un PNJ important — [[Enlevement_des_PNJ]].
- **Victoire :** Clé Maîtresse assemblée **et** porte de sortie atteinte / ouverte avant le coucher du soleil du jour 10 — [[Fragments_et_Cle]].
- **Game Over :** fin du jour 10, **ou** tous les joueurs morts — [[Sante_et_Degats]].
- **Escalade :** à chaque fragment, le gardien de la zone dont on a pris le fragment **rôde désormais dans tout le labyrinthe** jusqu'à ce qu'on le vainque. Il n'y a pas d'autre hausse de difficulté (décidé le 2026-11-08) — [[Gardiens]].
- **Zones en ordre libre :** les 4 zones sont accessibles dès le jour 1 ; ce sont les gardiens en liberté qui rendent les dernières plus dures — [[Labyrinthe]].
- **Sauvegarde :** automatique au **début de chaque jour**, par l'hôte. On reprend au dernier matin (décidé le 2026-11-08).
- **Entre deux parties :** rien ne rend plus fort ; seuls les **indices de lore** découverts sont archivés — [[Univers]].

## 3. Paramètres de tuning
| Paramètre | Type | Défaut | Rôle |
|---|---|---|---|
| `totalDays` | int | 10 | Durée de la partie |
| `dayDuration` | float (s) | 1080 (18 min) | Jour + nuit, voir [[Cycle_Jour_Nuit]] |

## 4. Feedback
- HUD : compteur de jours `x/10` + timer jour/nuit — [[UI_UX]]
- Écrans : Game Over, Victoire.

## 5. Liens
- Dépend de : [[Cycle_Jour_Nuit]]
- Pilote : [[Enlevement_des_PNJ]], [[Fragments_et_Cle]], [[Apparition_Ennemis]]

## 6. Implémentation technique
- **Prévu (tâches du brainstorming) :** `GameManager` porte le compte à rebours, les conditions de victoire / game over et diffuse des événements (`OnDayStarted`, `OnNightStarted`, `OnGameOver`…).
- **Existant :** `GameManager.cs` n'est pour l'instant qu'un singleton contenant le matériau d'outline.
- En multijoueur, ce timer doit être **autoritaire côté serveur** — [[Multijoueur]].

## 7. Questions ouvertes
- [x] Durée réelle d'un jour → **~18 min** (2026-11-08)
- [x] Un joueur mort peut-il revenir ? → **oui**, chez le Médecin : gratuit si le corps est ramené, payant sinon — [[Sante_et_Degats]] (2026-11-08)
- [ ] Que contient la sauvegarde (graine du labyrinthe, PNJ restants, inventaires, fragments, gardiens errants, corps au sol) ?
- [ ] Lore : que débloque-t-on exactement entre les parties, et où le consulte-t-on ?
