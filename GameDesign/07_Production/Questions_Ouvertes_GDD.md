---
type: production
tags: [production, gdd]
updated: 2026-11-08
---
# Questions ouvertes du GDD

Tous les points de design **encore flous** après la séance du 2026-11-08, classés par priorité. Chaque question renvoie à la note où elle est suivie (section « Questions ouvertes ») : c'est **là** qu'on coche la réponse, puis on la retire d'ici.

> [!info] Ce qui est déjà tranché
> Multijoueur 1-4 en ligne · 3e personne · jours de 18 min (70 % jour / 30 % nuit) · sauvegarde chaque matin · mort (à terre → résurrection chez le Médecin, gratuite si le corps est ramené, payante sinon ; sans Médecin, veillée à la grange ; séquelles de −½ cœur max (jamais sous ½ cœur) ; chirurgie +½ cœur ×2 ; prix des soins en `k × n²` ; Pharmacien Hérisson caché ; spectateur ; en solo, réveil à la grange) · fragments (raid de jour ou vol de nuit, porteur encombré) · gardiens Rat / Coq / Oie / Truie tirés au sort par zone, errants jusqu'à être vaincus · mobs mélangés partout, chaque mob a sa règle · 1 PNJ important enlevé tous les 2 jours + figurants chaque matin · or individuel, banque commune · Plume + Cartographe pour la carte · 3 emplacements d'inventaire · personnages cosmétiques (Renard, Poule, Mouton, Lapin) · lore seul conservé entre les parties.
> **Ajouts du 2026-11-08 (2e séance de questions) :** raid de l'Oie (œufs → oisons, l'approcher sans être vue) · gadoue glissante chez la Truie · gardien errant = poursuivant brut très fort, aucune récompense en plus · arènes placées au hasard dans la zone · indices vitaux dans le labyrinthe, figurants = raccourcis · Lobby dans la même scène · échelle fixée en playtest · seul le nombre de mobs s'adapte au nombre de joueurs · à terre ~30 s (ramper, appeler) · corps encombrant comme un fragment · résurrection payante croissante, en or porté, immédiate, sans les objets · or tombé disparaît en 15 s · objets gardés si le corps est ramené par les joueurs · roulade = stamina + i-frames, stamina hors attaque · socle des fragments dans la grange · clé forgée normale / bricolée encombrante · PNJ sauvables (cages, rare) · Taureau : 1 cœur + recul, gratte le sol avant de charger · Aigle tombé = mort · Taupe : courir / sauter / atterrir, monticules · **Souris** = mob de base dans toutes les zones.

## 🔴 Structurant — bloque le code ou plusieurs systèmes
- [ ] **Technologie réseau** (Relay / Lobby Unity, Steam…) et topologie hôte-client, à écrire dans un ADR — [[Multijoueur]]
- [ ] **Contenu de la sauvegarde** : graine du labyrinthe, PNJ restants, inventaires, fragments, gardiens errants, corps au sol, objets au sol — [[Cycle_de_Partie]]
- [ ] **Forme des indices du labyrinthe** (gravures, notes, cartes partielles) et combien en faut-il ; quels raccourcis donnent les figurants ? — [[Fragments_et_Cle]], [[Enlevement_des_PNJ]], [[Univers]]
- [ ] **Échelle du labyrinthe** : à mesurer en playtest (temps de traversée d'une zone) — [[Generation_Labyrinthe]]
- [ ] **Arènes à placement aléatoire** : distance minimale au Lobby, comment les repérer — [[Generation_Labyrinthe]], [[Gardiens]]
- [ ] **Cages de PNJ** : combien, qui (importants / figurants), gardées ou verrouillées, ré-enlèvement possible ? — [[Enlevement_des_PNJ]]

## 🟠 Systèmes à préciser
### Joueur
- [ ] Résurrection payante : prix de base et hausse par mort ; compteur par joueur ou pour l'équipe ? — [[Sante_et_Degats]], [[Medecin]]
- [ ] En solo, le réveil au matin fait-il perdre les objets (règle générale) ou seulement l'or ? — [[Sante_et_Degats]]
- [ ] Roulade : quelle touche ? Produit-elle une vibration pour la Taupe ? — [[Deplacement_Joueur]], [[Taupe]]
- [ ] Frames d'invincibilité après un coup ? — [[Sante_et_Degats]]
- [ ] Inventaire : touche « utiliser », taille des piles, le bouclier occupe-t-il la main de l'arme ? — [[Inventaire]], [[Combat_Attaque]]

### Pharmacien (Hérisson, caché, jamais enlevé)
- [ ] Liste des cachettes possibles ; reste-t-il au même endroit une fois trouvé ? — [[Pharmacien]]
- [ ] Recettes (Baies + Herbes) et prix des remèdes ; force de la potion et du tonique — [[Pharmacien]]

### Carte et objectifs
- [ ] La Plume peut-elle se perdre ou s'user ? — [[Carte_et_Navigation]]
- [ ] Cartographe enlevé : chacun garde son croquis, sans fusion (hypothèse notée) ? Peut-on s'échanger les cartes ? — [[Carte_et_Navigation]], [[Cartographe]]
- [ ] La mise au propre chez le Cartographe est-elle payante ? — [[Carte_et_Navigation]]
- [ ] Les tags au Stylo / Craie sont-ils visibles par les autres joueurs ? — [[Carte_et_Navigation]]

### Mobs
- [ ] Quels sens pour la Souris, l'Araignée, le Loup et l'Aigle ? — [[Discretion_et_Aggro]]
- [ ] Souris : règle propre ou volontairement simple ? Seule ou en groupe ? — [[Souris]]
- [ ] Oie : nombre d'œufs, stats de l'oison, plus d'œufs = ? ; Truie : glisse-t-elle aussi dans la gadoue ? — [[Gardiens]]
- [ ] Araignée : combien de toiles, destructibles par le feu ? — [[Araignee]]
- [ ] Loup : d'où viennent les renforts ? Attaque-t-il toujours un joueur seul ? — [[Loup]]
- [ ] Aigle : salive esquivable ou parable ? — [[Aigle]]
- [ ] Taureau : 1 s de préparation suffit-elle pour rouler ? — [[Taureau]]
- [ ] Courbe du nombre de mobs selon le nombre de joueurs — [[Apparition_Ennemis]], [[Multijoueur]]

### Multijoueur
- [ ] Rejoindre une partie en cours, reprendre une sauvegarde avec un joueur en moins ? — [[Multijoueur]]
- [ ] Chat vocal intégré ? (lié au Talkie-Walkie) — [[Multijoueur]], [[Catalogue_Objets]]
- [ ] Deux joueurs peuvent-ils prendre le même animal (variantes de couleur) ? — [[Personnages_Jouables]]

## 🟡 Lore et narration
- [ ] Qui sont les anciens bataillons ? — [[Univers]]
- [ ] Pourquoi les gardiens gardent-ils les fragments (le pilier les retient, les nourrit, les apaise ?) — [[Univers]]
- [ ] Que montre exactement la révélation finale, et quels morceaux dépendent du lore débloqué ? — [[Univers]]
- [ ] Que débloque-t-on entre les parties, et où le consulte-t-on (menu d'archive) ? — [[Cycle_de_Partie]], [[UI_UX]]

## 🟢 Chiffres et équilibrage (à régler en playtest)
- [ ] PV des mobs, dégâts des armes, dégâts des mobs vs cœurs du joueur — [[Combat_Attaque]]
- [ ] Or par mob, par coffre ; prix des objets (dont la Plume) ; coefficient `k` des soins, prix de la résurrection payante (même loi ?) et de l'assemblage de la clé — [[Economie]], [[Catalogue_Objets]]
- [ ] Nombre de figurants au départ et enlevés par matin — [[Enlevement_des_PNJ]]
- [ ] Proportions de chaque mob (l'Aigle rare) — [[Bestiaire]]
- [ ] Quels objets ont une durabilité ? — [[Catalogue_Objets]]
- [ ] Rareté de la Plume et des Herbes — [[Catalogue_Objets]]

## ⚪ Production et technique
- [ ] Algorithme : garder le parcours en profondeur ou passer au tile-based / WFC ? Graine partagée ? Correspondance zones A-D ↔ quadrants SW/NW/NE/SE ? — [[Generation_Labyrinthe]]
- [ ] La vue 1re personne du prototype : la retirer ou la garder pour le debug ? — [[Deplacement_Joueur]]
- [ ] Le Bâton manque dans la liste de modélisation du brainstorming : oubli ? — [[Catalogue_Objets]]
- [ ] Illustrations à faire : Lapin, Oie, Truie — [[Direction_Artistique]]
- [ ] Ambiance visuelle de chaque zone (indépendante du gardien, tiré au sort) — [[Labyrinthe]]

## Notes qui pourraient passer en `ready-to-code`
Proposition, à valider en équipe : leur intention de design est maintenant tranchée, il ne reste que des détails ou des chiffres.
- [[Cycle_de_Partie]] · [[Enlevement_des_PNJ]] · [[Fragments_et_Cle]] · [[Carte_et_Navigation]] · [[Economie]]

Voir aussi : [[Backlog]] · [[Roadmap]]
