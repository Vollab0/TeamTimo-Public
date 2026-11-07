---
type: mecanique
status: draft
priority: high
owner:
scripts:
  - Assets/Scripts/GameManager.cs
tags: [gdd, mecanique, coeur]
updated: 2026-11-07
---
# Cycle de partie — le compte à rebours des 10 jours

## 1. Intention de design
La « **Moisson** » : la partie dure exactement **10 jours in-game** (~3 h réelles). Le temps est la ressource principale ; il oblige à prioriser (voir [[Piliers_de_Design]]).

## 2. Règles et comportement
- **Timer global :** 10 jours, chacun découpé en jour / nuit — [[Cycle_Jour_Nuit]].
- **Chaque matin :** événement d'enlèvement d'un PNJ — [[Enlevement_des_PNJ]].
- **Victoire :** Clé Maîtresse assemblée **et** porte de sortie atteinte / ouverte avant le coucher du soleil du jour 10 — [[Fragments_et_Cle]].
- **Game Over :** fin du jour 10, **ou** tous les joueurs morts — [[Sante_et_Degats]].
- **Escalade :** chaque fragment débloqué augmente la difficulté globale.

## 3. Paramètres de tuning
| Paramètre | Type | Défaut | Rôle |
|---|---|---|---|
| `totalDays` | int | 10 | Durée de la partie |
| `dayDuration` | float (s) | ⚠️ à trancher | Voir [[Cycle_Jour_Nuit]] |

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
- [ ] Durée réelle d'un jour : 10 min ? 20 min ? 18 min (3 h / 10) ? Les trois chiffres apparaissent dans le brainstorming.
- [ ] Un joueur mort peut-il revenir avant le Game Over (réanimation) ? — oui d'après [[Sante_et_Degats]], à préciser.
