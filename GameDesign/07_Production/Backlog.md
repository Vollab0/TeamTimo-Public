---
type: backlog
tags: [production]
updated: 2026-11-07
---
# Backlog

> [!tip] Règles
> - Une tâche = une ligne `- [ ]`. Préfixe optionnel : `(Prénom)` pour l'assignation.
> - Un bug qui demande de l'analyse → une note dans `Bugs/` (template `Template_Bug`) et un lien ici.
> - Une idée pas encore décidée → `00_Inbox/` ou section « Idées » ci-dessous.
> - Les tâches 1 à 8 viennent de la liste « Tâches » du brainstorming.

## ⚠️ Design à trancher (incohérences relevées dans le brainstorming)
- [ ] **Caméra** : Top-Down / Isométrique (tâches) ou 3e personne (prototype) ? — [[Deplacement_Joueur]]
- [ ] **Durée d'un jour** : 10 min (tableau) / 20 min = 10 + 10 (tâches) / ~18 min (10 jours ≈ 3 h) ? — [[Cycle_Jour_Nuit]]
- [ ] **Ratio jour/nuit** : 60/40 (tableau) ou 50/50 (tâches) ?
- [ ] **Nombre de PNJ enlevés par matin** (1 ? un groupe ?) — [[Enlevement_des_PNJ]]
- [ ] **Gardiens** : 3 candidats « à revoir » (Taureau, Rat, Coq) pour 4 zones — [[Gardiens]]
- [ ] **Taureau** à la fois mob, boss et Forgeron ? — [[Taureau]], [[Forgeron]]
- [ ] **Vie** : cœurs (prototype) ou barres de vie (HUD du brainstorming) ? — [[Sante_et_Degats]]
- [ ] **Encombrement du fragment** confirmé ? — [[Fragments_et_Cle]]
- [ ] Thèmes sensoriels des zones C et D — [[Discretion_et_Aggro]]
- [ ] Nombre de joueurs, en ligne ou local — [[Multijoueur]]
- [ ] Écrire / valider les piliers — [[Piliers_de_Design]]

## 1. Programmation — systèmes cœur
- [ ] **GameManager (cycle du jeu)** — [[Cycle_de_Partie]]
  - [ ] Compte à rebours global (10 jours)
  - [ ] Game Over (fin du jour 10 ou tous morts)
  - [ ] Victoire (clé assemblée + sortie atteinte)
- [ ] **Cycle jour / nuit** — [[Cycle_Jour_Nuit]]
  - [ ] Timer journalier (durée à trancher)
  - [ ] Transition d'éclairage (lumière directionnelle, skybox, post-process) — *visuel déjà en place*
  - [ ] Event System : changement de stats des monstres à la tombée de la nuit
- [ ] **Génération procédurale (labyrinthe)** — [[Generation_Labyrinthe]]
  - [ ] Algorithme de génération de salles (tile-based ou WFC) — *un générateur DFS existe déjà*
  - [ ] Logique des 4 zones connectées (A, B, C, D) — *déjà en place*
  - [ ] Placement aléatoire du loot (coffres) et des spawners
- [ ] **PNJ & événements (Lobby)** — [[Enlevement_des_PNJ]]
  - [ ] « RNG du Fermier » : chaque matin, calculer quel PNJ disparaît
  - [ ] Désactiver le PNJ enlevé et fermer son stand

## 2. Programmation — joueur & interactions
- [ ] **Contrôleur personnage** — [[Deplacement_Joueur]]
  - [ ] Marche, course *(fait)*, accroupissement
  - [ ] Caméra (Top-Down ou isométrique — à trancher)
  - [ ] Santé (cœurs, *fait*) et stamina
  - [ ] Mort & réanimation (kit de réa) — [[Sante_et_Degats]]
- [ ] **Inventaire** — [[Inventaire]]
  - [ ] UI à slots limités *(fait)*
  - [ ] Stacking des consommables
  - [ ] Objet « Fragment » : bloque tous les slots + ralentit (encombrement)
- [ ] **Combat & outils** — [[Combat_Attaque]]
  - [ ] Hitbox corps-à-corps (Pelle, Fourche) + `WeaponItem.damage`
  - [ ] Projectiles (Lance-pierre)
  - [ ] Durabilité des objets (si applicable)

## 3. Intelligence artificielle — [[Bestiaire]]
- [ ] IA de base (state machine) : patrouille, détection, poursuite, attaque, retour
- [ ] [[Taureau]] : charge rectiligne + stun au mur *(fait)* ; arrêt sur joueur / bouclier
- [ ] [[Araignee]] : pose de toiles (ralentissement) + attaque à vue
- [ ] [[Loup]] : logique de meute (joueurs > loups = fuite / loups > joueurs = attaque)
- [ ] [[Taupe]] : déplacement souterrain (invisible / invulnérable) + émergence (dégâts de zone) + scan de vibrations
- [ ] [[Aigle]] : check de hauteur + tir de projectiles (salive)
- [ ] Comportement de nuit : buff vitesse / dégâts + rayon d'aggro — [[Cycle_Jour_Nuit]]
- [ ] IA des boss : phases des 4 gardiens — [[Gardiens]]

## 4. Art 3D & animation — [[Direction_Artistique]]
- [ ] Personnage joueur (base animale mignonne) — [[Personnages_Jouables]]
- [ ] 6 PNJ (Forgeron Taureau, Médecin Chat, Marchand Blaireau, Cartographe Hibou, Arnaqueur Raton, Banquier Castor) — [[PNJ]]
- [ ] 5 mobs (Taureau, Araignée, Loup, Taupe, Aigle) + variantes « nuit » (yeux rouges, textures sombres)
- [ ] 21 objets — [[Catalogue_Objets]]
- [ ] Kit modulaire du labyrinthe (murs, sol, plafond, portes) × 4 zones
- [ ] Lobby : stands des vendeurs (forge, tente médecin, table du cartographe…)
- [ ] Décor : coffres, toiles d'araignées, arbres, panneaux explicatifs
- [ ] Animations : Idle, Walk, Run, Attack, Hit, Die + charge du taureau, enfouissement de la taupe, vol de l'aigle

## 5. Level design & UI
- [ ] Lobby (zone sûre), PNJ placés de manière logique — [[Lobby]]
- [ ] 4 arènes de boss non procédurales, favorisant les stratégies de raid — [[Gardiens]]
- [ ] HUD : vie, inventaire rapide, timer jour/nuit, compteur de jours (x/10), argent — [[UI_UX]]
- [ ] Menus : principal, pause, Game Over, victoire
- [ ] Minimap (brouillard de guerre) + carte globale via Cartographe — [[Carte_et_Navigation]]
- [ ] Panneaux tuto du Lobby (textures / illustrations)

## 6. Audio — [[Direction_Artistique]]
- [ ] Musiques : Lobby (calme), exploration jour, exploration nuit (stressant), boss / poursuite
- [ ] SFX : pas selon le sol, cris des monstres, interface (clic, achat, loot), sonnerie du Réveil

## 7. Scripting des objets spéciaux — [[Catalogue_Objets]]
- [ ] Tomate / Sable : changement de cible de l'IA (aggro) — [[Discretion_et_Aggro]]
- [ ] Stylo / Craie : decal pour laisser une marque sur le mur
- [ ] Carton : invisibilité si `velocity == 0`
- [ ] Rappel : téléportation au Lobby (avec délai d'incantation)
- [ ] Boussole : UI qui pointe vers le Lobby ou l'objectif
- [ ] Talkie-Walkie : canal vocal global vs proximité (si vocal) — [[Multijoueur]]
- [ ] Baies / Graines de café : brancher `ConsumableItem` et `ActiverBoostVitesse()`

## 8. Polish & debug
- [ ] Playtest du cycle complet (jour 1 à jour 10)
- [ ] Équilibrage de l'économie (prix des objets vs gain d'or) — [[Economie]]
- [ ] Équilibrage de la difficulté (dégâts des mobs vs PV du joueur)

## Dette technique (relevée le 2026-11-07 en lisant le code)
- [x] **Encodage des fichiers** : `BullAI.cs`, `SpawnerManager.cs`, `GameManager.cs`, `InitialisationPositionObjet.cs` étaient en Windows-1252 (accents cassés `�`). Convertis en UTF-8 le 2026-11-07.
- [ ] **`MySkybox.mat` toujours modifié dans git** : `DayNightCycle` est `[ExecuteAlways]` et écrit `_DayNightBlend` dans l'asset du matériau. Piste : `Shader.SetGlobalFloat` ou une instance de matériau. — [[Cycle_Jour_Nuit]]
- [x] **Build Settings** : `Labo.unity` (inexistante) remplacée par `Labyrinthe.unity` le 2026-11-07.
- [ ] `PlayerController` : `GetComponent` / `transform.Find` appelés à chaque frame dans `SprintSmoke()` → mettre en cache dans `Awake()`.
- [ ] `PlayerHealth` : touches de debug `K`/`L`/`H` via l'ancienne API `Input` → les retirer ou les passer sous `#if UNITY_EDITOR`.
- [ ] `EnemySight` : `StopCoroutine("PatrolRoutine")` (par nom) n'arrête pas une coroutine lancée avec `StartCoroutine(PatrolRoutine())` → risque de patrouilles en double. À vérifier.
- [ ] `SpawnerManager` : le taureau est détecté par `prefab.name.Contains("Bull")` → préférer un champ dans `WaveSettings`.
- [ ] `PlayerController.attackDuration` non exposé dans l'Inspector.
- [ ] Champs `[SerializeField] public` (ex. `InventorySystem.inventorySize`, `GrabObject.handOffset`) → `[SerializeField] private` + propriété si besoin.
- [ ] Ranger `Assets/Scripts/` en sous-dossiers par système (en déplaçant les `.meta` avec les scripts).
- [ ] Systèmes mono-joueur (`FindFirstObjectByType<PlayerHealth>()`, cible unique dans `EnemySight`) à revoir pour le multijoueur — [[Multijoueur]].

## Outillage / dépôt
- [x] `.gitattributes` : UnityYAMLMerge pour les fichiers YAML Unity — 2026-11-07.
- [ ] **Chaque membre** : configurer UnityYAMLMerge (procédure dans `README.md`) — (Gabin ✅) (Kenny) (Lucas) (Nadir)
- [ ] **Git LFS** : tenté le 2026-11-07, push refusé par GitHub (« This repository exceeded its LFS budget »). Le propriétaire du compte `Vollab0` doit vérifier *Settings → Billing and licensing → Budgets and alerts* (quota gratuit : 10 Go de stockage + 10 Go de téléchargement par mois). À réactiver si le dépôt grossit beaucoup ou si des fichiers dépassent ~50 Mo.
- [x] README : pitch ajouté — 2026-11-07 — [[Pitch]].

## Idées
- Monstres importants qui dorment la nuit → récupérer des objets importants — [[Cycle_Jour_Nuit]]
- Coffre du Lobby améliorable — [[Lobby]]
- Indices des anciens bataillons dans le labyrinthe — [[Univers]]
