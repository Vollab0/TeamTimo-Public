---
type: mecanique
status: implemented
priority: medium
owner:
scripts:
  - Assets/Scripts/GrabObject.cs
  - Assets/Scripts/Items/ItemWorld.cs
  - Assets/Scripts/GameManager.cs
tags: [gdd, mecanique, inventaire]
updated: 2026-11-08
---
# Ramasser / lâcher un objet

## 1. Intention de design
Le lancer d'objet sert la **diversion** : Tomate et Sable changent l'aggro, Cailloux font du bruit, Réveil attire tous les monstres, Allumettes peuvent être jetées au sol pour éclairer — voir [[Discretion_et_Aggro]] et [[Catalogue_Objets]].
Le ramassage concerne aussi le **loot des coffres** et des mobs ([[Economie]]) et le **corps d'un allié mort** à ramener au [[Medecin]] (confirmé le 2026-11-08 — [[Sante_et_Degats]]).

## 2. Règles et comportement (état actuel du code)
- **Détection :** l'objet visé à moins de `detectionRange` est surligné (matériau `GameManager.globalOutlineMaterial`).
- **Input `Grab` :**
  - si un objet `ItemWorld` est visé → il est ajouté à l'[[Inventaire]] puis retiré du monde ;
  - sinon, si un objet est tenu → il est **lancé** devant la caméra (vitesse 5 + vitesse du joueur), retrouve sa physique, le tag `Ramassable` et son `ItemWorld`.
- L'objet du slot sélectionné est affiché dans la main (`objectHolder`, `handOffset`).

## 3. Paramètres de tuning
| Paramètre | Type | Défaut | Rôle |
|---|---|---|---|
| `detectionRange` | float | 2 | Portée de ramassage |
| `handOffset` | Vector3 | (0, 1.5, 2) | Position de l'objet tenu |
| `handRotation` | Vector3 | (0, 0, 0) | Rotation de l'objet tenu |
| `showDebugGizmos` | bool | true | Gizmo de portée |

## 5. Liens
- [[Inventaire]] · [[Catalogue_Objets]]

## 6. Implémentation technique
- **Scripts :** `GrabObject.cs`, `ItemWorld.cs` (référence vers l'`ItemData`), `GameManager.cs` (matériau d'outline global)
