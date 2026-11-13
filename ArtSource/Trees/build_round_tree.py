# Construit l'arbre « rond » stylisé (SM_Tree_Round_01) et l'exporte en FBX pour Unity.
# À lancer dans Blender 5.x (Scripting > Run Script, ou via le MCP).
#
# - Tronc et branches : repris tels quels de SM_Tree_01 (pack Free Nature), ses 3 LOD.
# - Feuillage : les cartes alpha « touffues » sont remplacées par des amas de boules bosselées
#   (un amas = une grosse boule + des boules satellites). Chaque amas se lit comme un volume
#   avec un contour festonné, dans l'esprit de l'herbe (RV There Yet?).
#
# Conventions lues par le shader TeamTimo/StylizedFoliage (matériau du feuillage) :
#   - couleur de sommet (export LINEAR) : R = occlusion (0 creux / dessous, 1 dehors),
#     G = hauteur normalisée dans le houppier (0 bas, 1 haut), B = aléa par amas, A = 1 ;
#   - normales personnalisées : arrondies par amas (relief lisible, pas de bruit de lumière) ;
#   - 1 unité = 1 m, origine au pied du tronc, Z haut dans Blender (Y haut dans Unity).
# Matériaux : emplacement 0 = « Tree_01 » (écorce), emplacement 1 = « Tree_Foliage ».
import math
import os
import random

import bmesh
import bpy
from mathutils import Vector, noise

PROJECT = os.environ.get("TEAMTIMO_PROJECT", r"C:\Users\gabin\Documents\Projets Unity\TeamTimo")
TRUNK_FBX = os.path.join(PROJECT, "Assets", "Art", "Models", "Environment", "SM_Tree_01.fbx")
OUT_DIR = os.path.join(PROJECT, "Assets", "Art", "Models", "Environment", "Trees")
BLEND_PATH = os.path.join(PROJECT, "ArtSource", "Trees", "tree_round.blend")
NAME = "SM_Tree_Round_01"
SEED = 7

# Amas du houppier (centre en m, rayon en m). Placés sur les anciens paquets de feuilles
# (k-means des cartes alpha), un peu écartés du tronc pour creuser des trous entre eux.
CLUSTERS = [
    ((-0.85, 2.6, 5.6), 2.45),
    ((-1.95, -1.8, 5.95), 2.5),
    ((1.75, -0.55, 5.35), 2.4),
    ((-3.0, 0.4, 5.85), 2.2),
    ((2.2, 2.3, 6.1), 2.3),
    ((2.0, -0.3, 8.3), 2.45),
    ((-0.85, 1.65, 8.7), 2.5),
    ((-0.7, -1.0, 9.1), 2.5),
    ((0.3, 0.4, 10.6), 2.0),
]

# Niveaux de détail : subdivisions de l'icosphère, nombre de boules satellites par amas
LODS = [
    {"subdiv": 3, "satellites": 3},
    {"subdiv": 2, "satellites": 3},
    {"subdiv": 2, "satellites": 0},
]

BOTTOM_SQUASH = 0.8     # dessous des amas aplati (silhouette « nuage »)
BUMP_AMOUNT = 0.1       # bosses (part du rayon de chaque boule)
BUMP_FREQ = 2.4         # fréquence des bosses (par rayon de boule)
NORMAL_MIX = (0.05, 0.12, 0.5, 0.33)  # surface, boule, amas, houppier


def make_puffs(lod, rnd):
    """Liste des boules (centre, rayon, index d'amas)."""
    canopy_c = sum((Vector(c) for c, _ in CLUSTERS), Vector()) / len(CLUSTERS)
    puffs = []
    for ci, (c, r) in enumerate(CLUSTERS):
        c = Vector(c)
        if lod["satellites"] == 0:
            puffs.append((c, r * 0.95, ci))
            continue
        out = (c - canopy_c)
        out.z = max(out.z, 0.0) + 0.6
        out.normalize()
        puffs.append((c + Vector((0, 0, -0.1 * r)), r * 0.74, ci))
        n = lod["satellites"]
        base_ang = rnd.uniform(0, math.tau)
        for k in range(n):
            # répartis autour de l'axe « vers l'extérieur » de l'amas, côté haut / dehors
            ang = base_ang + k / n * math.tau + rnd.uniform(-0.35, 0.35)
            elev = rnd.uniform(0.25, 0.75)
            side = out.orthogonal().normalized()
            side2 = out.cross(side).normalized()
            d = (out * elev + (side * math.cos(ang) + side2 * math.sin(ang)) * (1 - elev)).normalized()
            d.z = max(d.z, -0.25)
            d.normalize()
            puffs.append((c + d * r * rnd.uniform(0.42, 0.52), r * rnd.uniform(0.55, 0.66), ci))
    return puffs


def build_foliage(lod_index):
    lod = LODS[lod_index]
    rnd = random.Random(SEED)  # même tirage pour tous les LOD -> silhouettes cohérentes
    puffs = make_puffs(lod, rnd)
    cluster_rand = [random.Random(SEED * 100 + i).random() for i in range(len(CLUSTERS))]
    canopy_c = sum((Vector(c) for c, _ in CLUSTERS), Vector()) / len(CLUSTERS)
    zmin = min(c[2] - r for c, r in CLUSTERS)
    zmax = max(c[2] + r for c, r in CLUSTERS)

    bm = bmesh.new()
    puff_layer = bm.verts.layers.int.new("puff")
    for pi, (pc, pr, ci) in enumerate(puffs):
        tmp = bmesh.new()
        bmesh.ops.create_icosphere(tmp, subdivisions=lod["subdiv"], radius=1.0)
        ccen = Vector(CLUSTERS[ci][0])
        vmap = {}
        for v in tmp.verts:
            d = v.co.normalized()
            p = pc + d * pr
            # bosses organiques (bruit en coordonnées monde, fréquence liée à la taille)
            p += d * pr * BUMP_AMOUNT * noise.noise(p * (BUMP_FREQ / pr) +Vector((ci * 3.1, 0, 0)))
            if p.z < ccen.z:  # dessous aplati
                p.z = ccen.z + (p.z - ccen.z) * BOTTOM_SQUASH
            nv = bm.verts.new(p)
            nv[puff_layer] = pi
            vmap[v] = nv
        for f in tmp.faces:
            bm.faces.new([vmap[v] for v in f.verts])
        tmp.free()
    bm.verts.ensure_lookup_table()

    # Supprime les faces entièrement cachées dans une autre boule
    def inside_other(v):
        own = v[puff_layer]
        for pj, (pc, pr, _) in enumerate(puffs):
            if pj != own and (v.co - pc).length < pr * 0.97:
                return True
        return False

    hidden = {v: inside_other(v) for v in bm.verts}
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if all(hidden[v] for v in f.verts)], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.normal_update()

    col = bm.loops.layers.float_color.new("Col")
    normals = {}
    for v in bm.verts:
        pc, pr, ci = puffs[v[puff_layer]]
        ccen = Vector(CLUSTERS[ci][0])
        w = NORMAL_MIX
        n = (v.normal * w[0] + (v.co - pc).normalized() * w[1]
             + (v.co - ccen).normalized() * w[2] + (v.co - canopy_c).normalized() * w[3]).normalized()
        normals[v.index] = n
        # occlusion : proximité des autres boules (creux entre volumes) + profondeur + dessous
        occ = 1.0
        for pj, (qc, qr, _) in enumerate(puffs):
            if pj == v[puff_layer]:
                continue
            dist = (v.co - qc).length - qr
            if dist < qr * 0.7:
                occ -= 0.35 * (1.0 - max(dist, 0.0) / (qr * 0.7))
        depth = (v.co - canopy_c).length / 6.0
        occ *= 0.55 + 0.45 * min(depth, 1.0)
        occ *= 0.6 + 0.4 * (n.z * 0.5 + 0.5)
        occ = max(0.0, min(1.0, occ))
        h = (v.co.z - zmin) / (zmax - zmin)
        for loop in v.link_loops:
            loop[col] = (occ, h, cluster_rand[ci], 1.0)

    mesh = bpy.data.meshes.new(f"{NAME}_Foliage_LOD{lod_index}")
    bm.to_mesh(mesh)
    bm.free()
    for p in mesh.polygons:
        p.use_smooth = True
    loop_normals = [normals[mesh.loops[i].vertex_index] for i in range(len(mesh.loops))]
    return mesh, loop_normals


def import_trunks():
    """Importe SM_Tree_01 et ne garde que l'écorce de chaque LOD (Z haut, mètres)."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=TRUNK_FBX)
    trunks = {}
    for o in set(bpy.data.objects) - before:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
            continue
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index != 0], context="FACES")
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
        bm.to_mesh(o.data)
        bm.free()
        for a in list(o.data.color_attributes):
            o.data.color_attributes.remove(a)
        lod = int(o.name.split("LOD")[-1][0])
        trunks[lod] = o
    return trunks


def get_material(name, color):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = color
    return m


def main():
    coll = bpy.data.collections.get("RoundTree")
    if coll:
        for o in list(coll.objects):
            bpy.data.objects.remove(o, do_unlink=True)
    else:
        coll = bpy.data.collections.new("RoundTree")
        bpy.context.scene.collection.children.link(coll)
    for m in [m for m in bpy.data.meshes if m.users == 0]:
        bpy.data.meshes.remove(m)

    trunks = import_trunks()
    bark = trunks[0].material_slots[0].material
    bark.name = "Tree_01"
    foliage_mat = get_material("Tree_Foliage", (0.36, 0.6, 0.2, 1))
    stats = {}
    objects = []
    for lod in range(len(LODS)):
        trunk = trunks[lod]
        for c in list(trunk.users_collection):
            c.objects.unlink(trunk)
        coll.objects.link(trunk)
        trunk.name = f"{NAME}_LOD{lod}"
        trunk.data.name = trunk.name
        trunk.data.materials.clear()
        trunk.data.materials.append(bark)
        trunk.data.materials.append(foliage_mat)
        for p in trunk.data.polygons:
            p.material_index = 0
        trunk_normals = [Vector(n.vector) for n in trunk.data.corner_normals]
        trunk_loops = len(trunk.data.loops)
        trunk.data.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
        for d in trunk.data.color_attributes["Col"].data:
            d.color = (1, 1, 1, 1)

        mesh, fol_normals = build_foliage(lod)
        for p in mesh.polygons:
            p.material_index = 1
        # fusion tronc + feuillage (bmesh : les faces du tronc d'abord, puis celles du feuillage)
        me = trunk.data
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.from_mesh(mesh)
        bm.to_mesh(me)
        bm.free()
        bpy.data.meshes.remove(mesh)
        me.normals_split_custom_set(trunk_normals + fol_normals)
        assert len(me.loops) == trunk_loops + len(fol_normals)
        trunk.location = (lod * 16.0, 0, 0)
        objects.append(trunk)
        stats[trunk.name] = {
            "triangles_ecorce": sum(len(p.vertices) - 2 for p in me.polygons if p.material_index == 0),
            "triangles_feuillage": sum(len(p.vertices) - 2 for p in me.polygons if p.material_index == 1),
        }

    # export : les 3 LOD dans un seul FBX (suffixe _LODn -> LODGroup automatique dans Unity)
    os.makedirs(OUT_DIR, exist_ok=True)
    saved = [o.location.copy() for o in objects]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.location = (0, 0, 0)
        o.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_DIR, NAME + ".fbx"),
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
    os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    return stats


if __name__ == "__main__" or True:
    result = main()
    print(result)
