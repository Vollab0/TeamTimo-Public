---
type: mecanique
status: draft
priority: high
owner:
scripts: []
tags: [gdd, mecanique, ia, discretion]
updated: 2026-11-08
---
# Discrétion, sens des monstres et aggro

## 1. Intention de design
> [!info] Décidé le 2026-11-08
> Il n'y a **plus de thème sensoriel par zone**. Ce sont les **mobs** qui ont chacun leur règle, et ils sont mélangés dans toutes les zones (pilier « Chaque mob a sa règle » — [[Piliers_de_Design]]). Chaque mob détecte les joueurs avec ses propres sens — [[Bestiaire]].

Les objets servent à **manipuler l'aggro** des monstres.

## 2. Règles et comportement
### Sources de détection (à définir par mob)
| Sens | Déclenché par | Contre-mesure |
|---|---|---|
| Vue (cône) | Être dans le champ de vision | Carton (immobile), Sable (aveugle 3 s) |
| Ouïe | Courir, objets bruyants | S'accroupir ; diversion avec Cailloux / Réveil |
| Lumière | Torche, allumettes | Éteindre sa lumière |
| Vibrations | Courir, sauter, atterrir près de la [[Taupe]] (décidé le 2026-11-08) | Marcher |
| Odorat | Nourriture, joueurs proches ([[Gardiens]] : la Truie) | Tomates en diversion |

### Sens déjà connus par créature
| Créature | Sens |
|---|---|
| [[Taureau]] | Vue (couloir droit devant) |
| [[Taupe]] | Vibrations |
| L'Oie ([[Gardiens]]) | Vue : elle fait éclore un œuf quand elle voit un joueur |
| La Truie ([[Gardiens]]) | Odorat |
| [[Souris]], [[Araignee]], [[Loup]], [[Aigle]] | à définir |

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
- [[Bestiaire]] · [[Deplacement_Joueur]] (accroupissement) · [[Gardiens]] (chaque gardien a sa stratégie de raid)

## 6. Implémentation technique
- Prévoir un système de **stimuli** commun (bruit / visuel) émis par le joueur et les objets, et écouté par toutes les IA, plutôt que de coder chaque sens dans chaque mob.
- Existant : `EnemySight` (vision en cône + raycast) et `BullAI` (BoxCast) — aucune gestion du bruit.

## 7. Questions ouvertes
- [x] Quels thèmes pour les zones C et D ? → **abandonné** : plus de thèmes par zone (2026-11-08)
- [ ] Quels sens pour chaque mob ? (Taureau, Taupe, Oie, Truie connus — reste Souris, Araignée, Loup, Aigle)
