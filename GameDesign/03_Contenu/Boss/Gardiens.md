---
type: boss
status: draft
priority: high
owner:
scripts: []
tags: [gdd, contenu, boss]
updated: 2026-11-08
---
# Les Gardiens (boss de raid)

![[Boss_Rat.jpg|200]] ![[Boss_Coq.jpg|200]]
*Oie et Truie : pas encore d'illustration.*

## Rôle dans le jeu
Chaque zone du labyrinthe se termine par une **Salle des Gardiens** où un boss garde un **fragment de clé** — [[Fragments_et_Cle]]. Chaque gardien a **sa propre mécanique de raid** (les thèmes sensoriels par zone ont été abandonnés le 2026-11-08).

## Origine (lore)
Les gardiens sont **eux aussi des cobayes**, transformés par les expériences du Fermier : tragiques plutôt que méchants (décidé le 2026-11-08) — [[Univers]].

## Règles (design)
- **Mécanique de raid :** le combat n'est pas une simple bagarre. C'est un raid unique qui demande une **stratégie propre au gardien** (ex. : détourner la Truie avec de la nourriture) — [[Discretion_et_Aggro]].
- **Deux voies pour le fragment (décidé le 2026-11-08) :** le **vaincre de jour** en raid, ou **voler le fragment de nuit** pendant son sommeil — [[Fragments_et_Cle]].
- **Stats et préparation :** l'affronter sans l'équipement adéquat est **suicidaire** — [[Combat_Attaque]], [[Economie]].
- **Arènes non procédurales**, conçues à la main pour favoriser les stratégies de raid — [[Generation_Labyrinthe]].
- **La nuit** — [[Cycle_Jour_Nuit]] :
  - les gardiens **s'endorment** et sont **plus faibles** ;
  - le fragment est en fait **un pilier qui retient le boss** : les joueurs peuvent s'en approcher pendant son sommeil ;
  - **enlever le pilier réveille le boss**, qui peut alors **se promener partout** dans le labyrinthe.
  - **Menace permanente (décidé le 2026-11-08) :** il ne se rendort pas et erre le reste de la partie **jusqu'à ce qu'on le vainque**. Il **n'entre pas** dans le [[Lobby]], qui reste sûr.
  - **Errant = poursuivant brut (décidé le 2026-11-08) :** hors de son arène, il **perd sa mécanique de raid** (pas de terriers, d'œufs ni de gadoue dans les couloirs) mais devient un chasseur **vraiment fort** : on le vainc à la loyale, ce qui demande de l'équipement et du monde.
  - **Récompense (décidé le 2026-11-08) :** vaincre un gardien, en raid ou errant, **ne rapporte rien de plus** que le fragment (en raid) ou la fin de la menace (errant).
  - Ex. tableau « Nuit » : *le taureau dort mais se réveille quand le fragment est récupéré* (illustration du principe ; le Taureau n'est plus gardien).

    ![[Boss_Taureau_Endormi.jpg|260]]
- Musique dédiée : thème Boss / Poursuite.

## Les 4 gardiens (décidé le 2026-11-08)
Le Taureau est sorti de la liste (il reste un mob — [[Taureau]]) ; l'Oie et la Truie le remplacent et complètent la liste.

**Répartition :** les 4 gardiens sont **redistribués aléatoirement entre les 4 zones à chaque partie** (décidé le 2026-11-08). Chaque gardien garde **son arène** conçue à la main, **placée à un endroit aléatoire de la zone** qu'il occupe (précisé le 2026-11-08 : il faut la chercher) ; le décor des zones ne peut donc pas dépendre du gardien.

| Gardien | Description | Zone | Piste de mécanique de raid |
|---|---|---|---|
| **Le Rat** | Chef du groupe des souris | aléatoire | **Terriers à boucher** (décidé le 2026-11-08) : il disparaît dans les trous de son arène et ressort ailleurs avec des vagues de souris ; il faut boucher les terriers un à un pour le coincer |
| **Le Coq** | Chef du poulailler | aléatoire | **Chant assourdissant** (décidé le 2026-11-08) : son cocorico est une onde qui étourdit et repousse ; on s'abrite derrière des obstacles pendant qu'il chante, puis on frappe |
| **L'Oie** | Cheffe de la basse-cour, sentinelle hargneuse | aléatoire | **Couveuse** (décidé le 2026-11-08) : de **gros œufs** sont répartis dans toute l'arène ; quand elle **voit** un joueur, elle crie et fait **éclore un œuf**, d'où sort un **oison** qui attaque. Il faut **l'approcher sans être vu** (discrétion) pour la frapper |
| **La Truie** | Cheffe de la porcherie, énorme et gloutonne | aléatoire | **Gloutonne** (validé le 2026-11-08) : elle chasse à l'odeur ; on la détourne avec de la nourriture (Tomates) pour atteindre ses points faibles. **Sol de gadoue glissant** dans son arène (ajouté le 2026-11-08) : on dérape, difficile de s'arrêter ou de fuir |

## Implémentation technique
- « IA des Boss : scripter les phases spécifiques des 4 boss de salles » (liste de tâches).
- Phases à prévoir : endormi (nuit) / gardien (jour, dans l'arène, mécanique de raid) / réveillé et errant (après vol du fragment : IA de poursuite commune aux 4 gardiens, seules les stats changent).
- Oison de l'Oie : petit mob invoqué, à prévoir comme prefab à part — [[Bestiaire]].

## Questions ouvertes
- [x] Le 4e gardien ? → **Rat, Coq, Oie, Truie** (2026-11-08)
- [x] Le Taureau-boss et le Taureau-mob coexistent-ils ? → **non**, le Taureau n'est qu'un mob (2026-11-08)
- [x] Quel gardien dans quelle zone ? → **aléatoire à chaque partie** (2026-11-08)
- [x] Mécaniques de raid du Rat et du Coq → terriers à boucher / chant assourdissant (2026-11-08)
- [x] Pistes de raid → **Truie validée**, **Oie à retravailler** (2026-11-08)
- [x] Mécanique de raid de l'Oie → **couveuse** : œufs dans l'arène, elle en fait éclore un (oison) quand elle voit un joueur ; l'approcher sans être vu (2026-11-08)
- [x] Que deviennent ces mécaniques quand le gardien erre hors de son arène ? → **aucune** : poursuivant brut, mais très fort (2026-11-08)
- [x] Un gardien réveillé et errant peut-il être tué ? Se rendort-il ? → **peut être vaincu, ne se rendort pas** (2026-11-08)
- [x] Vaincre un gardien errant rapporte-t-il quelque chose ? → **rien de plus** (2026-11-08)
- [ ] Oie : nombre d'œufs, stats de l'oison ; que se passe-t-il quand il n'y a plus d'œufs ? Combien de coups pour la vaincre (approches discrètes répétées) ?
- [ ] Truie : la gadoue fait-elle glisser la Truie aussi (la faire déraper dans un mur) ?
- [ ] Arènes à placement aléatoire : distance minimale au Lobby ? Comment le joueur la repère-t-il (indices, bruits) ?
