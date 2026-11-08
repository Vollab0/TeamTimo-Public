---
type: production
tags: [production, gdd]
updated: 2026-11-08
---
# Questions ouvertes du GDD

Tous les points de design **encore flous** après la séance du 2026-11-08, classés par priorité. Chaque question renvoie à la note où elle est suivie (section « Questions ouvertes ») : c'est **là** qu'on coche la réponse, puis on la retire d'ici.

> [!info] Ce qui est déjà tranché
> Multijoueur 1-4 en ligne · 3e personne · jours de 18 min (70 % jour / 30 % nuit) · sauvegarde chaque matin · mort (à terre → résurrection chez le Médecin, gratuite si le corps est ramené, payante sinon ; sans Médecin, veillée à la grange ; séquelles de −½ cœur max (jamais sous ½ cœur) ; chirurgie +½ cœur ×2 ; prix des soins en `k × n²` ; Pharmacien Hérisson caché ; spectateur ; en solo, réveil à la grange) · fragments (raid de jour ou vol de nuit, porteur encombré) · gardiens Rat / Coq / Oie / Truie tirés au sort par zone, errants jusqu'à être vaincus · mobs mélangés partout, chaque mob a sa règle · 1 PNJ important enlevé tous les 2 jours + figurants chaque matin · or individuel, banque commune · Plume + Cartographe pour la carte · 3 emplacements d'inventaire · personnages cosmétiques (Renard, Poule, Mouton, Lapin) · lore seul conservé entre les parties.

## 🔴 Structurant — bloque le code ou plusieurs systèmes
- [ ] **Technologie réseau** (Relay / Lobby Unity, Steam…) et topologie hôte-client, à écrire dans un ADR — [[Multijoueur]]
- [ ] **Équilibrage selon le nombre de joueurs** : PV des gardiens, nombre de mobs, Loup face à un joueur seul, porter un corps seul — [[Multijoueur]], [[Loup]]
- [ ] **Contenu de la sauvegarde** : graine du labyrinthe, PNJ restants, inventaires, fragments, gardiens errants, corps au sol — [[Cycle_de_Partie]]
- [ ] **Le Lobby est-il dans la même scène que le labyrinthe ?** — [[Lobby]]
- [ ] **Gardien errant** : que deviennent ses mécaniques de raid hors de son arène ? Le vaincre rapporte-t-il quelque chose ? — [[Gardiens]]
- [ ] **Mécanique de raid de l'Oie** (la piste « alarme / cloches » n'a pas été retenue) — [[Gardiens]]
- [ ] **Indices** (porte de sortie, faiblesses des gardiens) : quelle forme (notes, gravures, cartes), combien en faut-il, qui les donne (labyrinthe, figurants) et comment éviter qu'un indice vital disparaisse avec un figurant ? — [[Fragments_et_Cle]], [[Enlevement_des_PNJ]], [[Univers]]
- [ ] **Échelle du labyrinthe** : 1 500 m de côté, est-ce cohérent avec ~13 min de jour et ~5 min de nuit ? — [[Generation_Labyrinthe]]
- [ ] **Arènes des gardiens** : les salles aléatoires actuelles (1 par zone) deviennent-elles les arènes, placées « au fond » de la zone ? — [[Generation_Labyrinthe]]

## 🟠 Systèmes à préciser
### Joueur
- [ ] Durée de l'état « à terre » ; peut-on ramper ou appeler à l'aide ? — [[Sante_et_Degats]]
- [ ] Porter un corps : ralentit-il comme un fragment, bloque-t-il l'inventaire ? — [[Sante_et_Degats]]
- [ ] Résurrection payante : prix (fixe ou croissant), qui paie (or porté ou banque commune), réapparition immédiate ou au matin ? — [[Sante_et_Degats]], [[Medecin]]
- [ ] Roulade : coût en stamina, invincibilité courte, touche ? La stamina sert-elle aussi à l'attaque ? — [[Deplacement_Joueur]]
- [ ] Frames d'invincibilité après un coup ? — [[Sante_et_Degats]]
- [ ] Inventaire : touche « utiliser », taille des piles, le bouclier occupe-t-il la main de l'arme ? — [[Inventaire]], [[Combat_Attaque]]
- [ ] Que perd exactement un joueur qui meurt, à part son or (son inventaire) ? — [[Economie]]

### Pharmacien (Hérisson, caché, jamais enlevé)
- [ ] Liste des cachettes possibles ; reste-t-il au même endroit une fois trouvé ? — [[Pharmacien]]
- [ ] Recettes (Baies + Herbes) et prix des remèdes ; force de la potion et du tonique — [[Pharmacien]]

### Carte et objectifs
- [ ] La Plume peut-elle se perdre ou s'user ? — [[Carte_et_Navigation]]
- [ ] Cartographe enlevé : chacun garde son croquis, sans fusion (hypothèse notée) ? Peut-on s'échanger les cartes ? — [[Carte_et_Navigation]], [[Cartographe]]
- [ ] La mise au propre chez le Cartographe est-elle payante ? — [[Carte_et_Navigation]]
- [ ] Les tags au Stylo / Craie sont-ils visibles par les autres joueurs ? — [[Carte_et_Navigation]]
- [ ] Les fragments récupérés se stockent-ils au Lobby (coffre du Lobby) ? — [[Fragments_et_Cle]]
- [ ] La Clé Maîtresse assemblée par le Forgeron est-elle encombrante comme un fragment ? — [[Fragments_et_Cle]]
- [ ] Peut-on sauver un PNJ enlevé ? — [[Enlevement_des_PNJ]]

### Mobs
- [ ] Quels sens pour chaque mob (vue, ouïe, vibrations…) ? — [[Discretion_et_Aggro]]
- [ ] Taupe : qu'est-ce qui produit une vibration (marcher, courir, sauter, s'accroupir) ? Indice visuel de son passage ? — [[Taupe]]
- [ ] Araignée : combien de toiles, destructibles par le feu ? — [[Araignee]]
- [ ] Loup : d'où viennent les renforts ? — [[Loup]]
- [ ] Aigle : une fois tombé, combat au sol ou mort ? Salive esquivable ou parable ? — [[Aigle]]
- [ ] Taureau : le joueur percuté prend-il dégâts et recul ? Signal de préparation de charge ? — [[Taureau]]
- [ ] La Souris est-elle un mob à part entière (ou seulement les renforts du Rat) ? — [[Bestiaire]], [[Gardiens]]

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
