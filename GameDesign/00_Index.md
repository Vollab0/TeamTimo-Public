---
type: index
tags: [index]
updated: 2026-11-08
---
# TeamTimo — Game Design Document

> [!abstract] Le jeu
> Des animaux-cobayes ont **10 jours** pour arracher aux gardiens de 4 labyrinthes les fragments d'une clé, la réassembler et s'évader, pendant que le Fermier enlève chaque matin les habitants de leur village.
> Commence par le [[Pitch]], puis la [[Boucle_Gameplay.canvas|boucle de gameplay]]. Les points de design encore flous sont listés dans [[Questions_Ouvertes_GDD]].

## 🎯 01 — Vision
- [[Pitch]] — le jeu en une page
- [[Piliers_de_Design]] — les principes qui tranchent les débats (validés en équipe)
- [[Boucle_Gameplay.canvas|Boucle de gameplay]] (canvas)
- [[Glossaire]] — vocabulaire design ↔ code

## ⚙️ 02 — Mécaniques (1 note = 1 système)
| Partie | Joueur | Monde & IA | Social |
|---|---|---|---|
| [[Cycle_de_Partie]] | [[Deplacement_Joueur]] | [[Generation_Labyrinthe]] | [[Multijoueur]] |
| [[Cycle_Jour_Nuit]] | [[Combat_Attaque]] | [[Apparition_Ennemis]] | [[Economie]] |
| [[Fragments_et_Cle]] | [[Sante_et_Degats]] | [[Discretion_et_Aggro]] | [[Enlevement_des_PNJ]] |
| | [[Inventaire]] | [[Carte_et_Navigation]] | |
| | [[Ramassage_Objets]] | | |

## 📦 03 — Contenu
- **Personnages :** [[Personnages_Jouables]] (Renard, Poule, Mouton, Lapin)
- **Ennemis :** [[Bestiaire]] — [[Souris]] · [[Taureau]] · [[Araignee]] · [[Loup]] · [[Taupe]] · [[Aigle]]
- **Boss :** [[Gardiens]]
- **PNJ :** [[PNJ]] — [[Forgeron]] · [[Medecin]] · [[Marchand]] · [[Cartographe]] · [[Arnaqueur]] · [[Banquier]] · [[Pharmacien]] (Hérisson, semi-important, caché)
- **Objets :** [[Catalogue_Objets]] (23 objets + 3 remèdes du Pharmacien)
- **Niveaux :** [[Lobby]] · [[Labyrinthe]]

## 📖 04 — Univers & Lore
- [[Univers]] — le Fermier, la Moisson, les cobayes

## 🎨 05 — Art & Audio
- [[Direction_Artistique]] (inclut l'audio)
- [[UI_UX]]
- [[Moodboard_Visuel.canvas|Moodboard visuel]] (canvas)

## 🧩 06 — Architecture technique
- [[Vue_Ensemble_Code]] — carte des scripts et flux d'initialisation
- Décisions (ADR) : [[ADR_001_GDD_Obsidian_dans_le_depot]]

## 📅 07 — Production
- [[Roadmap]] · [[Backlog]] · [[Equipe]]
- [[Questions_Ouvertes_GDD]] — tous les points de design encore flous, par priorité
- Dossiers : `Bugs/`, `Playtests/`, `Reunions/`

## 📊 Suivi
![[Suivi_GDD.base]]

---
### Organisation du vault
| Dossier | Contenu |
|---|---|
| `00_Inbox/` | Notes rapides à trier (emplacement par défaut des nouvelles notes) |
| `01_Vision/` → `07_Production/` | Le GDD, du « pourquoi » au « quand » |
| `98_Templates/` | Modèles : *Ctrl+P → « Modèles : Insérer un modèle »* |
| `99_Attachments/` | Images et pièces jointes (automatique) ; `Brainstorming/` = visuels du tableau blanc d'origine |

**Statuts** (propriété `status`) : `draft` → `ready-to-code` → `in-progress` → `implemented` (ou `deprecated`).
