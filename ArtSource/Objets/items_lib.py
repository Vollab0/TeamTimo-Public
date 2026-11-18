# Outils communs aux objets de TeamTimo (build_items_*.py), chargés comme module dans Blender 5.2.
# Réutilise le constructeur de mesh du village (ArtSource/Lobby/village_lib.py : MB, lathe, tube, box_bm, export FBX).
#
# Conventions (GameDesign/03_Contenu/Objets/Objets_Plan_De_Production.md §3) :
#   - 1 unité = 1 m du jeu ; Z haut ; face utile vers -Y (= +Z dans Unity).
#   - Objet tenu : manche vertical, partie utile en haut, ORIGINE = centre de la prise en main.
#     Coffre : origine au sol au centre. Pièce : centre du volume.
#   - UN mesh, UN matériau « M_Items » (atlas T_Items_Atlas, disposition dans items_layout.py).
#     Pièces mobiles = objets séparés parentés, nommés <Nom>_<Piece> (ex. Chest_Maze_Lid).
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

PROJECT = os.environ.get("TEAMTIMO_PROJECT", r"C:\Users\gabin\Documents\Projets Unity\TeamTimo")
_HERE = os.path.join(PROJECT, "ArtSource", "Objets")
_LOBBY = os.path.join(PROJECT, "ArtSource", "Lobby")
for _p in (_HERE, _LOBBY):
    if _p not in sys.path:
        sys.path.insert(0, _p)

import importlib  # noqa: E402

import items_layout as IL  # noqa: E402
import village_lib as vl  # noqa: E402

importlib.reload(IL)
importlib.reload(vl)

OUT_DIR = os.path.join(PROJECT, "Assets", "Art", "Models", "Objects", "Items")
ATLAS = os.path.join(PROJECT, "Assets", "Art", "Textures", "Objects", "T_Items_Atlas.png")
MB = vl.MB
TAU = 2 * math.pi
MAT_T = IL.EDGE_T0 - 0.04       # t max de la zone « matière » d'une bande (au-delà : arête usée)


# ------------------------------------------------------------------ UV dans l'atlas
def band(name, u, t):
    """UV dans la bande name : u libre (raccord), t 0..1 (0 -> MAT_T : matière ; >= EDGE_T0 : arête usée)."""
    u0, v0, u1, v1 = IL.band_rect(name)
    m = 2.0 / IL.ATLAS_SIZE
    return (u, v0 + m + max(0.0, min(1.0, t)) * (v1 - v0 - 2 * m))


def band_mat(name, u, f):
    """f 0..1 réparti dans la zone matière de la bande."""
    return band(name, u, f * MAT_T)


def band_edge(name, u, f=0.5):
    return band(name, u, IL.EDGE_T0 + 0.04 + f * (0.96 - IL.EDGE_T0 - 0.04))


def cell(name, s, t, margin=0.04):
    u0, v0, u1, v1 = IL.cell_rect(name)
    s = margin + max(0.0, min(1.0, s)) * (1 - 2 * margin)
    t = margin + max(0.0, min(1.0, t)) * (1 - 2 * margin)
    return (u0 + s * (u1 - u0), v0 + t * (v1 - v0))


def cell_uvf(name):
    """uvfunc pour MB.add : projection boîte dans une case (aplats)."""
    def f(lc, wc, n):
        ax = max(range(3), key=lambda i: abs(n[i]))
        a, b = [(1, 2), (0, 2), (0, 1)][ax]
        return cell(name, (lc[a] * 1.7) % 1.0, (lc[b] * 1.7) % 1.0)
    return f


def box_band_uvf(name, size, u_scale=2.0, edge=True, u_off=0.0):
    """uvfunc pour une boîte (box_bm biseauté) : fil le long de la plus grande dimension de la face,
    faces de biseau (normale non alignée) dans la zone « arête usée »."""
    def f(lc, wc, n):
        nn = Vector(n).normalized()
        ax = max(range(3), key=lambda i: abs(nn[i]))
        a, b = [(1, 2), (0, 2), (0, 1)][ax]
        if size[b] > size[a]:
            a, b = b, a
        u = lc[a] / u_scale + u_off
        if edge and abs(nn[ax]) < 0.95:
            return band_edge(name, u)
        return band_mat(name, u, lc[b] / size[b] + 0.5)
    return f


# ------------------------------------------------------------------ primitives
def box(mb, center, size, uvf, bevel=0.03, segments=1, rot=None):
    m = Matrix.Translation(Vector(center))
    if rot is not None:
        m = m @ rot
    mb.add(vl.box_bm(size, bevel, segments), m, 0, uvf)


def band_box(mb, center, size, name, bevel=0.03, u_scale=2.0, rot=None, u_off=0.0):
    box(mb, center, size, box_band_uvf(name, size, u_scale, u_off=u_off), bevel, 1, rot)


def lathe(mb, profile, sides, matrix, uvfn, cap_top=None, cap_bottom=None, a0=0.0, rmod=None, smooth=True):
    """Révolution autour de Z local (profil [(r, z)] du bas vers le haut), comme village_lib.lathe
    mais avec un modulateur de rayon rmod(angle, z, k) -> facteur (formes irrégulières : bourse, plis).
    uvfn(angle, z, r, k) -> (u, v), ou uvfn(angle, z, r, k, fk) avec fk = rangée de la FACE (pour changer de zone
    d'atlas d'une rangée à l'autre sans qu'une face s'étire à travers l'atlas) ;
    cap_* = uvfn_cap(x, y) (polygone de fermeture, matériau unique)."""
    import inspect
    per_face = len(inspect.signature(uvfn).parameters) >= 5
    rows = []
    for k, (r, z) in enumerate(profile):
        row = []
        for i in range(sides + 1):
            a = a0 + TAU * i / sides
            rr = r * (rmod(a, z, k) if rmod else 1.0)
            row.append((matrix @ Vector((rr * math.cos(a), rr * math.sin(a), z)), a, z, r, k))
        rows.append(row)
    m3 = matrix.to_3x3()
    for k in range(len(profile) - 1):
        r0, r1 = profile[k][0], profile[k + 1][0]
        if r0 < 1e-6 and r1 < 1e-6:
            continue
        dr, dz = r1 - r0, profile[k + 1][1] - profile[k][1]
        for i in range(sides):
            q = [rows[k][i], rows[k][i + 1], rows[k + 1][i + 1], rows[k + 1][i]]
            am = a0 + TAU * (i + 0.5) / sides
            out = (m3 @ Vector((math.cos(am), math.sin(am), 0))) * dz - (m3 @ Vector((0, 0, 1))) * dr
            pts = [p[0] for p in q]
            uvs = [uvfn(p[1], p[2], p[3], p[4], k) if per_face else uvfn(p[1], p[2], p[3], p[4]) for p in q]
            if r0 < 1e-6 or r1 < 1e-6:          # sommet sur l'axe : triangle
                drop = 0 if r0 < 1e-6 else 2
                pts.pop(drop)
                uvs.pop(drop)
            mb.face(pts, 0, uvs, smooth=smooth, ref=out)
    up = m3 @ Vector((0, 0, 1))
    for cap, row, sgn in ((cap_top, rows[-1], 1), (cap_bottom, rows[0], -1)):
        if cap is None or row[0][3] < 1e-6:
            continue
        pts = [p[0] for p in row[:-1]]
        loc = [matrix.inverted() @ p for p in pts]
        mb.face(pts, 0, [cap(v.x, v.y) for v in loc], ref=up * sgn)
    return rows


def tube(mb, path, radius, sides, uvfn, side_axis=(1, 0, 0), cap_start=None, cap_end=None, smooth=True):
    """Tube balayé (village_lib.tube) + fermetures optionnelles cap_*(angle) -> (u, v)."""
    rings = vl.tube(mb, path, radius, sides, 0, uvfn, side_axis, smooth)
    path = [Vector(p) for p in path]
    for cap, ring, ref in ((cap_start, rings[0], path[0] - path[1]), (cap_end, rings[-1], path[-1] - path[-2])):
        if cap is None:
            continue
        mb.face([x[0] for x in ring[:-1]], 0, [cap(x[1]) for x in ring[:-1]], ref=ref)
    return rings


def flat_strip(mb, path, width, thick, side, uvfn, caps=True):
    """Ruban plat (section rectangulaire width × thick) le long de path ; side = direction de la largeur."""
    path = [Vector(p) for p in path]
    rings, length = [], 0.0
    for k, p in enumerate(path):
        if k:
            length += (p - path[k - 1]).length
        t = (path[min(k + 1, len(path) - 1)] - path[max(k - 1, 0)]).normalized()
        a = Vector(side)
        a = (a - t * a.dot(t)).normalized()
        b = t.cross(a)
        w, h = a * width / 2, b * thick / 2
        rings.append(([p + w + h, p - w + h, p - w - h, p + w - h], length, p))
    for k in range(len(path) - 1):
        for i in range(4):
            j = (i + 1) % 4
            q = [rings[k][0][i], rings[k][0][j], rings[k + 1][0][j], rings[k + 1][0][i]]
            c = sum(q, Vector()) / 4
            ax = (rings[k][2] + rings[k + 1][2]) / 2
            uvs = [uvfn(i, rings[k][1]), uvfn(j, rings[k][1]), uvfn(j, rings[k + 1][1]), uvfn(i, rings[k + 1][1])]
            mb.face(q, 0, uvs, smooth=False, ref=c - ax)
    if caps:
        for ring, ref in ((rings[0], path[0] - path[1]), (rings[-1], path[-1] - path[-2])):
            mb.face(ring[0], 0, [uvfn(i, ring[1]) for i in range(4)], ref=ref)


def smooth_path(pts, n_per=4):
    """Catmull-Rom passant par pts (liste de tuples), n_per points par segment."""
    P = [Vector(p) for p in pts]
    P = [P[0] * 2 - P[1]] + P + [P[-1] * 2 - P[-2]]
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for k in range(n_per):
            t = k / n_per
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t +
                              (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(P[-2])
    return out


# ------------------------------------------------------------------ Blender
def material():
    mat = vl.tex_material("M_Items", ATLAS)
    return mat


def finish(mb, name, coll, origin=(0, 0, 0), weld=True, sharp_angle=50.0):
    """Recentre sur origin (le pivot devient l'origine de l'objet) puis crée l'objet avec le matériau M_Items."""
    o = Vector(origin)
    for v in mb.bm.verts:
        v.co -= o
    ob = mb.to_object(name, coll, sharp_angle=sharp_angle, weld=weld)
    ob.data.materials.append(material())
    return ob


def export(objects, fbx_name):
    """Exporte les objets (racine en premier, pièces mobiles parentées ensuite), aux réglages du village."""
    vl.export_fbx(objects, os.path.join(OUT_DIR, fbx_name + ".fbx"))


def report(ob):
    bpy.context.view_layer.update()
    pts = [ob.matrix_world.inverted() @ (ob.matrix_world @ Vector(c)) for c in ob.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return {"tris": vl.tri_count(ob), "size": [round(x, 2) for x in (hi - lo)],
            "min": [round(x, 2) for x in lo], "max": [round(x, 2) for x in hi]}


def reset_collection(name):
    coll = vl.reset_collection(name)
    for o in list(coll.objects):
        bpy.data.objects.remove(o)
    return coll
