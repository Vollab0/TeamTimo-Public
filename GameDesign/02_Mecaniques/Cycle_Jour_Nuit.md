---
type: mecanique
status: in-progress
priority: high
owner:
scripts:
  - Assets/Scripts/DayNightCycle.cs
tags: [gdd, mecanique, coeur, ambiance]
updated: 2026-11-07
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
- Répartition **60 % jour / 40 % nuit**.
- **La nuit :**
  - les mobs deviennent des **mobs de nuit** : buff de vitesse et de dégâts, **rayon d'aggro augmenté**, apparence différente (yeux rouges, textures sombres) — [[Bestiaire]] ;
  - les **gardiens dorment** et sont **plus faibles** → fenêtre pour voler le fragment — [[Gardiens]] ;
  - idée : les « monstres importants » dorment → possibilité de récupérer des objets importants.
- Les joueurs peuvent **sauter la nuit** s'ils le veulent (vote ? tous au Lobby ? à préciser).
- **Event System :** le passage à la nuit déclenche le changement de stats des monstres.
- Transition d'éclairage : lumière directionnelle, skybox, post-process.

### Existant (code)
- Cycle continu de `dayDurationInSeconds` ; `timeOfDay` : 0 = midi, 0.5 = minuit.
- Pilote soleil, lune (gradients), lumière ambiante, brouillard et `_DayNightBlend` de la skybox.
- **Purement visuel** : aucun événement n'est émis, pas de répartition 60/40 (le cycle est symétrique), pas de compteur de jours.
- `[ExecuteAlways]` : tourne aussi en mode édition.

## 3. Paramètres de tuning
| Paramètre | Type | Défaut code | Cible design | Rôle |
|---|---|---|---|---|
| `dayDurationInSeconds` | float | 60 | ⚠️ 600 ou 1200 | Durée d'un cycle complet |
| part de jour | float | (50 %) | 60 % | À ajouter |
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
- [ ] Durée d'un jour : **10 min** (tableau « Cycle jour/nuit ») ou **20 min = 10 jour + 10 nuit** (liste de tâches) ? Et 10 jours ≈ 3 h donne ~18 min.
- [ ] Ratio : 60/40 (tableau) ou 50/50 (liste de tâches) ?
- [ ] Conditions pour sauter la nuit (tous d'accord ? tous au Lobby ?).
