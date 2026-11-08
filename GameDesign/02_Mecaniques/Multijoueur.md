---
type: mecanique
status: draft
priority: high
owner:
scripts: []
tags: [gdd, mecanique, reseau]
updated: 2026-11-08
---
# Multijoueur (coopératif)

## 1. Intention de design
Le jeu est pensé **en coopération** : tout le brainstorming parle « des joueurs ». Références : *Lethal Company*, *Content Warning* ([[Pitch]]). Pilier : « Ensemble ou rien » ([[Piliers_de_Design]]).

## Décisions (2026-11-08)
- **1 à 4 joueurs, en ligne** : jouable en solo, mais pensé pour 2-4. L'équilibrage doit s'adapter au nombre de joueurs.
- **Sauvegarde automatique chaque matin** par l'hôte : on reprend la partie au dernier matin avec le même groupe — [[Cycle_de_Partie]].
- **Personnages cosmétiques** : Renard, Poule, Mouton et **Lapin** (4e animal pour jouer à 4) — [[Personnages_Jouables]].
- **Or individuel + banque commune** — [[Economie]].
- **Mort** : à terre → mort ; résurrection chez le Médecin, gratuite si l'équipe ramène le corps, payante sinon ; sans Médecin, **veillée** à la grange. Spectateur en attendant — [[Sante_et_Degats]].

## 2. Règles — ce que le brainstorming implique
- **Défaite** quand **tous** les joueurs sont morts — [[Cycle_de_Partie]].
- **Réanimation** d'un allié (kit sur un allié à terre ; Médecin, gratuit si corps ramené) — [[Sante_et_Degats]].
- **Gardiens en « raid »** : combat de groupe — [[Gardiens]].
- **Loup** : fuit si les joueurs sont plus nombreux que les loups, attaque sinon — [[Loup]].
- **Talkie-Walkie** : chat à distance / canal vocal global vs proximité (si vocal) — [[Catalogue_Objets]].
- **Banquier enlevé** : les joueurs doivent se partager les pièces — [[Banquier]].
- **Chaque joueur a sa carte** — [[Carte_et_Navigation]].
- **Personnages jouables :** Renard, Poule, Mouton, Lapin — [[Personnages_Jouables]].

## 3. État actuel du projet
- **Netcode for GameObjects 2.13** installé, `Assets/DefaultNetworkPrefabs.asset` présent.
- `PlayerController` importe `Unity.Netcode` mais reste un `MonoBehaviour` : **aucun script n'est réseau**.
- Plusieurs systèmes supposent **un seul joueur** (`HeartUIManager` : `FindFirstObjectByType<PlayerHealth>()`, `EnemySight` : un seul joueur ciblé).

## 5. Liens
- Impacte : [[Deplacement_Joueur]], [[Sante_et_Degats]], [[Generation_Labyrinthe]] (graine partagée), [[Apparition_Ennemis]] (côté serveur), [[Cycle_de_Partie]] (timer serveur), [[Enlevement_des_PNJ]] (tirage serveur), [[Economie]]

## 6. Implémentation technique
- Décision à formaliser dans un ADR (`06_Architecture_Tech/Decisions/`) : topologie (hôte-client ?), autorité, relay / lobby en ligne.
- Plus le passage au réseau est tardif, plus il coûte cher : à décider avant de coder les nouveaux systèmes.

## 7. Questions ouvertes
- [x] Nombre de joueurs → **1 à 4** (2026-11-08)
- [x] En ligne ou local → **en ligne** (2026-11-08) ; technologie (Relay / Lobby Unity, Steam…) à fixer dans un ADR
- [x] Jouable en solo → **oui** (2026-11-08)
- [ ] Comment l'équilibrage s'adapte-t-il au nombre de joueurs (PV des gardiens, nombre de mobs, [[Loup]] en solo, porter un corps seul) ?
- [x] En solo, la mort = Game Over ? → **non** : réveil à la grange au matin suivant, or porté perdu (2026-11-08) — [[Sante_et_Degats]]
- [ ] Peut-on rejoindre une partie en cours / reprendre une sauvegarde avec un joueur en moins ?
- [ ] Chat vocal intégré ?
