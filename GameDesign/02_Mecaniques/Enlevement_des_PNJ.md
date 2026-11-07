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
- **Chaque matin**, un calcul aléatoire désigne **1 PNJ important** (à service) enlevé (décidé le 2026-11-08).
- **Figurants :** le [[Lobby]] est aussi peuplé de **nombreux villageois figurants** (simples lignes de dialogue, aucun service). Plusieurs sont enlevés chaque matin eux aussi : le joueur doit **sentir le village se vider** jour après jour.
- Le GameObject du PNJ est désactivé et **son stand est fermé** pour le reste de la partie.
- **Conséquences par PNJ :**

| PNJ | Si enlevé |
|---|---|
| [[Forgeron]] | Les joueurs construisent la clé eux-mêmes, mais **si le porteur la lâche, elle se casse** |
| [[Medecin]] | Soins uniquement avec des **baies** ; réanimation uniquement avec un **kit de réanimation** |
| [[Marchand]] | Aucun impact |
| [[Cartographe]] | Carte mal dessinée → moins d'informations |
| [[Arnaqueur]] | Aucun impact |
| [[Banquier]] | Lâche les pièces : les joueurs doivent se les partager |

## 3. Paramètres de tuning
| Paramètre | Défaut | Rôle |
|---|---|---|
| PNJ importants enlevés par matin | 1 | Décidé le 2026-11-08 |
| Figurants au départ | ? | Ex. 20-30 |
| Figurants enlevés par matin | ? | Doit vider le village vers le jour 10 |
| Poids aléatoires par PNJ | égaux ? | Protéger certains PNJ en début de partie ? |

## 5. Liens
- [[PNJ]] · [[Cycle_de_Partie]] · [[Lobby]] · [[Univers]]

## 6. Implémentation technique
- Écouter l'événement « nouveau jour » de [[Cycle_de_Partie]].
- En multijoueur : tirage côté serveur et synchronisé.

## 7. Questions ouvertes
- [x] Combien de PNJ enlevés par jour ? → **1 PNJ important + des figurants** (2026-11-08)
- [ ] ⚠️ Avec 6 PNJ importants à 1 par matin, il n'y a **plus aucun service à partir du jour 7** : voulu, ou faut-il plus de PNJ importants / des matins sans enlèvement important ?
- [ ] Les figurants ont-ils un rôle (indices, rumeurs sur le labyrinthe, réactions aux enlèvements) ?
- [ ] Le Forgeron peut-il être enlevé dès le jour 1 ? (rend l'assemblage de la clé très risqué)
- [ ] Peut-on sauver un PNJ enlevé ?
