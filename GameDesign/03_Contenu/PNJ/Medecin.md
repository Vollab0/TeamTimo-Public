---
type: pnj
status: draft
animal: Chat
stand: Tente du médecin
tags: [gdd, contenu, pnj]
updated: 2026-11-08
---
# Le Médecin

![[PNJ_Medecin.jpg|220]]

## Rôles
- **Soigner** les joueurs.
- **Ressusciter** un joueur mort (précisé le 2026-11-08) — [[Sante_et_Degats]] :
  - **gratuitement** si ses alliés lui **ramènent son corps** ;
  - **contre paiement** sinon : il va récupérer le corps lui-même, et le fait savoir — « Il va encore falloir que j'aille récupérer le corps moi-même… » Un allié paie avec son **or porté** ; le prix **augmente à chaque mort** de la partie. Le joueur réapparaît **tout de suite** au Lobby, mais **sans ses objets** (restés sur place jusqu'au matin) : « Je n'ai pas pu prendre ces objets avec moi. » (précisé le 2026-11-08)
- **Soigner les séquelles** (décidé le 2026-11-08) : il rend le **½ cœur max** perdu à chaque retour sans lui (veillée, réveil au matin, solo) ou relevé au kit — [[Sante_et_Degats]]. Le [[Pharmacien]] peut aussi le faire avec son Baume de séquelle.
- **Chirurgie** (décidé le 2026-11-08) : il vend des **améliorations permanentes pour la partie** : **+½ cœur max** par opération, **2 fois au maximum** (+1 cœur entier). On sort de son cabinet en se demandant ce qu'il nous a fait — [[Piliers_de_Design]] (« Mignon dehors, sombre dedans »).

## S'il est enlevé
- Les joueurs ne peuvent se soigner **qu'avec des baies**.
- Le **kit de réanimation** relève toujours un allié **à terre** (le [[Pharmacien]], une fois trouvé, en fabrique).
- Un joueur mort revient par la **veillée à la grange** (corps ramené) ou se réveille au matin en perdant or et inventaire — [[Sante_et_Degats]].
- Plus de **chirurgie** ; seul le **Baume de séquelle** du [[Pharmacien]] soigne encore les **séquelles**, qui s'accumulent.

## Prix des services
| Service | Prix |
|---|---|
| Soin (½ cœur de vie) | `k × n²`, n = demi-cœurs de vie actuels |
| Résurrection, corps ramené | gratuit |
| Résurrection, corps non ramené | croissant à chaque mort (base et hausse ?), en or porté |
| Soin d'une séquelle (½ cœur max) | `k × n²`, n = demi-cœurs de vie max actuels |
| Chirurgie (+½ cœur max, 2 fois max) | `k × n²`, n = demi-cœurs de vie max actuels |

**Loi de prix** (décidé le 2026-11-08) : `prix = k × n²`, où `n` est le nombre de demi-cœurs que le joueur a **avant** l'achat (sa vie actuelle pour un soin, sa vie max pour une séquelle ou une chirurgie). **Moins on a de cœurs, moins c'est cher.** `k` est à régler en playtest.

## Dialogues (idées)
- Résurrection payante : « Il va encore falloir que j'aille récupérer le corps moi-même… »
- Au retour du corps : « Je n'ai pas pu prendre ces objets avec moi. »

Voir : [[Sante_et_Degats]] · [[PNJ]] · [[Enlevement_des_PNJ]]
