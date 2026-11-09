# TeamTimo

> Cute little animals, lab rats of a mad-scientist "Farmer", have **10 days** to defeat the guardians of
> four mazes, rebuild a key and escape — while every morning the Farmer kidnaps one of their villagers.

A **co-op survival / escape game** made with **Unity 6** (URP, C#) by a team of friends.
**Work in progress** — in particular, the online multiplayer has to be rebuilt.

## The game in short

1. Players (Fox, Hen, Sheep) start in the **barn** of the Lobby, a safe village of merchant and craftsman NPCs.
2. **By day**, they explore the four zones of the maze, collect gold and items, and gear up in the village.
3. **By night**, monsters get stronger but the guardians sleep: the time to steal the **key fragments**.
4. **Every morning**, the Farmer kidnaps an NPC, whose service is lost for the rest of the run.
5. With the four fragments, the Blacksmith forges the **Master Key**: open the exit before the end of day 10.

The full Game Design Document (in French) lives in [`GameDesign/`](GameDesign/00_Index.md), an Obsidian vault —
start with the [pitch](GameDesign/01_Vision/Pitch.md).

## Technical highlights

| System | Scripts |
| --- | --- |
| **Procedural maze** generated at runtime: four zones around the Lobby, with doors between zones | `MazeGenerator`, `MazeCell` |
| **NavMesh baked at runtime** on the generated maze so enemies can find their way through it | `MazeNavMeshBuilder` |
| **Enemy AI**: field-of-view detection, a bull that prepares and executes charges, enemy spawning | `EnemySight`, `BullAI`, `SpawnerManager` |
| **Day/night cycle** driving the game rules | `DayNightCycle` |
| Procedural **terrain** generation | `TerrainGenerator` |
| **Inventory** with items as ScriptableObjects (weapons, consumables) | `InventorySystem`, `InventoryUI`, `Items/*` |
| Player controller, health, damage, **ragdoll** on death, heart-based health UI | `PlayerController`, `PlayerHealth`, `DamageDealer`, `RagdollController`, `HeartUIManager` |
| Online co-op with **Netcode for GameObjects** | to be rebuilt — not functional yet |

**Game design ↔ code workflow:** the GDD in `GameDesign/` is the source of truth; [`CLAUDE.md`](CLAUDE.md)
describes how we use Claude Code to keep the design documents and the C# code in sync.

## Team

| Member | Main contributions |
| --- | --- |
| **Gabin Mondongou** ([@Vollab0](https://github.com/Vollab0)) | Lead developer (96 of 130 commits): procedural maze and runtime NavMesh, enemy vision, day/night cycle, terrain, inventory and items, player systems and UI, multiplayer, Game Design Document, git workflow |
| [@Albrosss](https://github.com/Albrosss) | Object grabbing, item data, game manager |
| [@w4lou](https://github.com/w4lou) | Bull AI, enemy spawner |
| [@luls302](https://github.com/luls302), [@Mahtze](https://github.com/Mahtze), [@Eskioi](https://github.com/Eskioi) | Contributions to the project |

## About this repository

This is the **public version** of our team repository. Its history was rewritten to remove third-party assets
that may not be redistributed (Unity Asset Store packs, Mixamo characters and animations). Everything else —
code, scenes, prefabs, our own models and textures, and the commit history — is preserved.

Because of that, some models and materials appear **missing (pink)** when the project is opened. To restore
them, import these free packs from the Unity Asset Store:

- *Stylized Farm Asset Pack - Small*
- *Low Poly Medieval Market Stalls*
- *Stylized Low Poly Nature Lite*
- *free Storage House*

## Open the project

- Open the repository root with **Unity 6000.3.28f1** (Unity 6.3 LTS). Main scene: `Assets/Scenes/Labyrinthe.unity`.
- Game Design Document: in Obsidian, *Open folder as vault* → `GameDesign/`.

## Credits

- Stylized textures: [3dtextures.me](https://3dtextures.me/) (CC0).
- Font: [Lilita One](https://fonts.google.com/specimen/Lilita+One) (SIL Open Font License).
