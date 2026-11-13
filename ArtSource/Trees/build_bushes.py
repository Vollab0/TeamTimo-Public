# Construit les buissons stylisés (SM_Bush_*) et les exporte en FBX pour Unity.
# À lancer dans Blender 5.x (Scripting > Run Script, ou via le MCP), dans un fichier vide.
#
# Les buissons sont posés comme « détails » du terrain (comme l'herbe, voir TerrainGrassSettings) :
# un seul mesh par buisson (pas de LOD), origine au pied, taille réelle en jeu (le joueur fait ~5,2 m).
# Mêmes conventions que les sapins (shader TeamTimo/StylizedFoliage) :
#   UV0 = atlas (T_BushLeaves ou T_PineFrond), couleur de sommet R = occlusion, G = hauteur,
#   B = aléa par rameau, A = souplesse au vent (0 au pied, 1 au bout des rameaux).
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

OUT_DIR = os.path.join(PROJECT, "Assets", "Art", "Models", "Environment", "Bushes")
BLEND_PATH = os.path.join(PROJECT, "ArtSource", "Trees", "bushes.blend")
UP = fc.UP

# Chaque buisson : rameaux (cartes) partant d'une zone près du pied.
#   atlas : "leaves" (T_BushLeaves, rapport 0,5) ou "frond" (T_PineFrond, rapport 0,45)
#   count, length, pitch (degrés), droop, root_radius / root_z (zone de départ des rameaux),
#   cross = part des rameaux doublés d'une carte croisée, core = cœur sombre (rayon, hauteur) ou None
#   (retiré le 2026-11-13 : se voyait comme un cube vert au centre)
BUSHES = {
    # Buisson feuillu rond
    "SM_Bush_Leafy_01": {
        "seed": 2, "atlas": "leaves", "count": 34, "length": (1.3, 1.8), "pitch": (-15, 70),
        "droop": 0.18, "root_radius": 0.45, "root_z": (0.5, 1.2), "cross": 1.0, "fold": 0.2,
        "core": None, "segments": 2, "width": 1.0,
    },
    # Buisson feuillu haut et dressé
    "SM_Bush_Leafy_02": {
        "seed": 8, "atlas": "leaves", "count": 26, "length": (1.6, 2.4), "pitch": (45, 85),
        "droop": 0.12, "root_radius": 0.35, "root_z": (0.1, 0.9), "cross": 1.0, "fold": 0.15,
        "core": None, "segments": 3, "width": 1.0,
    },
    # Fougère : grandes frondes qui partent du centre et retombent en arc
    "SM_Bush_Fern_01": {
        "seed": 5, "atlas": "frond", "count": 14, "length": (1.7, 2.4), "pitch": (52, 80),
        "droop": 0.55, "root_radius": 0.12, "root_z": (0.05, 0.2), "cross": 0.0, "fold": 0.2,
        "core": None, "segments": 4, "width": 1.15,
    },
    # Genévrier : arbuste bas et large d'aiguilles
    "SM_Bush_Juniper_01": {
        "seed": 9, "atlas": "frond", "count": 32, "length": (1.4, 2.0), "pitch": (15, 62),
        "droop": 0.3, "root_radius": 0.4, "root_z": (0.2, 0.7), "cross": 0.5, "fold": 0.35,
        "core": None, "segments": 3, "width": 1.2,
    },
}
ASPECTS = {"leaves": 0.5, "frond": 0.45}


def build_bush(name, cfg, coll, material):
    rnd = random.Random(cfg["seed"])
    bm = bmesh.new()
    layers = fc.new_layers(bm)
    uv, col = layers
    normals = {}
    center = Vector((0, 0, sum(cfg["root_z"]) * 0.5 + 0.2))
    height = cfg["length"][1] * 1.1 + cfg["root_z"][1]

    def h01(co):
        return max(0.0, min(1.0, co.z / height))

    def bush_normal(co, s):
        d = co - center
        d = d.normalized() if d.length > 1e-4 else UP
        return (d * 0.85 + UP * 0.45).normalized()

    for i in range(cfg["count"]):
        a = rnd.uniform(0, math.tau)
        r = cfg["root_radius"] * math.sqrt(rnd.random())
        root = Vector((math.cos(a) * r, math.sin(a) * r, rnd.uniform(*cfg["root_z"])))
        yaw = a + rnd.uniform(-0.6, 0.6) if r > 0.05 else rnd.uniform(0, math.tau)
        pitch = math.radians(rnd.uniform(*cfg["pitch"]))
        length = rnd.uniform(*cfg["length"])
        droop = cfg["droop"] * rnd.uniform(0.8, 1.2)
        rand = rnd.random()

        def color(co, s, rand=rand):
            occ = 0.55 + 0.45 * min(1.0, s * 1.2) ** 0.8   # centre à peine plus sombre (pas de trou noir)
            return (occ, h01(co), rand, s ** 1.2)

        common = dict(aspect=ASPECTS[cfg["atlas"]], fold=cfg["fold"])
        normals.update(fc.add_card(bm, layers, root, yaw, pitch, length, droop, cfg["segments"], color,
                                   bush_normal, roll=math.radians(rnd.uniform(-20, 20)),
                                   width=cfg["width"], **common))
        if rnd.random() < cfg["cross"]:
            normals.update(fc.add_card(bm, layers, root, yaw, pitch, length * 0.9, droop, cfg["segments"], color,
                                       bush_normal, roll=math.radians(rnd.choice((-80, 80))),
                                       width=cfg["width"] * 0.85, **common))

    if cfg["core"]:  # cœur sombre : boule aplatie qui bouche le centre
        radius, zc = cfg["core"]
        tmp = bmesh.new()
        bmesh.ops.create_icosphere(tmp, subdivisions=1, radius=1.0)
        vmap = {}
        for v in tmp.verts:
            co = Vector((v.co.x * radius, v.co.y * radius, zc + v.co.z * radius * 0.75))
            nv = bm.verts.new(co)
            normals[nv] = (v.co.normalized() * 0.85 + UP * 0.45).normalized()
            vmap[v] = nv
        for f in tmp.faces:
            nf = bm.faces.new([vmap[v] for v in f.verts])
            for loop in nf.loops:
                loop[uv].uv = (fc.SOLID_U, 0.5)
                loop[col] = (0.1, h01(loop.vert.co), 0.5, 0.15)
        tmp.free()

    mesh, loop_normals = fc.finish_mesh(bm, normals, name, 0)
    mesh.materials.append(material)
    mesh.normals_split_custom_set(loop_normals)
    obj = bpy.data.objects.new(name, mesh)
    coll.objects.link(obj)
    return obj, sum(len(p.vertices) - 2 for p in mesh.polygons), [round(x, 2) for x in obj.dimensions]


def main():
    coll = bpy.data.collections.get("Bushes")
    if coll:
        for o in list(coll.objects):
            bpy.data.objects.remove(o, do_unlink=True)
    else:
        coll = bpy.data.collections.new("Bushes")
        bpy.context.scene.collection.children.link(coll)
    os.makedirs(OUT_DIR, exist_ok=True)
    stats = {}
    for i, (name, cfg) in enumerate(BUSHES.items()):
        # un matériau par atlas (remappés dans Unity vers un matériau par buisson)
        mat_name = "Bush_Leaves" if cfg["atlas"] == "leaves" else "Bush_Frond"
        material = bpy.data.materials.get(mat_name) or bpy.data.materials.new(mat_name)
        obj, tris, dims = build_bush(name, cfg, coll, material)
        fc.export_fbx([obj], os.path.join(OUT_DIR, name + ".fbx"))
        obj.location = (i * 6.0, 0, 0)
        stats[name] = {"triangles": tris, "dimensions_m": dims}
    os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    return stats


result = main()
print(result)
