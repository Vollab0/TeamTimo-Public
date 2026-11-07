---
type: mecanique
status: in-progress
priority: medium
owner:
scripts:
  - Assets/Scripts/InventorySystem.cs
  - Assets/Scripts/InventoryUI.cs
  - Assets/Scripts/Items/ItemData.cs
  - Assets/Scripts/Items/WeaponItem.cs
  - Assets/Scripts/Items/ConsumableItem.cs
tags: [gdd, mecanique, inventaire]
updated: 2026-11-07
---
# Inventaire

## 1. Intention de design
Inventaire de survie **à slots limités** : ce qu'on emporte est un choix. Le **fragment de clé** le rend encore plus contraignant.

## 2a. Règles — design (brainstorming)
- **Slots limités** ; le **Sac** (épique) ajoute un slot.
- **Stacking** pour les consommables (cailloux, baies, allumettes…).
- **Mécanique du fragment** : l'objet « Fragment » **bloque tous les slots** et **réduit la vitesse de déplacement** (encombrement, à confirmer) — [[Fragments_et_Cle]].
- **Durabilité** des objets (si applicable), réparée par le [[Forgeron]].
- Objets revendables à l'[[Arnaqueur]] ; coffre du Lobby améliorable pour stocker — [[Lobby]].
- Catalogue des 21 objets : [[Catalogue_Objets]].

## 2b. Règles et comportement (état actuel du code)
- **Barre rapide** de `inventorySize` emplacements (5).
- **Ajout :** premier emplacement libre ; refusé si la barre est pleine.
- **Sélection :** molette (action `Scroll`), boucle en fin de barre.
- **Objet en main :** l'objet du slot sélectionné est affiché dans la main (voir [[Ramassage_Objets]]).
- **Données :** chaque objet est un `ItemData` (ScriptableObject) : `nameObject`, `icone`, `prefab`, méthode virtuelle `Utiliser(joueur)`.
  - `WeaponItem` : `damage` (menu *Inventory/Weapon*)
  - `ConsumableItem` : `healthRestore` (menu *Inventory/Health*)
- **Pas encore implémenté :** utiliser un objet. `Utiliser()` n'est surchargée par aucune sous-classe et aucune action d'input ne l'appelle.

## 3. Paramètres de tuning
| Paramètre | Type | Défaut | Rôle |
|---|---|---|---|
| `inventorySize` | int | 5 | Nombre d'emplacements |
| `InventoryUI.containerName` | string | `InventoryContainer` | Élément UI Toolkit hôte |

## 4. Feedback
- UI Toolkit générée dynamiquement par `InventoryUI`, slot sélectionné surligné.

## 5. Liens
- Dépend de : [[Ramassage_Objets]]
- Contenu : [[Catalogue_Objets]]
- Utilisé par : [[Combat_Attaque]], [[Sante_et_Degats]]

## 6. Implémentation technique
- **Événements :** `OnInventoryChanged`, `OnSelectionChanged(int)`
- **ScriptableObjects :** `Assets/Data item/`

## 7. Questions ouvertes
- [ ] Quelle touche pour « utiliser » un objet ?
- [ ] Taille max d'une pile (stack) ?
- [ ] Nombre de slots de départ : 5 (prototype) ?
- [ ] À faire dans le code : stacking, `Utiliser()` par type d'objet, slots bloqués par le fragment, slot bonus du Sac, durabilité.
