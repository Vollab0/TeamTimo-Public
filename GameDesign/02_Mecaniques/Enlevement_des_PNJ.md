---
type: mecanique
status: draft
priority: medium
owner:
scripts: []
tags: [gdd, mecanique, pnj]
updated: 2026-11-07
---
# Enlèvement des PNJ — le « RNG du Fermier »

## 1. Intention de design
Le village est **en sursis** : chaque jour, le Fermier « enlève » aléatoirement des PNJ. Les joueurs doivent **prioriser leurs achats** (soins, équipement) avant que le spécialiste concerné ne disparaisse définitivement.

## 2. Règles et comportement
- **Chaque matin**, un calcul aléatoire désigne le(s) PNJ enlevé(s).
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
| PNJ enlevés par matin | 1 ? | « un groupe » / « des PNJ » selon les passages |
| Poids aléatoires par PNJ | égaux ? | Protéger certains PNJ en début de partie ? |

## 5. Liens
- [[PNJ]] · [[Cycle_de_Partie]] · [[Lobby]] · [[Univers]]

## 6. Implémentation technique
- Écouter l'événement « nouveau jour » de [[Cycle_de_Partie]].
- En multijoueur : tirage côté serveur et synchronisé.

## 7. Questions ouvertes
- [ ] Combien de PNJ enlevés par jour ? Avec 6 PNJ et 10 jours, tous disparaissent avant la fin si c'est 1 par jour.
- [ ] Le Forgeron peut-il être enlevé dès le jour 1 ? (rend l'assemblage de la clé très risqué)
- [ ] Peut-on sauver un PNJ enlevé ?
