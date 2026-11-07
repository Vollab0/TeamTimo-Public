# TeamTimo

> Des petits animaux mignons, cobayes d'un « Fermier » scientifique, ont **10 jours** pour vaincre les gardiens de 4 labyrinthes, réassembler une clé et s'évader, pendant que le Fermier enlève chaque matin les habitants de leur village.

Jeu de survie / évasion **coopératif** développé sous Unity 6.

## Le jeu en bref
1. Les joueurs (Renard, Poule, Mouton) apparaissent dans la **grange** du Lobby, un village sûr peuplé de PNJ marchands et artisans.
2. **Le jour**, ils explorent les 4 zones du labyrinthe, récupèrent de l'or et des objets, et s'équipent au village.
3. **La nuit**, les monstres sont plus forts, mais les gardiens dorment : c'est le moment de voler les **fragments de clé**.
4. **Chaque matin**, le Fermier enlève un PNJ : son service disparaît pour le reste de la partie.
5. Avec les 4 fragments, le Forgeron reconstruit la **Clé Maîtresse** : il faut ouvrir la porte de sortie avant la fin du 10e jour.

- ✅ **Victoire :** clé assemblée + sortie ouverte avant le coucher du soleil du jour 10.
- ❌ **Défaite :** fin du jour 10, ou toute l'équipe morte.

**Inspirations :** *Le Labyrinthe* (film), *L'Attaque des Titans* (anime), *Lethal Company*, *Content Warning*.

Game Design Document complet : [`GameDesign/00_Index.md`](GameDesign/00_Index.md) · Pitch : [`GameDesign/01_Vision/Pitch.md`](GameDesign/01_Vision/Pitch.md)

## Démarrer
- **Unity :** ouvrir la racine du dépôt avec Unity **6000.2.7f2**. Scène principale : `Assets/Scenes/Labyrinthe.unity`.
- **Game Design Document :** dans Obsidian, *Ouvrir un dossier comme coffre* → choisir `GameDesign/`. Point d'entrée : `GameDesign/00_Index.md`.
- **Claude Code :** les conventions du projet et le workflow GDD ↔ code sont décrits dans [`CLAUDE.md`](CLAUDE.md).

## Configuration git — à faire une fois par membre de l'équipe

### UnityYAMLMerge (recommandé)
Fusion intelligente des scènes et prefabs en cas de conflit. Adapter le chemin à votre installation d'Unity :
```bash
git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.2.7f2/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
```
Sans cette configuration, rien ne casse : git fusionne simplement de façon classique.

### Fichiers lourds
Le projet **n'utilise pas Git LFS** pour l'instant : textures, modèles et sons sont des fichiers git normaux. GitHub refuse tout fichier de plus de **100 Mo** ; évitez aussi de pousser des fichiers de travail volumineux (exports temporaires, vidéos de capture, `.blend` de sauvegarde).
