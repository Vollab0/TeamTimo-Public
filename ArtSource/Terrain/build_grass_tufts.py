# Modélise les touffes d'herbe low-poly du terrain et les exporte en FBX pour Unity.
# À lancer dans Blender 5.x (Scripting > Run Script, ou via le MCP).
#
# Conventions (lues par le shader TeamTimo/StylizedGrass) :
#   - 1 unité = 1 m, origine au pied de la touffe, hauteur max = 1 (l'échelle finale vient
#     des minHeight/maxHeight du DetailPrototype du terrain) ;
#   - UV0.y = hauteur normalisée (0 au pied, 1 en haut) -> dégradé de couleur + poids du vent ;
#   - couleur de sommet RGB = teinte propre (fleurs), A = 1 sur les pétales, 0 sur les brins ;
#   - UV1.x = valeur aléatoire par brin (variation de teinte).
import math
import os
import random

import bmesh
import bpy

PROJECT = os.environ.get("TEAMTIMO_PROJECT", r"C:\Users\gabin\Documents\Projets Unity\TeamTimo")
OUT_DIR = os.path.join(PROJECT, "Assets", "Art", "Models", "Environment", "Grass")
BLEND_PATH = os.path.join(PROJECT, "ArtSource", "Terrain", "grass_tufts.blend")

SEGMENTS = 3  # segments par brin (3 -> 7 sommets, 5 triangles)


def add_blade(bm, uv0, uv1, col, rnd, base, yaw, lean, height, width, bend, blade_rand):
    """Ajoute un brin effilé et courbé. base = (x, y), yaw/lean en radians."""
    dir_x, dir_y = math.cos(yaw), math.sin(yaw)          # direction d'inclinaison
    side_x, side_y = -dir_y, dir_x                       # largeur du brin
    twist = rnd.uniform(-0.5, 0.5)
    rows = []
    for i in range(SEGMENTS + 1):
        t = i / SEGMENTS
        # courbe : l'inclinaison augmente avec la hauteur
        off = math.sin(lean) * height * (t ** (1.0 + bend))
        z = height * t * math.cos(lean * t * 0.6)
        cx = base[0] + dir_x * off
        cy = base[1] + dir_y * off
        w = width * (1.0 - t) ** 0.8
        sx = side_x * math.cos(twist * t) - dir_x * math.sin(twist * t)
        sy = side_y * math.cos(twist * t) - dir_y * math.sin(twist * t)
        if i < SEGMENTS:
            rows.append([bm.verts.new((cx - sx * w * 0.5, cy - sy * w * 0.5, z)),
                         bm.verts.new((cx + sx * w * 0.5, cy + sy * w * 0.5, z))])
        else:
            rows.append([bm.verts.new((cx, cy, z))])
    faces = []
    for i in range(SEGMENTS):
        a, b = rows[i]
        if i < SEGMENTS - 1:
            c, d = rows[i + 1]
            faces.append(bm.faces.new((a, b, d)))
            faces.append(bm.faces.new((a, d, c)))
        else:
            tip = rows[i + 1][0]
            faces.append(bm.faces.new((a, b, tip)))
    for f in faces:
        for loop in f.loops:
            v = loop.vert
            loop[uv0].uv = (0.5, v.co.z)          # remplacé après normalisation
            loop[uv1].uv = (blade_rand, 0.0)
            loop[col] = (0.0, 0.0, 0.0, 0.0)
    return faces


def add_flower(bm, uv0, uv1, col, rnd, base, height):
    """Tige fine + corolle (disque à 6 pétales, cœur jaune, bord blanc)."""
    add_blade(bm, uv0, uv1, col, rnd, base, rnd.uniform(0, math.tau), rnd.uniform(0.05, 0.25),
              height, 0.035, 0.3, rnd.random())
    cx, cy, cz = base[0], base[1], height * 0.98
    tilt_yaw = rnd.uniform(0, math.tau)
    tilt = math.radians(rnd.uniform(15, 40))
    radius = rnd.uniform(0.05, 0.07)
    center = bm.verts.new((cx, cy, cz + 0.01))
    ring = []
    n = 10
    for i in range(n):
        a = i / n * math.tau
        r = radius * (1.0 if i % 2 == 0 else 0.55)        # contour en étoile -> pétales
        x, y = math.cos(a) * r, math.sin(a) * r
        # inclinaison de la corolle
        z = -x * math.sin(tilt) * math.cos(tilt_yaw) - y * math.sin(tilt) * math.sin(tilt_yaw)
        ring.append(bm.verts.new((cx + x, cy + y, cz + z)))
    for i in range(n):
        f = bm.faces.new((center, ring[i], ring[(i + 1) % n]))
        for loop in f.loops:
            loop[uv0].uv = (0.5, 1.0)
            loop[uv1].uv = (0.5, 0.0)
            if loop.vert is center:
                loop[col] = (1.0, 0.82, 0.25, 1.0)          # cœur jaune
            else:
                loop[col] = (1.0, 1.0, 0.97, 1.0)           # pétales blancs


def build_tuft(name, seed, blade_count, height_range, lean_range, width_range, bend_range,
               spread, flowers=0):
    rnd = random.Random(seed)
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    uv0 = bm.loops.layers.uv.new("UVMap")
    uv1 = bm.loops.layers.uv.new("BladeRandom")
    col = bm.loops.layers.color.new("Col")
    for i in range(blade_count):
        ang = rnd.uniform(0, math.tau)
        r = spread * math.sqrt(rnd.random())
        base = (math.cos(ang) * r, math.sin(ang) * r)
        # les brins penchent vers l'extérieur de la touffe
        yaw = ang + rnd.uniform(-0.6, 0.6)
        add_blade(bm, uv0, uv1, col, rnd, base, yaw,
                  rnd.uniform(*lean_range), rnd.uniform(*height_range),
                  rnd.uniform(*width_range), rnd.uniform(*bend_range), rnd.random())
    for i in range(flowers):
        ang = rnd.uniform(0, math.tau)
        r = spread * 0.8 * math.sqrt(rnd.random())
        add_flower(bm, uv0, uv1, col, rnd, (math.cos(ang) * r, math.sin(ang) * r), rnd.uniform(0.75, 1.0))

    # hauteur normalisée -> UV0.y (0..1), pour le vent et le dégradé
    max_z = max(v.co.z for v in bm.verts)
    for f in bm.faces:
        for loop in f.loops:
            u = loop[uv0].uv.x
            loop[uv0].uv = (u, loop.vert.co.z / max_z)
    # hauteur max = 1
    for v in bm.verts:
        v.co *= 1.0 / max_z
    bm.to_mesh(mesh)
    bm.free()
    for p in mesh.polygons:
        p.use_smooth = True
    obj = bpy.data.objects.new(name, mesh)
    return obj, len(mesh.polygons), len(mesh.vertices)


def main():
    # collection dédiée, reconstruite à chaque exécution
    coll = bpy.data.collections.get("GrassTufts")
    if coll:
        for o in list(coll.objects):
            bpy.data.objects.remove(o, do_unlink=True)
    else:
        coll = bpy.data.collections.new("GrassTufts")
        bpy.context.scene.collection.children.link(coll)

    specs = [
        # nom, graine, brins, hauteur, inclinaison, largeur, courbure, étalement, fleurs
        ("SM_GrassTuft", 11, 14, (0.5, 1.0), (0.3, 0.95), (0.08, 0.13), (0.4, 1.4), 0.35, 0),
        ("SM_GrassTuft_Dry", 23, 8, (0.55, 1.0), (0.15, 0.6), (0.05, 0.08), (0.0, 0.5), 0.2, 0),
        ("SM_GrassFlowers", 37, 7, (0.35, 0.65), (0.3, 0.9), (0.07, 0.11), (0.4, 1.2), 0.3, 2),
    ]
    stats = {}
    os.makedirs(OUT_DIR, exist_ok=True)
    for i, spec in enumerate(specs):
        obj, tris_quads, verts = build_tuft(*spec)
        coll.objects.link(obj)
        obj.location = (i * 2.0, 0, 0)
        tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        stats[obj.name] = {"triangles": tris, "vertices": verts}

        # export individuel, origine au pied, axes convertis pour Unity (Y haut)
        loc = obj.location.copy()
        obj.location = (0, 0, 0)
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(OUT_DIR, obj.name + ".fbx"),
            use_selection=True,
            object_types={"MESH"},
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_UNITS",
            axis_forward="-Z",
            axis_up="Y",
            bake_space_transform=True,
            mesh_smooth_type="FACE",
            use_mesh_modifiers=True,
            colors_type="LINEAR",
            add_leaf_bones=False,
            bake_anim=False,
        )
        obj.location = loc
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    return stats


result = main()
print(result)
