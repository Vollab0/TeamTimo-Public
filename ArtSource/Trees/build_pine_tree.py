# Construit les sapins stylisés (SM_Tree_Pine_01 à 04) et les exporte en FBX pour Unity.
# À lancer dans Blender 5.x (Scripting > Run Script, ou via le MCP), dans un fichier vide.
#
# Style inspiré des sapins de RV There Yet? : branches en « frondes » d'aiguilles découpées
# (cartes alpha, texture T_PineFrond générée par generate_pine_frond.py), étages irréguliers qui
# retombent, intérieur sombre (cœur autour du tronc). Une recette, plusieurs essences (VARIANTS).
#
# Conventions lues par le shader TeamTimo/StylizedFoliage (matériau du feuillage) :
#   - UV0 : atlas T_PineFrond (u 0-0,8 = branche, tige à u = 0,4 ; u 0,8-1 = zone pleine) ;
#   - couleur de sommet (export LINEAR) : R = occlusion (0 cœur / près du tronc, 1 bout des
#     branches), G = hauteur normalisée dans le feuillage, B = aléa par branche,
#     A = souplesse au vent (0 près du tronc, 1 au bout des branches) ;
#   - normales personnalisées : vers l'extérieur du tronc et vers le haut (volume de cône) ;
#   - 1 unité = 1 m, origine au pied du tronc, Z haut dans Blender (Y haut dans Unity).
# Matériaux : emplacement 0 = « Tree_01 » (écorce), 1 = « Tree_Foliage » (remappés dans Unity).
import importlib
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector

PROJECT = os.environ.get("TEAMTIMO_PROJECT", r"C:\Users\gabin\Documents\Projets Unity\TeamTimo")
SCRIPT_DIR = os.path.join(PROJECT, "ArtSource", "Trees")
if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)
import foliage_cards as fc  # noqa: E402

importlib.reload(fc)

OUT_DIR = os.path.join(PROJECT, "Assets", "Art", "Models", "Environment", "Trees")
BLEND_PATH = os.path.join(PROJECT, "ArtSource", "Trees", "tree_pine.blend")
UP = fc.UP
CARD_ASPECT = 0.45      # largeur de carte / longueur (= ASPECT de generate_pine_frond.py)

# Paramètres communs (valeurs du sapin 01) ; chaque essence en remplace une partie.
BASE = {
    "seed": 5,
    "trunk_height": 15.5,    # m
    "trunk_radius": 0.36,    # m au pied (évasement des racines en plus)
    "first_z": 5.6,          # premières branches (×1,9 en jeu : la caméra passe dessous)
    "last_z": 15.2,          # dernières branches avant la pointe
    "whorls": 20,            # étages de branches (espacement irrégulier)
    "bottom_length": 4.0,    # longueur des branches du bas (m)
    "top_length": 1.5,       # longueur des branches du haut
    "length_curve": 1.15,    # <1 = garde les branches longues plus haut (cime ronde), >1 = cône pointu
    "branches_bottom": 7,    # branches par étage en bas
    "branches_top": 5,
    "pitch": (-22, -8),      # pente des branches en degrés (tirée dans l'intervalle)
    "pitch_top_bonus": 18,   # les branches du haut remontent
    "droop": 0.28,           # retombée du bout des branches (part de la longueur)
    "fold": 0.35,            # pliage en toit de la carte
    "gap_chance": 0.0,       # probabilité qu'une branche manque (silhouette dégarnie)
    "leader": 2.0,           # pointe verticale au sommet (0 = pas de pointe)
    "core_radius": 0.16,     # rayon du cœur sombre (part de la longueur des branches)
    "card_width": 1.2,
}

VARIANTS = {
    # Sapin de base (RV There Yet?) ; branches un peu plus basses (forêt plus dense, 2026-11-13)
    "SM_Tree_Pine_01": {"first_z": 4.4},
    # Épicéa élancé : haut et étroit, branches courtes qui pendent
    "SM_Tree_Pine_02": {
        "seed": 11, "trunk_height": 18.5, "first_z": 6.0, "last_z": 18.0, "whorls": 25,
        "bottom_length": 3.0, "top_length": 1.1, "length_curve": 0.9,
        "branches_bottom": 6, "branches_top": 4, "pitch": (-34, -18), "pitch_top_bonus": 20,
        "droop": 0.36, "core_radius": 0.2,
    },
    # Sapin bas et très large : branches presque jusqu'au sol (masse de végétation à hauteur d'yeux)
    "SM_Tree_Pine_03": {
        "seed": 23, "trunk_height": 9.0, "trunk_radius": 0.3, "first_z": 2.2, "last_z": 8.7,
        "whorls": 15, "bottom_length": 5.6, "top_length": 1.4, "length_curve": 0.9,
        "branches_bottom": 10, "branches_top": 5, "pitch": (-4, 8), "droop": 0.18,
        "leader": 1.4, "card_width": 1.5, "core_radius": 0.2,
    },
    # Vieux pin dégarni : long tronc nu, cime arrondie et clairsemée, branches presque à plat
    "SM_Tree_Pine_04": {
        "seed": 31, "trunk_height": 17.0, "trunk_radius": 0.46, "first_z": 9.6, "last_z": 16.8,
        "whorls": 11, "bottom_length": 3.9, "top_length": 1.8, "length_curve": 0.5,
        "branches_bottom": 7, "branches_top": 5, "pitch": (2, 26), "pitch_top_bonus": 8,
        "droop": 0.42, "gap_chance": 0.3, "leader": 0.0, "core_radius": 0.08, "card_width": 1.5,
    },
    # Sapin large et dense, taille moyenne : branches basses, silhouette en cône épais
    "SM_Tree_Pine_05": {
        "seed": 41, "trunk_height": 12.5, "trunk_radius": 0.38, "first_z": 3.4, "last_z": 12.2,
        "whorls": 18, "bottom_length": 6.2, "top_length": 1.5, "length_curve": 1.0,
        "branches_bottom": 10, "branches_top": 5, "pitch": (-10, 2), "droop": 0.22,
        "card_width": 1.45, "core_radius": 0.2,
    },
}

# Niveaux de détail
LODS = [
    {"segments": 3, "cross": 1.0, "whorl_step": 1, "core_sides": 7, "trunk_sides": 9},
    {"segments": 2, "cross": 0.5, "whorl_step": 1, "core_sides": 6, "trunk_sides": 7},
    {"segments": 2, "cross": 0.5, "whorl_step": 2, "core_sides": 5, "trunk_sides": 5},
]


def branch_length(cfg, z):
    t = max(0.0, min(1.0, (z - cfg["first_z"]) / (cfg["last_z"] - cfg["first_z"])))
    return cfg["bottom_length"] + (cfg["top_length"] - cfg["bottom_length"]) * (t ** cfg["length_curve"]), t


def make_whorls(cfg):
    rnd = random.Random(cfg["seed"])
    whorls = []
    n_w = cfg["whorls"]
    step = (cfg["last_z"] - cfg["first_z"]) / (n_w - 1)
    for i in range(n_w):
        z = cfg["first_z"] + i * step + (rnd.uniform(-0.3, 0.3) * step if 0 < i < n_w - 1 else 0)
        L, t = branch_length(cfg, z)
        n = round(cfg["branches_bottom"] + (cfg["branches_top"] - cfg["branches_bottom"]) * t)
        phase = rnd.uniform(0, math.tau)
        branches = []
        for j in range(n):
            b = {
                "z": z + rnd.uniform(-0.15, 0.15),
                "yaw": phase + j / n * math.tau + rnd.uniform(-0.35, 0.35),
                "length": L * rnd.uniform(0.82, 1.12),
                "pitch": math.radians(rnd.uniform(*cfg["pitch"]) + cfg["pitch_top_bonus"] * t),
                "droop": cfg["droop"] * rnd.uniform(0.8, 1.2) * (1.0 - 0.4 * t),
                "rand": rnd.random(),
                "t": t,
            }
            if rnd.random() >= cfg["gap_chance"] or i == n_w - 1:
                branches.append(b)
        whorls.append(branches)
    return whorls


def build_foliage(cfg, name, lod_index):
    lod = LODS[lod_index]
    whorls = make_whorls(cfg)  # même tirage pour tous les LOD
    zmin = cfg["first_z"] - cfg["bottom_length"] * (cfg["droop"] + 0.4)
    zmax = cfg["last_z"] + max(cfg["leader"], 1.0)
    bm = bmesh.new()
    layers = fc.new_layers(bm)
    uv, col = layers
    normals = {}

    def h01(co):
        return max(0.0, min(1.0, (co.z - zmin) / (zmax - zmin)))

    def outward_normal(co, s):
        radial = Vector((co.x, co.y, 0.0))
        radial = radial.normalized() if radial.length > 1e-4 else Vector((1, 0, 0))
        return (radial * 0.8 + UP * 0.7).normalized()

    for wi, branches in enumerate(whorls):
        if wi % lod["whorl_step"] and wi != len(whorls) - 1:
            continue
        for b in branches:
            length = b["length"] * (1.12 if lod["whorl_step"] > 1 else 1.0)
            root = Vector((0, 0, b["z"]))

            def color(co, s, occ_base=0.85 + 0.15 * b["t"], rand=b["rand"]):
                return (occ_base * (0.3 + 0.7 * s ** 0.7), h01(co), rand, s ** 1.4)

            normals.update(fc.add_card(bm, layers, root, b["yaw"], b["pitch"], length, b["droop"],
                                       lod["segments"], color, outward_normal,
                                       width=cfg["card_width"], aspect=CARD_ASPECT, fold=cfg["fold"]))
            if b["rand"] < lod["cross"]:  # carte croisée presque verticale : volume de profil
                def color2(co, s, rand=(b["rand"] + 0.37) % 1.0, occ_base=0.8 + 0.15 * b["t"]):
                    return (occ_base * (0.3 + 0.7 * s ** 0.7), h01(co), rand, s ** 1.4)

                normals.update(fc.add_card(bm, layers, root, b["yaw"], b["pitch"], length * 0.92, b["droop"],
                                           lod["segments"], color2, outward_normal,
                                           roll=math.radians(78 if b["rand"] > 0.5 else -78),
                                           width=0.8 * cfg["card_width"] / 1.2, aspect=CARD_ASPECT,
                                           fold=cfg["fold"]))

    # pointe : 3 cartes verticales croisées
    if cfg["leader"] > 0:
        for i in range(3):
            def color3(co, s):
                return (0.9, h01(co), 0.5, s * 0.8)

            normals.update(fc.add_card(bm, layers, Vector((0, 0, cfg["last_z"] - 0.3)), i / 3 * math.pi,
                                       math.radians(89.0), cfg["leader"], 0.0, 2, color3,
                                       lambda co, s: (Vector((co.x, co.y, 0)) * 0.5 + UP).normalized(),
                                       roll=math.radians(90), width=1.0, aspect=CARD_ASPECT, fold=0.0))

    # cœur sombre autour du tronc : remplit les trous entre les branches
    sides = lod["core_sides"]
    levels = []
    z = cfg["first_z"] - 0.2
    while z < cfg["last_z"] + 0.4:
        L, _ = branch_length(cfg, z)
        levels.append((z, max(0.25, L * cfg["core_radius"])))
        z += 1.6
    levels.append((cfg["last_z"] + 0.9, 0.05))
    rings = []
    for z, r in levels:
        ring = []
        for k in range(sides):
            a = k / sides * math.tau
            v = bm.verts.new((math.cos(a) * r, math.sin(a) * r, z))
            normals[v] = (Vector((math.cos(a), math.sin(a), 0)) * 0.8 + UP * 0.7).normalized()
            ring.append(v)
        rings.append(ring)
    for r0, r1 in zip(rings[:-1], rings[1:]):
        for k in range(sides):
            f = bm.faces.new((r0[k], r0[(k + 1) % sides], r1[(k + 1) % sides], r1[k]))
            for loop in f.loops:
                loop[uv].uv = (fc.SOLID_U, 0.5)
                loop[col] = (0.08, h01(loop.vert.co), 0.5, 0.0)

    return fc.finish_mesh(bm, normals, f"{name}_Foliage_LOD{lod_index}", 1)


def build_trunk(cfg, lod_index):
    """Tronc effilé avec évasement au pied ; UV sur la bande d'écorce de T_Tree_01_Base."""
    sides = LODS[lod_index]["trunk_sides"]
    H = cfg["trunk_height"]
    levels = [(0.0, 1.5), (0.35, 1.12), (1.2, 1.0), (H * 0.32, 0.78), (H * 0.65, 0.5), (H, 0.12)]
    mesh = bpy.data.meshes.new("trunk")
    bm = bmesh.new()
    uv, col = fc.new_layers(bm)
    rows = []
    for z, rf in levels:
        ring = []
        for k in range(sides + 1):                     # couture dupliquée pour les UV
            a = k / sides * math.tau
            r = cfg["trunk_radius"] * rf * (1.0 + (0.12 * math.sin(a * 3 + 1.0) if z < 0.5 else 0.0))
            ring.append(bm.verts.new((math.cos(a) * r, math.sin(a) * r, z)))
        rows.append(ring)
    for r0, r1 in zip(rows[:-1], rows[1:]):
        for k in range(sides):
            f = bm.faces.new((r0[k], r0[k + 1], r1[k + 1], r1[k]))
            for loop in f.loops:
                kk = k + (1 if loop.vert in (r0[k + 1], r1[k + 1]) else 0)
                loop[uv].uv = (0.05 + 0.4 * kk / sides, 0.02 + 0.96 * loop.vert.co.z / H)
                loop[col] = (1.0, 1.0, 1.0, 0.0)
    bm.to_mesh(mesh)
    bm.free()
    for p in mesh.polygons:
        p.use_smooth = True
        p.material_index = 0
    return mesh


def build_variant(name, cfg, coll, bark, foliage, x_offset):
    objects, stats = [], {}
    for lod in range(len(LODS)):
        trunk = build_trunk(cfg, lod)
        fol, fol_normals = build_foliage(cfg, name, lod)
        me = bpy.data.meshes.new(f"{name}_LOD{lod}")
        bm = bmesh.new()
        bm.from_mesh(trunk)
        bm.from_mesh(fol)
        bm.to_mesh(me)
        bm.free()
        me.materials.append(bark)
        me.materials.append(foliage)
        n_trunk_loops = len(trunk.loops)
        trunk_normals = [Vector(n.vector) for n in trunk.corner_normals]
        bpy.data.meshes.remove(trunk)
        bpy.data.meshes.remove(fol)
        me.normals_split_custom_set(trunk_normals + fol_normals)
        assert len(me.loops) == n_trunk_loops + len(fol_normals)
        obj = bpy.data.objects.new(f"{name}_LOD{lod}", me)
        coll.objects.link(obj)
        obj.location = (x_offset + lod * 12.0, 0, 0)
        objects.append(obj)
        stats[obj.name] = sum(len(p.vertices) - 2 for p in me.polygons)
    fc.export_fbx(objects, os.path.join(OUT_DIR, name + ".fbx"))
    return stats


def main(only=None):
    coll = bpy.data.collections.get("PineTree")
    if coll:
        for o in list(coll.objects):
            bpy.data.objects.remove(o, do_unlink=True)
    else:
        coll = bpy.data.collections.new("PineTree")
        bpy.context.scene.collection.children.link(coll)
    bark = bpy.data.materials.get("Tree_01") or bpy.data.materials.new("Tree_01")
    foliage = bpy.data.materials.get("Tree_Foliage") or bpy.data.materials.new("Tree_Foliage")
    os.makedirs(OUT_DIR, exist_ok=True)
    stats = {}
    for i, (name, overrides) in enumerate(VARIANTS.items()):
        if only and name not in only:
            continue
        cfg = dict(BASE, **overrides)
        stats.update(build_variant(name, cfg, coll, bark, foliage, i * 40.0))
    os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    return stats


result = main()
print(result)
