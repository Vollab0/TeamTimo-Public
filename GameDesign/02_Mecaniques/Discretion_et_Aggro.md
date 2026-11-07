---
type: mecanique
status: draft
priority: high
owner:
scripts: []
tags: [gdd, mecanique, ia, discretion]
updated: 2026-11-07
---
# Discrétion, sens des monstres et aggro

## 1. Intention de design
Chaque zone du labyrinthe a une **thématique sensorielle** qui dicte le gameplay (voir [[Piliers_de_Design]]) :
- **Exemple zone A — ennemis aveugles :** sensibles au **bruit** → il faut être discret (s'accroupir, ne pas courir).
- **Exemple zone B — ennemis sourds :** détectent le **mouvement ou la lumière** → il faut se cacher visuellement.

Les objets servent à **manipuler l'aggro** des monstres.

## 2. Règles et comportement
### Sources de détection (à définir par zone)
| Sens | Déclenché par | Contre-mesure |
|---|---|---|
| Vue (cône) | Être dans le champ de vision | Carton (immobile), Sable (aveugle 3 s) |
| Ouïe | Courir, objets bruyants | S'accroupir ; diversion avec Cailloux / Réveil |
| Lumière | Torche, allumettes | Éteindre sa lumière |
| Vibrations | Se déplacer près de la [[Taupe]] | Rester immobile ? |

### Objets qui modifient l'aggro — [[Catalogue_Objets]]
| Objet | Effet |
|---|---|
| Tomate | Change la cible de l'IA |
| Sable | Aveugle 3 s et change l'aggro |
| Cailloux | Fait du bruit (et marque le chemin) |
| Réveil | Sonne après 10 s, attire **tous** les monstres (diversion) |
| Carton | Camouflage (invisible) si immobile (`velocity == 0`) |
| Allumettes / Torche | Éclairent (allumettes jetables au sol) |

### La nuit
Rayon d'aggro augmenté — [[Cycle_Jour_Nuit]].

## 3. Paramètres de tuning
| Paramètre | Défaut | Rôle |
|---|---|---|
| Rayon de bruit (marche / course / accroupi) | ? | |
| Durée d'aveuglement (Sable) | 3 s | |
| Délai du Réveil | 10 s | |
| Multiplicateur d'aggro de nuit | ? | |

## 5. Liens
- [[Bestiaire]] · [[Deplacement_Joueur]] (accroupissement) · [[Generation_Labyrinthe]] (thème par zone) · [[Gardiens]] (boss aveugle à leurrer par le son)

## 6. Implémentation technique
- Prévoir un système de **stimuli** commun (bruit / visuel) émis par le joueur et les objets, et écouté par toutes les IA, plutôt que de coder chaque sens dans chaque mob.
- Existant : `EnemySight` (vision en cône + raycast) et `BullAI` (BoxCast) — aucune gestion du bruit.

## 7. Questions ouvertes
- [ ] Quels thèmes pour les zones C et D ?
- [ ] Les mobs « classiques » (taureau, araignée…) suivent-ils aussi le thème de leur zone ?
