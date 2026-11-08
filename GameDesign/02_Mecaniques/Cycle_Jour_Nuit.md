---
type: mecanique
status: in-progress
priority: high
owner:
scripts:
  - Assets/Scripts/DayNightCycle.cs
tags: [gdd, mecanique, coeur, ambiance]
updated: 2026-11-08
---
# Cycle jour / nuit

![[Mob_Souris_Jour.jpg|180]] ![[Mob_Souris_Nuit.jpg|180]]
*Un mob de jour et sa variante de nuit.*

## 1. Intention de design
Deux phases au rôle opposé :
- **Le jour** sert à se **préparer** et à **visiter** le labyrinthe (exploration, coffres, achats).
- **La nuit** sert à **récupérer les morceaux de clé / les armes** pour pouvoir sortir. Les mobs y sont plus forts : il faut faire plus attention.

## 2. Règles et comportement
### Design (brainstorming)
- Répartition **70 % jour / 30 % nuit** (décidé le 2026-11-08 ; le brainstorming hésitait entre 60/40 et 50/50) : sur un jour de ~18 min, **~13 min de jour et ~5 min de nuit**. La nuit est un « coup » court et risqué.
- **La nuit :**
  - les mobs deviennent des **mobs de nuit** : buff de vitesse et de dégâts, **rayon d'aggro augmenté**, apparence différente (yeux rouges, textures sombres) — [[Bestiaire]] ;
  - les **gardiens dorment** et sont **plus faibles** → fenêtre pour voler le fragment — [[Gardiens]] ;
  - idée : les « monstres importants » dorment → possibilité de récupérer des objets importants.
- Les joueurs peuvent **sauter la nuit** : si **tous les joueurs vivants sont dans la grange**, ils peuvent dormir et passer directement au matin (décidé le 2026-11-08). C'est aussi la **veillée** qui ramène un allié mort quand le Médecin est enlevé — [[Sante_et_Degats]].
- **Event System :** le passage à la nuit déclenche le changement de stats des monstres.
- Transition d'éclairage : lumière directionnelle, skybox, post-process.

### Existant (code)
- Cycle continu de `dayDurationInSeconds` ; `timeOfDay` : 0 = midi, 0.5 = minuit.
- Pilote soleil, lune (gradients), lumière ambiante, brouillard et `_DayNightBlend` de la skybox.
- **Purement visuel** : aucun événement n'est émis, pas de répartition 70/30 (le cycle est symétrique), pas de compteur de jours.
- `[ExecuteAlways]` : tourne aussi en mode édition.

## 3. Paramètres de tuning
| Paramètre | Type | Défaut code | Cible design | Rôle |
|---|---|---|---|---|
| `dayDurationInSeconds` | float | 60 | 1080 (18 min) | Durée d'un cycle complet |
| part de jour | float | (50 %) | 70 % | À ajouter |
| `timeOfDay` | float [0-1] | 0 | | Heure de départ |
| `sunIntensity` / `moonIntensity` | float | 1.5 / 0.5 | | Puissance des astres |
| `fogDensity` | float | 0.005 | | Brouillard |
| `sunColor`, `moonColor`, `ambientColor`, `fogColor` | Gradient | — | | Couleurs sur le cycle |

## 4. Feedback
- Musique : thème exploration **jour** vs **nuit (stressant)** — [[Direction_Artistique]]
- HUD : timer jour/nuit — [[UI_UX]]

## 5. Liens
- Dépend de : [[Cycle_de_Partie]]
- Impacte : [[Bestiaire]], [[Gardiens]], [[Apparition_Ennemis]], [[Fragments_et_Cle]]

## 6. Implémentation technique
- **Script :** `DayNightCycle.cs`
- **À faire :** événements `OnDayStarted` / `OnNightStarted`, ratio jour/nuit, compteur de jours (ou déléguer au `GameManager`), skip de nuit.
- **Matériau :** `Assets/Art/Materials/Environment/MySkybox.mat` — modifié en mode édition par le script, voir [[Backlog]].

## 7. Questions ouvertes
- [x] Durée d'un jour → **~18 min** (2026-11-08)
- [x] Ratio → **70/30** (2026-11-08)
- [x] Conditions pour sauter la nuit → **tous les joueurs vivants dans la grange** (2026-11-08)
