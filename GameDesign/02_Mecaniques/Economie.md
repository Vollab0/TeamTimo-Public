---
type: mecanique
status: draft
priority: medium
owner:
scripts: []
tags: [gdd, mecanique, economie]
updated: 2026-11-07
---
# Économie

## 1. Intention de design
L'or permet de **se préparer** (soins, équipement, carte, réparation) : la progression passe par les statistiques et l'équipement (voir [[Piliers_de_Design]]).

## 2. Règles et comportement
### Gagner de l'or
- **Tuer des monstres** (loot de mobs) — [[Bestiaire]]
- **Fouiller les coffres** disséminés dans les couloirs — [[Generation_Labyrinthe]]
- **Revendre des objets** à l'[[Arnaqueur]] (jamais rentable, prix fluctuant chaque jour)

### Dépenser de l'or (au [[Lobby]])
| PNJ | Service |
|---|---|
| [[Marchand]] | Objets de base (consommables : cailloux, allumettes…) |
| [[Forgeron]] | Durabilité / réparation des objets, assemblage de la clé |
| [[Medecin]] | Soins, réanimation (gratuite ou peu chère si le corps est ramené) |
| [[Cartographe]] | Mise à jour de la carte |
| [[Arnaqueur]] | Jeux d'argent |

### Stocker l'or
- Le [[Banquier]] stocke les pièces de tout le monde et prend **1 pièce par jour d'intérêt**.
- S'il est enlevé, il **lâche les pièces** : les joueurs doivent se les partager.
- Le **coffre du Lobby** est améliorable (stockage d'objets ?).

## 3. Paramètres de tuning
| Paramètre | Défaut | Rôle |
|---|---|---|
| Or par mob | ? | Par type de mob — [[Bestiaire]] |
| Or par coffre | ? | |
| Prix des objets | ? | Voir [[Catalogue_Objets]] |
| Intérêt du banquier | 1 pièce / jour | Coût de la banque |

## 4. Feedback
- HUD : argent — [[UI_UX]]
- Sons : achat, loot.

## 5. Liens
- [[PNJ]] · [[Catalogue_Objets]] · [[Enlevement_des_PNJ]] · [[Multijoueur]] (or partagé ou individuel ?)

## 7. Questions ouvertes
- [ ] L'or est-il individuel ou commun à l'équipe ?
- [ ] Que perd un joueur qui meurt (or porté) ?
- [ ] Équilibrage prix des objets vs gain d'or (tâche de polish).
