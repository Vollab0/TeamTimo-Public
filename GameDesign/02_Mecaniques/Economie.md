---
type: mecanique
status: draft
priority: medium
owner:
scripts: []
tags: [gdd, mecanique, economie]
updated: 2026-11-08
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
| [[Medecin]] | Soins ; résurrection **gratuite** si le corps est ramené, **payante** sinon (il va chercher le corps) ; soin des séquelles ; chirurgie (+½ cœur max, 2 fois max) |
| [[Pharmacien]] | Remèdes (potion de soin, tonique de stamina, baume de séquelle) et kits de réanimation à partir de Baies et d'Herbes (s'il a été trouvé) |
| [[Cartographe]] | Mise au propre et fusion des cartes des joueurs (payante ? à confirmer) |
| [[Arnaqueur]] | Jeux d'argent |

### Prix des soins (décidé le 2026-11-08)
**Prix d'un demi-cœur — loi quadratique** (décidé le 2026-11-08) : `prix = k × n²`, où `n` est le nombre de demi-cœurs que le joueur a **avant** l'achat (sa vie actuelle pour un soin, sa vie max pour une séquelle ou une chirurgie). **Moins on a de cœurs, moins c'est cher.** `k` est à régler en playtest. Exemple avec `k = 1` : passer de 1 à 2 demi-cœurs coûte 1, de 5 à 6 coûte 25 ; une chirurgie à 3 cœurs (n = 6) coûte 36, la suivante 49 — [[Medecin]].

### Possession de l'or (décidé le 2026-11-08)
- **Individuel** : chacun porte son or. À la mort, l'or porté **tombe au sol** (récupérable par les alliés) ; en solo, il est perdu.
- La **banque est commune** à l'équipe : y déposer son or le met en sécurité et en commun.

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
- [[PNJ]] · [[Catalogue_Objets]] · [[Enlevement_des_PNJ]] · [[Multijoueur]]

## 7. Questions ouvertes
- [x] L'or est-il individuel ou commun ? → **individuel, banque commune** (2026-11-08)
- [x] Que perd un joueur qui meurt ? → son **or porté tombe au sol** (2026-11-08 ; inventaire : à préciser)
- [ ] Équilibrage prix des objets vs gain d'or (tâche de polish).
