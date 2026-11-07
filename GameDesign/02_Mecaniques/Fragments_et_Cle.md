---
type: mecanique
status: draft
priority: high
owner:
scripts: []
tags: [gdd, mecanique, coeur, objectif]
updated: 2026-11-07
---
# Fragments de clé et Clé Maîtresse

![[Boss_Taureau_Endormi.jpg|300]]

## 1. Intention de design
L'objectif principal : pour s'enfuir, récupérer **4 fragments de clé**, un dans la **salle des gardiens** au fond de chacune des 4 zones. Chaque fragment est une prise de risque qui rend la suite plus dure.

## 2. Règles et comportement
- **Où :** un coffre / un **pilier** dans chaque salle des gardiens — [[Gardiens]].
- **Comment :**
  - **de jour**, il faut vaincre le gardien en « raid » ;
  - **de nuit**, le gardien dort (et est plus faible) : on peut s'approcher du fragment. Mais le fragment **est le pilier qui retient le boss** : l'enlever **réveille le gardien, qui peut alors se promener partout** dans le labyrinthe.
- **Escalade de la difficulté :** le déblocage d'une clé déclenche une réaction immédiate du système : monstres plus agressifs, nouveaux pièges, ou apparition de l'ennemi dans les autres zones.
- **Malédiction du porteur — encombrement (optionnel / à confirmer) :** le fragment est un objet lourd qui **bloque tous les slots d'inventaire et réduit la vitesse de déplacement**. Le porteur est vulnérable pendant le retour au Lobby.
- **Reconstruction :** rapporter les fragments au **Forgeron** pour réassembler la **Clé Maîtresse** (contre des pièces).
  - **Si le Forgeron a été enlevé :** les joueurs construisent la clé eux-mêmes, mais **si le joueur qui la porte la lâche, elle se casse** — [[Forgeron]].
- **Sortie :** localiser la porte de sortie finale et l'ouvrir avant la fin du jour 10 — [[Cycle_de_Partie]].

## 3. Paramètres de tuning
| Paramètre | Type | Défaut | Rôle |
|---|---|---|---|
| `fragmentCount` | int | 4 | Fragments nécessaires |
| `carrierSpeedMultiplier` | float | ? | Ralentissement du porteur |
| `escalationPerFragment` | ? | ? | Bonus de difficulté par fragment |
| prix d'assemblage (Forgeron) | int | ? | Voir [[Economie]] |

## 4. Feedback
- Réveil du gardien : son, tremblement, alerte à tous les joueurs ?
- HUD : fragments `x/4`.

## 5. Liens
- [[Gardiens]] · [[Cycle_Jour_Nuit]] · [[Inventaire]] · [[Forgeron]] · [[Generation_Labyrinthe]] (porte de sortie)

## 6. Implémentation technique
- Fragment = `ItemData` spécial (à créer) qui bloque l'inventaire — voir [[Inventaire]].
- La sortie actuelle du labyrinthe (`MazeGenerator.CreateRandomExit()`) est un mur ouvert : à remplacer par une porte verrouillée.

## 7. Questions ouvertes
- [ ] Encombrement confirmé ou non ?
- [ ] Le fragment se casse-t-il aussi si le porteur meurt ?
- [ ] Les fragments récupérés sont-ils stockés au Lobby (coffre du Lobby) ?
- [ ] La porte de sortie est-elle visible dès le début, ou à trouver (indices des « anciens bataillons ») ?
