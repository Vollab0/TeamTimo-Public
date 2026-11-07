# CLAUDE.md — TeamTimo

Jeu Unity développé par une équipe de 4 (Gabin, Kenny, Lucas, Nadir). Ce dépôt contient **le projet Unity** et **le Game Design Document** (vault Obsidian `GameDesign/`). Les notes et le code sont écrits en français ; réponds en français.

## 1. Aperçu technique
- **Moteur :** Unity **6000.3.28f1** (Unity 6.3 LTS) — physique : `Rigidbody.linearVelocity` (pas `velocity`).
- **Rendu :** URP 17.3 · VFX Graph · UI **UI Toolkit** (UXML/USS dans `Assets/UI/`, pas uGUI).
- **Le jeu :** survie / évasion **coopérative**. Des animaux-cobayes ont **10 jours in-game** pour vaincre les gardiens de 4 labyrinthes (zones A-D autour d'un Lobby-village), récupérer 4 fragments de clé et s'évader, pendant que le « Fermier » enlève chaque matin un PNJ du village. Jour = exploration / préparation ; nuit = mobs plus forts mais gardiens endormis. Résumé complet : `GameDesign/01_Vision/Pitch.md`.
- **Prototype actuel :** 3e personne, labyrinthe procédural en 4 zones, taureau chargeur, araignée, inventaire, cœurs, jour/nuit visuel. Plusieurs points de design sont encore à trancher (caméra, durée du jour…) : voir la section « Design à trancher » de `GameDesign/07_Production/Backlog.md`.
- **Packages clés :** Input System 1.20 (`PlayerInput` → Unity Events) · **Cinemachine 2.10** (API `CinemachineVirtualCamera` / `CinemachineFreeLook`, pas la v3) · AI Navigation 2.0 (NavMeshSurface/Modifier, zones NavMesh nommées) · Netcode for GameObjects 2.13 (installé, **non utilisé** pour l'instant).

## 2. Structure du dépôt
```
TeamTimo/
├── CLAUDE.md
├── Assets/
│   ├── Scripts/          # Tout le C# (à plat, + Items/). Pas de namespace, pas d'asmdef.
│   ├── Data item/        # ScriptableObjects ItemData (objets)
│   ├── Prefabs/          # Characters/ (Player, Bull, Spider), Items/, ...
│   ├── Scenes/           # Labyrinthe.unity (principale, seule dans les Build Settings) + scènes perso <Prénom>.unity
│   ├── Animations/       # + Characters/PlayerInput.inputactions
│   ├── Art/ Audio/ UI/ VFX/ URP/ Settings/
└── GameDesign/           # Vault Obsidian (ouvrir CE dossier dans Obsidian)
    ├── 00_Index.md               # Point d'entrée (MOC)
    ├── 00_Inbox/                 # Notes rapides à trier
    ├── 01_Vision/                # Pitch, piliers, boucle de gameplay (.canvas), glossaire
    ├── 02_Mecaniques/            # 1 note = 1 système de gameplay  ← le plus utile pour coder
    ├── 03_Contenu/               # Ennemis/ (Bestiaire), Boss/ (Gardiens), PNJ/, Personnages/, Objets/ (Catalogue), Niveaux/ (Lobby, Labyrinthe)
    ├── 04_Univers_et_Lore/
    ├── 05_Art_et_Audio/          # DA, UI/UX, moodboard
    ├── 06_Architecture_Tech/     # Vue_Ensemble_Code.md + Decisions/ (ADR)
    ├── 07_Production/            # Roadmap, Backlog (dont dette technique), Bugs/, Playtests/, Reunions/, Suivi_GDD.base
    ├── 98_Templates/             # Modèles de notes
    └── 99_Attachments/           # Images (Brainstorming/ = visuels du tableau blanc d'origine)
```
`GameDesign/` est **hors de `Assets/`** : Unity ne l'importe pas, aucun `.meta` à gérer.

## 3. Workflow GDD ↔ code
- **Avant de coder une fonctionnalité :** lis la note correspondante dans `GameDesign/02_Mecaniques/` ou `03_Contenu/` (et `06_Architecture_Tech/Vue_Ensemble_Code.md` pour la carte des scripts). Respecte les règles et les paramètres de tuning décrits.
- **Note en `draft` ou section « Intention de design » vide :** les choix de design appartiennent à l'équipe. Demande plutôt que d'inventer ; si tu dois avancer, note l'hypothèse dans « Questions ouvertes ».
- **Syntaxe Obsidian :** liens `[[Nom_De_Note]]` (résolus par nom de fichier, cherche-le avec Glob `GameDesign/**/Nom_De_Note.md`), propriétés YAML en frontmatter, callouts `> [!note]`, diagrammes Mermaid. Les `.canvas` sont du JSON (nœuds `text`/`file`/`group` + `edges`), les `.base` du YAML (vues de base de données Obsidian).
- **Frontmatter des notes de mécanique :**
  ```yaml
  type: mecanique
  status: draft | ready-to-code | in-progress | implemented | deprecated
  priority: high | medium | low
  owner: <prénom>
  scripts: [Assets/Scripts/....cs]   # chemins réels
  updated: AAAA-MM-JJ
  ```
- **Après avoir implémenté / modifié un système**, mets à jour sa note dans le même travail :
  1. `status` (et `updated` à la date du jour) ;
  2. la liste `scripts` et la section « Implémentation technique » ;
  3. le tableau « Paramètres de tuning » si des champs Inspector ont changé (nom, type, valeur par défaut) ;
  4. coche la tâche dans `07_Production/Backlog.md` si elle y figure.
- **Nouvelle note :** pars du template de `98_Templates/` (remplace `{{title}}` / `{{date}}`), garde les noms de fichiers en `Snake_Case` **sans accents**, et ajoute le lien dans `00_Index.md`.
- **Bug trouvé en passant :** ajoute-le au `Backlog.md` (section dette technique) ou crée une note `07_Production/Bugs/` — ne le corrige pas hors du périmètre demandé sans le signaler.
- Ne modifie jamais `GameDesign/.obsidian/` sauf demande explicite.

## 4. Conventions C# et Unity
- **Encapsulation :** `[SerializeField] private` plutôt que `public` (pas de `[SerializeField] public`). `[Header]` et `[Tooltip]` en français pour que les game designers puissent régler dans l'Inspector.
- **Données :** configuration (stats, objets, armes) dans des `ScriptableObject` ; les objets héritent de `ItemData` (`Assets/Scripts/Items/`).
- **Communication :** privilégie les `event Action<...>` C# (modèle existant : `PlayerHealth.OnSanteChangee`, `InventorySystem.OnInventoryChanged`) ; abonnement dans `OnEnable`, désabonnement dans `OnDisable`.
- **Performance :** aucun `GetComponent`, `Find`, `transform.Find` ou allocation dans `Update`/`FixedUpdate`/`LateUpdate`/coroutines par frame : cache dans `Awake`.
- **Input :** nouveau Input System uniquement (méthodes `public void X(InputAction.CallbackContext ctx)` liées dans `PlayerInput`). Pas de `Input.GetKey`.
- **Nommage :** le code existant mélange français et anglais ; pour un nouveau script, noms de classes/méthodes en anglais, commentaires et tooltips en français. Ne renomme pas l'existant sans demande (cela casse les références sérialisées).
- **Encodage :** fichiers `.cs` en **UTF-8 sans BOM**, fins de ligne CRLF (comme l'existant). Si un fichier arrive en Windows-1252 (accents affichés `�`), signale-le plutôt que de l'écraser.
- **Chaînes magiques à ne pas renommer :** zones NavMesh (`SouthWest`, `NorthWest`, `NorthEast`, `SouthEast`, `*Room`, `Lobby`, `WallTop`), éléments UXML (`HeartsContainer`, `DamageOverlay`, `InventoryContainer`), tags (`Player`, `Ramassable`), noms d'enfants VFX (`SprintSmoke`, `JumpSmoke`, `LandSmoke`).

## 5. Règles de sécurité sur les assets Unity
- **`.meta` :** si tu déplaces, renommes ou supprimes un fichier/dossier dans `Assets/`, fais de même avec son `.meta` (sinon les GUID et toutes les références sautent). Tu ne peux pas créer de `.meta` valide toi-même : laisse Unity le générer pour un nouveau fichier.
- **Ne modifie pas en texte brut** les `.unity`, `.prefab`, `.asset`, `.mat`, `.controller` sauf demande explicite ; préfère un script, un outil d'éditeur (`Assets/Scripts/Editor/`) ou des instructions pour l'Inspector.
- **Scènes :** chaque membre a sa scène de test (`Assets/Scenes/<Prénom>.unity`) ; `Labyrinthe.unity` est partagée.
- **Tu ne peux pas lancer Unity ni les tests :** après une modification de code, indique ce qu'il faut vérifier dans l'éditeur (Play Mode, Inspector, console).

## 6. Git
- Ignorés : `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`, `UIElementsSchema/`, l'état local d'Obsidian (`GameDesign/.obsidian/workspace*.json`), fichiers système.
- Versionnés : `Assets/` (avec les `.meta`), `Packages/`, `ProjectSettings/`, `GameDesign/` (dont la config partagée `.obsidian/*.json`).
- **Pas de Git LFS** pour l'instant : les binaires sont des fichiers git normaux (GitHub refuse les fichiers de plus de 100 Mo). N'ajoute pas de règle `filter=lfs` sans accord de l'équipe : le budget LFS du compte GitHub est bloqué (voir `Backlog.md`).
- **UnityYAMLMerge** est le pilote de fusion des fichiers YAML Unity (`.unity`, `.prefab`, `.asset`, `.mat`, `.meta`…). En cas de conflit sur ces fichiers, ne pas les fusionner à la main : signaler le conflit à l'utilisateur.
- `Assets/Art/Materials/Environment/MySkybox.mat` apparaît souvent modifié à cause de `DayNightCycle` (`[ExecuteAlways]`) : ne le commite pas sans raison.
- Ne commite / push que sur demande.
