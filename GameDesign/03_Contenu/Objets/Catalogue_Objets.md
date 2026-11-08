---
type: catalogue
tags: [gdd, contenu, objet]
updated: 2026-11-08
---
# Catalogue des objets

Les 21 objets du brainstorming, plus la **Plume** et les **Herbes** (ajoutées le 2026-11-08), et 3 remèdes fabriqués par le [[Pharmacien]]. **Rareté** : Commun < Rare < Épique < Légendaire. **Emplacement** : *Marchand* (acheté au [[Marchand]]) ou *Labyrinthe* (coffres / loot).
Un objet complexe mérite sa propre note dans ce dossier (template `Template_Objet`).

| # | Objet | Effet | Rareté | Emplacement | Mécanique | Code |
|---|---|---|---|---|---|---|
| 1 | Tomate | Change l'aggro | Commun | Marchand | [[Discretion_et_Aggro]] | — |
| 2 | Cailloux | Retrouver son chemin + faire du bruit | Commun | Marchand | [[Carte_et_Navigation]] | — |
| 3 | Bâton | Tuer des mobs | Rare | Labyrinthe | [[Combat_Attaque]] | — |
| 4 | Pelle | Tuer des mobs | Épique | Labyrinthe | [[Combat_Attaque]] | `Shovel.asset` (`WeaponItem`, damage 1) |
| 5 | Fourche | Tuer des mobs | Légendaire | Labyrinthe | [[Combat_Attaque]] | — |
| 6 | Stylo (Craie) | Mettre des tags sur les murs (decals) | Légendaire | Labyrinthe | [[Carte_et_Navigation]] | — |
| 7 | Sac | Ajoute un slot d'inventaire | Épique | Labyrinthe | [[Inventaire]] | — |
| 8 | Allumettes | Éclairer (peuvent être jetées au sol) | Commun | Marchand | [[Discretion_et_Aggro]] | — |
| 9 | Torche | Éclairer | Rare | Labyrinthe | [[Discretion_et_Aggro]] | — |
| 10 | Bouclier | Encaisser des coups ; pare la charge du [[Taureau]] | Rare | Labyrinthe | [[Combat_Attaque]] | — |
| 11 | Piège à ours | Étourdit quelques secondes et fait des dégâts | Épique | Labyrinthe | [[Combat_Attaque]] | — |
| 12 | Baies | Soigne un cœur ; ingrédient du [[Pharmacien]] | Commun | Marchand | [[Sante_et_Degats]] | `ConsumableItem` (classe prête) |
| 13 | Sable | Aveugle 3 s et change l'aggro | Rare | Labyrinthe | [[Discretion_et_Aggro]] | — |
| 14 | Carton | Camouflage si immobile (`velocity == 0`) | Rare | Labyrinthe | [[Discretion_et_Aggro]] | — |
| 15 | Rappel | Téléporte au Lobby (délai d'incantation) | Légendaire | Labyrinthe | [[Carte_et_Navigation]] | — |
| 16 | Boussole | Indique ta position sur la carte / pointe vers le Lobby ou l'objectif | Commun | Marchand | [[Carte_et_Navigation]] | — |
| 17 | Réveil | Sonne après 10 s, attire tous les monstres (diversion) | Rare | Labyrinthe | [[Discretion_et_Aggro]] | — |
| 18 | Graines de café | Boost de vitesse | Épique | Labyrinthe | [[Deplacement_Joueur]] | `PlayerController.ActiverBoostVitesse()` |
| 19 | Talkie-Walkie | Chat à distance (vocal global vs proximité) | Épique | Labyrinthe | [[Multijoueur]] | — |
| 20 | Kit de réanimation | Relève sur place un allié **à terre** (le relevé garde une séquelle) | Épique | Labyrinthe + [[Pharmacien]] | [[Sante_et_Degats]] | — |
| 21 | Lance-pierre | Fait tomber les aigles | Épique | Labyrinthe | [[Combat_Attaque]], [[Aigle]] | — |
| 22 | Plume | Portée sur soi, dessine automatiquement (et grossièrement) sa carte ; le Cartographe la met au propre | ? | Marchand + Labyrinthe | [[Carte_et_Navigation]] | — |

| 23 | Herbes | Ingrédient du [[Pharmacien]] (avec les Baies) | Commun ? | Labyrinthe | [[Pharmacien]] | — |

## Remèdes du Pharmacien (ajoutés le 2026-11-08)
Fabriqués par le [[Pharmacien]] à partir de Baies et d'Herbes (recettes et prix à définir).

| Remède | Effet |
|---|---|
| Potion de soin | Rend plusieurs cœurs d'un coup |
| Tonique de stamina | Stamina illimitée ou régénération rapide pendant un temps |
| Baume de séquelle | Rend ½ cœur max perdu — [[Sante_et_Degats]] |
| *Kit de réanimation (n° 20)* | *Aussi fabriqué par le Pharmacien* |

## Objet spécial
- **Fragment de clé** — bloque tous les slots et ralentit le porteur (confirmé le 2026-11-08) ; on peut le poser ou le passer — [[Fragments_et_Cle]].

## Objets de test du prototype
| Objet | Asset | Prefab |
|---|---|---|
| Cube | `Assets/Data item/Data_Cube.asset` (`ItemData`) | `Assets/Prefabs/Items/Cube.prefab` |
| Sphere | `Assets/Data item/Data_Sphere.asset` (`ItemData`) | `Assets/Prefabs/Items/Sphere.prefab` |

## Questions ouvertes
- [ ] Prix de chaque objet au Marchand — [[Economie]]
- [ ] Durabilité : quels objets en ont ?
- [ ] La liste de modélisation ne contient pas le **Bâton** (20 objets listés pour « 21 ») : oubli ?
