---
type: reference
tags: [gdd, vision]
updated: 2026-11-08
---
# Glossaire

Vocabulaire commun à l'équipe (et à Claude Code). Le code mélange français et anglais : ce tableau fait le lien.

## Termes de design
| Terme | Définition |
|---|---|
| **Le Fermier** | Scientifique qui retient les animaux-cobayes. Enlève des PNJ chaque matin — [[Univers]] |
| **La Moisson** | Les 10 jours de la partie ; après le 10e coucher de soleil, il ne reste plus personne — [[Cycle_de_Partie]] |
| **Lobby / Village** | Zone sûre au centre de la carte, avec les PNJ — [[Lobby]] |
| **Grange** | Bâtiment du Lobby où apparaissent les joueurs, et où l'on dort pour sauter la nuit |
| **Zone (A, B, C, D)** | Un des 4 petits labyrinthes interconnectés, chacun tenu par un gardien — [[Labyrinthe]] |
| **Salle des Gardiens** | Arène non procédurale au fond d'une zone, gardée par un boss — [[Gardiens]] |
| **Gardien / Boss** | Boss d'une zone ; combat en « raid » — [[Gardiens]] |
| **Fragment de clé** | Pièce de la clé, gardée par un gardien ; c'est un **pilier** qui retient le boss — [[Fragments_et_Cle]] |
| **Clé Maîtresse** | Clé réassemblée à partir des 4 fragments, ouvre la sortie |
| **Malédiction du porteur** | Le porteur d'un fragment est ralenti et a ses slots bloqués ; il peut le poser ou le passer — [[Fragments_et_Cle]] |
| **Gardien errant** | Gardien réveillé par le vol de son fragment : il erre dans le labyrinthe jusqu'à être vaincu — [[Gardiens]] |
| **À terre** | État d'un joueur à 0 cœur, relevable au kit pendant quelques secondes avant la mort — [[Sante_et_Degats]] |
| **Résurrection** | Chez le Médecin : gratuite si l'équipe ramène le corps, payante sinon — [[Medecin]] |
| **Veillée** | Sans Médecin : ramener le corps d'un allié à la grange et y dormir pour qu'il se réveille au matin — [[Sante_et_Degats]] |
| **Séquelle** | −½ cœur max pour un joueur revenu sans le Médecin ou relevé au kit (jamais sous ½ cœur) ; soignée par le Médecin ou le Baume du Pharmacien — [[Sante_et_Degats]] |
| **Chirurgie** | +½ cœur max vendu par le Médecin, 2 fois au maximum — [[Medecin]] |
| **PNJ semi-important** | Le [[Pharmacien]] (Hérisson), caché dans le village : il faut le trouver ; jamais enlevé |
| **Figurant** | Villageois sans service ; ses dialogues donnent rumeurs et indices. Enlevé lui aussi au fil des matins — [[Enlevement_des_PNJ]] |
| **Indices des anciens bataillons** | Traces laissées dans le labyrinthe ; mènent à la porte de sortie et s'archivent entre les parties — [[Univers]] |
| **PNJ (important)** | Habitant du village offrant un service ; 1 enlevé tous les 2 jours — [[PNJ]] |
| **Mob / Mob de nuit** | Monstre du labyrinthe ; sa variante de nuit est plus forte — [[Bestiaire]] |
| **Pièces d'or** | Monnaie (monstres, coffres) — [[Economie]] |
| **Aggro** | Cible actuelle d'un monstre — [[Discretion_et_Aggro]] |

## Correspondance design ↔ code
| Terme (design) | Dans le code | Définition |
|---|---|---|
| Lobby | `Lobby` (zone NavMesh), `GetLobby()` | Salle centrale du labyrinthe |
| Zone | `SouthWest`, `NorthWest`, `NorthEast`, `SouthEast` | Un des 4 quadrants (A/B/C/D : correspondance à fixer) |
| Salle | `Room`, `*Room` | Espace ouvert dans une zone (future salle des gardiens ?) |
| Cœur / demi-cœur | `nombreDeCoeurs`, `vieActuelle` | 1 cœur = 2 PV |
| Blesser / Soigner | `PlayerHealth.Blesser()` / `Soigner()` | Infliger / rendre des PV |
| Taureau | `Bull`, `BullAI` | Mob chargeur — [[Taureau]] |
| Araignée | `Spider`, `EnemySight` | Mob tisseur de toiles — [[Araignee]] |
| Objet (données) | `ItemData` (ScriptableObject) | Définition d'un objet |
| Objet (au sol) | `ItemWorld` | Instance ramassable dans la scène |
| Vague | `WaveSettings` | Groupe d'ennemis à faire apparaître — [[Apparition_Ennemis]] |
| Rail | `_isAxisX`, `_fixedAxisCoordinate` | Axe de couloir sur lequel le taureau est verrouillé |
