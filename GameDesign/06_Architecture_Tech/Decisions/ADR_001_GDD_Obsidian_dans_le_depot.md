---
type: decision
status: acceptee
date: 2026-11-07
decideurs: [Gabin]
tags: [tech, decision]
---
# ADR 001 — Le GDD vit dans le dépôt, sous forme de vault Obsidian

## Contexte
Le design était dispersé (discussions, tête des membres) et Claude Code n'y avait pas accès. Le code et les intentions de design dérivent l'un de l'autre.

## Options envisagées
1. **Google Docs / Notion** — collaboratif en temps réel, mais hors du dépôt : pas d'historique commun avec le code, illisible pour Claude Code.
2. **Vault Obsidian dans `GameDesign/`, versionné avec git** — Markdown lisible par tous les outils, historique aligné sur le code, liens entre notes.

## Décision
Option 2. Le vault est à la racine du dépôt, **hors de `Assets/`** (Unity ne l'importe pas, pas de `.meta`).

## Conséquences
- Positives : un commit peut contenir à la fois le code et la mise à jour de la note de design ; Claude Code lit et met à jour les notes (voir `CLAUDE.md`).
- Négatives : pas d'édition simultanée en temps réel ; conflits git possibles sur une même note → une note = un sujet, notes courtes.
- La config partagée d'Obsidian (`.obsidian/*.json`) est versionnée, mais pas l'état local (`workspace.json`).
