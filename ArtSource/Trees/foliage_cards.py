# Outils communs aux scripts de végétation (sapins, buissons) : cartes de feuillage texturées.
# Importé par build_pine_tree.py et build_bushes.py (dossier ajouté à sys.path par ces scripts).
#
# Une « carte » = une bande qui part d'un point (racine) dans une direction, retombe vers son bout,
# pliée en toit le long de sa tige ; elle est texturée par une fronde / un rameau de l'atlas :
# u = 0 (bord gauche) -> 0,4 (tige) -> 0,8 (bord droit), v = 0 (racine) -> 1 (bout).
# Couleur de sommet (convention du shader TeamTimo/StylizedFoliage) :
#   R = occlusion, G = hauteur normalisée, B = aléa (par branche), A = souplesse au vent (0 racine, 1 bout).
import math

import bmesh
import bpy
from mathutils import Vector

UP = Vector((0, 0, 1))
ATLAS_U = (0.0, 0.4, 0.8)
SOLID_U = 0.9


def card_frame(yaw, pitch, droop, s):
    """Position relative (en longueurs) et repère local d'un point à l'abscisse s de la carte."""
    fwd = Vector((math.cos(yaw), math.sin(yaw), 0.0))
    side0 = Vector((-math.sin(yaw), math.cos(yaw), 0.0))
    pos = fwd * s * math.cos(pitch) + UP * (s * math.sin(pitch) - droop * s * s)
    tangent = (fwd * math.cos(pitch) + UP * (math.sin(pitch) - 2.0 * droop * s)).normalized()
    up0 = side0.cross(tangent).normalized()
    if up0.z < 0:
        up0 = -up0
    return pos, side0, up0


def add_card(bm, layers, root, yaw, pitch, length, droop, segments, color_fn, normal_fn,
             roll=0.0, width=1.0, aspect=0.45, fold=0.35):
    """Ajoute une carte. color_fn(co, s) -> (R, G, B, A) ; normal_fn(co, s) -> Vector."""
    uv, col = layers
    half_w = length * aspect * 0.5 * width
    rows = []
    for k in range(segments + 1):
        s = k / segments
        pos, side0, up0 = card_frame(yaw, pitch, droop, s)
        p = root + pos * length
        side = side0 * math.cos(roll) + up0 * math.sin(roll)
        normal = up0 * math.cos(roll) - side0 * math.sin(roll)
        f = fold * half_w * (0.4 + 0.6 * s)
        row = []
        for c, co in enumerate((p + side * half_w - normal * f, p, p - side * half_w - normal * f)):
            row.append((bm.verts.new(co), c, s))
        rows.append(row)
    normals = {}
    for r0, r1 in zip(rows[:-1], rows[1:]):
        for c in range(2):
            quad = (r0[c], r0[c + 1], r1[c + 1], r1[c])
            face = bm.faces.new([q[0] for q in quad])
            for loop, (v, cc, s) in zip(face.loops, quad):
                loop[uv].uv = (ATLAS_U[cc], s)
                loop[col] = color_fn(v.co, s)
                normals[v] = normal_fn(v.co, s)
    return normals


def finish_mesh(bm, normals, name, material_index):
    """bmesh -> mesh, faces lisses ; renvoie (mesh, normales par coin dans l'ordre des boucles)."""
    bm.verts.index_update()
    nlist = [None] * len(bm.verts)
    for v, n in normals.items():
        nlist[v.index] = n
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for p in mesh.polygons:
        p.use_smooth = True
        p.material_index = material_index
    loop_normals = [nlist[mesh.loops[i].vertex_index] for i in range(len(mesh.loops))]
    return mesh, loop_normals


def new_layers(bm):
    return bm.loops.layers.uv.new("UVMap"), bm.loops.layers.float_color.new("Col")


def export_fbx(objects, path):
    """Exporte des objets (origine au pied, Z haut) en FBX pour Unity (Y haut, axes convertis)."""
    saved = [o.location.copy() for o in objects]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.location = (0, 0, 0)
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        mesh_smooth_type="OFF",
        use_mesh_modifiers=True,
        colors_type="LINEAR",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
        embed_textures=False,
    )
    for o, loc in zip(objects, saved):
        o.location = loc
