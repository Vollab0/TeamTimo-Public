---
type: mecanique
status: in-progress
priority: high
owner:
scripts:
  - Assets/Scripts/PlayerController.cs
  - Assets/Scripts/Items/WeaponItem.cs
tags: [gdd, mecanique, combat]
updated: 2026-11-08
---
# Combat et outils

## 1. Intention de design
On combat avec des **outils de ferme**. Tuer des mobs rapporte de l'or ([[Economie]]) ; les gardiens demandent un vrai « raid » préparé ([[Gardiens]]). La difficulté dépend des statistiques et de l'équipement : il faut s'équiper pour rivaliser.

## 2. Règles et comportement
### Design (brainstorming)
- **Sans arme : poussée seulement** (décidé le 2026-11-08) — aucun dégât, mais on repousse un mob… ou un allié.
- **Corps-à-corps** avec hitbox : **Bâton** (rare), **Pelle** (épique), **Fourche** (légendaire).
- **Projectiles :** **Lance-pierre** — seul moyen de faire tomber les [[Aigle|aigles]] des murs.
- **Défense :** **Bouclier** — encaisse des coups ; **parer le [[Taureau]]** stoppe sa charge.
- **Pièges :** **Piège à ours** — étourdit quelques secondes et inflige des dégâts.
- **Durabilité des objets** (si applicable), réparée par le [[Forgeron]].
- Les mobs ont des **fenêtres d'attaque** (taureau étourdi, taupe sortie de terre).

### Existant (code)
- Input Action `Attack` : le joueur est immobilisé pendant `attackDuration` (0.15 s) ; le flag d'attaque alimente l'Animator.
- **Pas encore implémenté :** hitbox, dégâts aux ennemis, PV des ennemis, projectiles, durabilité, bouclier.
- `WeaponItem.damage` existe (la Pelle a `damage: 1`) mais n'est lu nulle part.

## 3. Paramètres de tuning
| Paramètre | Type | Défaut | Rôle |
|---|---|---|---|
| `attackDuration` | float | 0.15 | Durée d'immobilisation (**privé, non exposé**) |
| dégâts par arme | int | ? | Bâton < Pelle < Fourche |
| durabilité par arme | int | ? | |
| PV des mobs | int | ? | Voir [[Bestiaire]] |

## 5. Liens
- Dépend de : [[Deplacement_Joueur]], [[Inventaire]] (arme = slot sélectionné ?)
- Cibles : [[Bestiaire]], [[Gardiens]]
- Objets : [[Catalogue_Objets]]

## 6. Implémentation technique
- **Scripts :** `PlayerController.Attack()` / `AttackRoutine()`
- **ScriptableObjects :** `WeaponItem` (`Assets/Data item/Shovel.asset`) — à étendre (durabilité, portée, type mêlée / projectile).

## 7. Questions ouvertes
- [x] Attaque à mains nues possible ? → **poussée sans dégâts** (repousse mobs et alliés) (2026-11-08)
- [ ] Combien de PV ont les mobs ? Équilibrage dégâts des mobs vs PV du joueur.
- [ ] Le bouclier occupe-t-il la main de l'arme ?
