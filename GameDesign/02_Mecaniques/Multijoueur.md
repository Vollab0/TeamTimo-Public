---
type: mecanique
status: draft
priority: high
owner:
scripts: []
tags: [gdd, mecanique, reseau]
updated: 2026-11-07
---
# Multijoueur (coopératif)

## 1. Intention de design
Le jeu est pensé **en coopération** : tout le brainstorming parle « des joueurs ». Références : *Lethal Company*, *Content Warning* ([[Pitch]]). Pilier : « Ensemble ou rien » ([[Piliers_de_Design]]).

## 2. Règles — ce que le brainstorming implique
- **Défaite** quand **tous** les joueurs sont morts — [[Cycle_de_Partie]].
- **Réanimation** d'un allié (kit, Médecin, corps ramené) — [[Sante_et_Degats]].
- **Gardiens en « raid »** : combat de groupe — [[Gardiens]].
- **Loup** : fuit si les joueurs sont plus nombreux que les loups, attaque sinon — [[Loup]].
- **Talkie-Walkie** : chat à distance / canal vocal global vs proximité (si vocal) — [[Catalogue_Objets]].
- **Banquier enlevé** : les joueurs doivent se partager les pièces — [[Banquier]].
- **Chaque joueur a sa carte** — [[Carte_et_Navigation]].
- **Personnages jouables :** Renard, Poule, Mouton — [[Personnages_Jouables]].

## 3. État actuel du projet
- **Netcode for GameObjects 1.14.1** installé, `Assets/DefaultNetworkPrefabs.asset` présent.
- `PlayerController` importe `Unity.Netcode` mais reste un `MonoBehaviour` : **aucun script n'est réseau**.
- Plusieurs systèmes supposent **un seul joueur** (`HeartUIManager` : `FindFirstObjectByType<PlayerHealth>()`, `EnemySight` : un seul joueur ciblé).

## 5. Liens
- Impacte : [[Deplacement_Joueur]], [[Sante_et_Degats]], [[Generation_Labyrinthe]] (graine partagée), [[Apparition_Ennemis]] (côté serveur), [[Cycle_de_Partie]] (timer serveur), [[Enlevement_des_PNJ]] (tirage serveur), [[Economie]]

## 6. Implémentation technique
- Décision à formaliser dans un ADR (`06_Architecture_Tech/Decisions/`) : topologie (hôte-client ?), autorité, relay / lobby en ligne.
- Plus le passage au réseau est tardif, plus il coûte cher : à décider avant de coder les nouveaux systèmes.

## 7. Questions ouvertes
- [ ] Nombre de joueurs (2-4 ?)
- [ ] En ligne (Relay / Lobby Unity) ou réseau local ?
- [ ] Jouable en solo ?
- [ ] Chat vocal intégré ?
