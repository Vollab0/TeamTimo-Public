---
type: mecanique
status: draft
priority: medium
owner:
scripts: []
tags: [gdd, mecanique, pnj]
updated: 2026-11-08
---
# Enlèvement des PNJ — le « RNG du Fermier »

## 1. Intention de design
Le village est **en sursis** : chaque jour, le Fermier « enlève » aléatoirement des PNJ. Les joueurs doivent **prioriser leurs achats** (soins, équipement) avant que le spécialiste concerné ne disparaisse définitivement.

## 2. Règles et comportement
- **Un matin sur deux** (jours **2, 4, 6, 8 et 10**), un calcul aléatoire désigne **1 PNJ important** (à service) enlevé (décidé le 2026-11-08). Soit **5 enlèvements** : il reste **1 PNJ important** le dernier jour. Le jour 1 est tranquille.
- **Figurants :** le [[Lobby]] est aussi peuplé de **nombreux villageois figurants** (simples lignes de dialogue, aucun service). Leurs dialogues donnent des **rumeurs et indices utiles** (faiblesses des gardiens, coffres, porte de sortie) : chaque figurant enlevé, c'est de l'information perdue (décidé le 2026-11-08). Plusieurs sont enlevés **chaque matin**, même les matins sans enlèvement important : le joueur doit **sentir le village se vider** jour après jour.
- **Mise en scène :** à l'aube, une courte séquence montre la **silhouette géante du Fermier** qui emporte les villageois (décidé le 2026-11-08) — [[Univers]].
- Le GameObject du PNJ est désactivé et **son stand est fermé** pour le reste de la partie.
- **Conséquences par PNJ :**

| PNJ | Si enlevé |
|---|---|
| [[Forgeron]] | Les joueurs construisent la clé eux-mêmes, mais **si le porteur la lâche, elle se casse** |
| [[Medecin]] | Soins uniquement avec des **baies** ; un mort revient par la **veillée à la grange** (ou au matin, sans or ni inventaire) ; plus de chirurgie ; seul le Baume du [[Pharmacien]] soigne les **séquelles** |
| [[Marchand]] | Aucun impact |
| [[Cartographe]] | Plus de mise au propre ni de fusion : chacun garde son **croquis artisanal** (Plume) — à confirmer |
| [[Arnaqueur]] | Aucun impact |
| [[Banquier]] | Lâche les pièces : les joueurs doivent se les partager |
| [[Pharmacien]] *(semi-important)* | **Jamais enlevé** : sa cachette le protège (décidé le 2026-11-08) |

## 3. Paramètres de tuning
| Paramètre | Défaut | Rôle |
|---|---|---|
| PNJ importants enlevés | 1 tous les 2 jours (jours 2, 4, 6, 8, 10) | Décidé le 2026-11-08 |
| Figurants au départ | ? | Ex. 20-30 |
| Figurants enlevés par matin | ? | Doit vider le village vers le jour 10 |
| Poids aléatoires par PNJ | égaux | Aucun PNJ protégé, pas même le Forgeron (décidé le 2026-11-08) |

## 5. Liens
- [[PNJ]] · [[Cycle_de_Partie]] · [[Lobby]] · [[Univers]]

## 6. Implémentation technique
- Écouter l'événement « nouveau jour » de [[Cycle_de_Partie]].
- En multijoueur : tirage côté serveur et synchronisé.

## 7. Questions ouvertes
- [x] Combien de PNJ enlevés par jour ? → **1 PNJ important tous les 2 jours (jours 2, 4, 6, 8, 10) + des figurants chaque matin** (2026-11-08)
- [x] Les figurants ont-ils un rôle ? → **rumeurs et indices** (2026-11-08)
- [ ] Quels indices précisément, et comment éviter qu'un indice vital disparaisse avec un figurant ?
- [x] Le Forgeron peut-il être enlevé ? → **oui, comme les autres**, dès le jour 2 : on assemble alors la clé soi-même (2026-11-08)
- [ ] Peut-on sauver un PNJ enlevé ?
