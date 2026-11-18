# Outils communs aux bâtiments du village du Lobby (build_houses.py, build_barn.py, build_tent.py).
# Chargé comme module depuis Blender 5.2 (le dossier ArtSource/Lobby est ajouté à sys.path par chaque script).
#
# Conventions partagées :
#   - 1 unité = 1 m du jeu, pivot au sol au centre, façade (porte) vers -Y (= +Z dans Unity), Z haut.
#   - Bois : trim sheet T_Village_Wood (3 bandes de fil le long de u + 4 carrés spéciaux en haut) ;
#     T_Village_WoodWhite a la même disposition (bois peint en blanc).
#   - Enduit, pierre, bardage, toile : projection boîte en coordonnées monde (WORLD_TILE m par répétition).
#   - Tuiles : atlas 4 × 2 tuiles T_Village_RoofTile_<Couleur>.
#   - Colliders : objets « <Nom>_Col<n> » (meshes convexes, sans matériau) exportés dans le même FBX ;
#     Unity en fait des MeshCollider convexes sur le calque Wall (la caméra et les ennemis s'y arrêtent).
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

PROJECT = os.environ.get("TEAMTIMO_PROJECT", r"C:\Users\gabin\Documents\Projets Unity\TeamTimo")
OUT_DIR = os.path.join(PROJECT, "Assets", "Art", "Models", "Environment", "Village")
TEX_DIR = os.path.join(PROJECT, "Assets", "Art", "Textures", "Environment", "Village")
WALL_TEX = os.path.join(PROJECT, "Assets", "Art", "Textures", "Environment", "Walls", "T_WallStone_Albedo.png")

WOOD_U_METERS = 4.0          # une répétition du fil du bois = 4 m de poutre
BANDS = [(0.5, 0.75), (0.25, 0.5), (0.0, 0.25)]
SPECIAL = {"glass": 0, "lattice": 1, "brass": 2, "black": 3}   # carrés de la rangée du haut (v 0,75-1)
WORLD_TILE = {"plaster": 6.0, "stone": 6.0, "planks": 8.0, "canvas": 8.0, "paint": 3.0}
TILE_W, TILE_L, TILE_T, ROW = 1.55, 3.4, 0.28, 2.55


# ------------------------------------------------------------------ constructeur de mesh
class MB:
    """Accumule des pièces (bmesh temporaires) dans un bmesh, avec matériau et UV par coin de face."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def add(self, tmp, matrix, mat, uvfunc, smooth=True):
        """Copie tmp (coordonnées locales) transformé par matrix ; uvfunc(local, world, n_local) -> (u, v)."""
        vmap = {v: self.bm.verts.new(matrix @ v.co) for v in tmp.verts}
        flip = matrix.to_3x3().determinant() < 0
        for f in tmp.faces:
            verts = [vmap[v] for v in f.verts]
            loops = list(f.loops)
            if flip:                          # repère en miroir : on garde les faces vers l'extérieur
                verts, loops = verts[::-1], loops[::-1]
            nf = self.bm.faces.new(verts)
            n = f.normal.copy()
            nf.material_index = mat(matrix.to_3x3() @ n) if callable(mat) else mat   # mat(normale monde) possible
            for l_new, l_old in zip(nf.loops, loops):
                l_new[self.uv].uv = uvfunc(l_old.vert.co, matrix @ l_old.vert.co, n)
            nf.smooth = smooth
        tmp.free()

    def grid(self, fn, ns, nt, mat, uvfn, smooth=True, ref=None):
        """Surface paramétrée fn(s, t) -> Vector, s et t dans [0, 1], ns × nt cellules ; uvfn(s, t, p) -> (u, v).
        ref(p) -> Vector : sens voulu de la normale au centre p de chaque face (la face est retournée sinon)."""
        rows = [[fn(i / ns, j / nt) for i in range(ns + 1)] for j in range(nt + 1)]
        vs = [[self.bm.verts.new(p) for p in row] for row in rows]
        for j in range(nt):
            for i in range(ns):
                quad = [vs[j][i], vs[j][i + 1], vs[j + 1][i + 1], vs[j + 1][i]]
                st = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
                if ref is not None:
                    pc = sum((v.co for v in quad), Vector()) / 4
                    nrm = (quad[1].co - quad[0].co).cross(quad[3].co - quad[0].co) + \
                          (quad[2].co - quad[3].co).cross(quad[2].co - quad[1].co)
                    if nrm.dot(ref(pc)) < 0:
                        quad, st = quad[::-1], st[::-1]
                f = self.bm.faces.new(quad)
                f.material_index = mat
                f.smooth = smooth
                for loop, (a, b) in zip(f.loops, st):
                    loop[self.uv].uv = uvfn(a / ns, b / nt, rows[b][a])
        return vs

    def face(self, pts, mat, uvs, smooth=False, ref=None):
        """Ajoute un polygone (points monde) avec ses UV ; ref : sens voulu de la normale (retourné sinon)."""
        pts, uvs = [Vector(p) for p in pts], list(uvs)
        if ref is not None:
            nrm = Vector()
            for a, b in zip(pts, pts[1:] + pts[:1]):       # normale de Newell
                nrm += Vector(((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y)))
            if nrm.dot(Vector(ref)) < 0:
                pts, uvs = pts[::-1], uvs[::-1]
        f = self.bm.faces.new([self.bm.verts.new(p) for p in pts])
        f.material_index = mat
        f.smooth = smooth
        for loop, uv in zip(f.loops, uvs):
            loop[self.uv].uv = uv
        return f

    def to_object(self, name, coll, sharp_angle=60.0, weighted=True, weld=False):
        me = bpy.data.meshes.new(name)
        bm = self.bm
        if weld:      # soude les sommets confondus (faces ajoutées une à une) pour lisser les surfaces courbes
            bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-4)
        bm.normal_update()
        for e in bm.edges:
            e.smooth = len(e.link_faces) == 2 and e.calc_face_angle(0.0) < math.radians(sharp_angle)
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.new(name, me)
        coll.objects.link(ob)
        if weighted:
            mod = ob.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
            mod.mode = "FACE_AREA"
            mod.weight = 80
            mod.keep_sharp = True
        return ob


def box_bm(size, bevel=0.0, segments=1):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    if bevel > 0:
        b = min(bevel, min(size) * 0.3)
        bmesh.ops.bevel(bm, geom=list(bm.edges) + list(bm.verts), offset=b, segments=segments,
                        profile=0.5, affect="EDGES", clamp_overlap=True)
    bm.normal_update()
    return bm


def prism_bm(profile, y0, y1, cap_bottom=False):
    """Polygone convexe (x, z) extrudé le long de Y de y0 à y1, faces vers l'extérieur."""
    bm = bmesh.new()
    a = [bm.verts.new((x, y0, z)) for x, z in profile]
    b = [bm.verts.new((x, y1, z)) for x, z in profile]
    bm.faces.new(a)
    bm.faces.new(b)
    n = len(profile)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([a[i], a[j], b[j], b[i]])
    bm.normal_update()
    c = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
    for f in bm.faces:
        if f.normal.dot(f.calc_center_median() - c) < 0:
            f.normal_flip()
    if not cap_bottom:
        for f in [f for f in bm.faces if f.normal.z < -0.99]:
            bm.faces.remove(f)
    bm.normal_update()
    return bm


def slab_bm(profile, y0, y1, bevel=0.0, cap_bottom=True):
    """Prisme convexe (profil (x, z) extrudé le long de Y) aux arêtes biseautées : pierres, ferrures, planches."""
    bm = prism_bm(profile, y0, y1, cap_bottom=True)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges) + list(bm.verts), offset=bevel, segments=1,
                        profile=0.5, affect="EDGES", clamp_overlap=True)
    if not cap_bottom:
        zmin = min(v.co.z for v in bm.verts)
        for f in [f for f in bm.faces if f.normal.z < -0.99 and all(abs(v.co.z - zmin) < 1e-4 for v in f.verts)]:
            bm.faces.remove(f)
    bm.normal_update()
    return bm


def u_profile(half, zs, n_wall=3, n_arc=20, z0=0.0):
    """Profil en U renversé (murs droits + demi-cercle) de (-half, z0) à (half, z0) en passant par le haut.
    Retourne une fonction f(s) -> (x, z), s dans [0, 1] proportionnel à l'abscisse curviligne, et la longueur totale."""
    pts = [(-half, z0 + (zs - z0) * i / n_wall) for i in range(n_wall + 1)]
    pts += [(half * math.cos(math.pi - math.pi * i / n_arc), zs + half * math.sin(math.pi - math.pi * i / n_arc)) for i in range(1, n_arc)]
    pts += [(half, z0 + (zs - z0) * i / n_wall) for i in range(n_wall, -1, -1)]
    acc = [0.0]
    for a, b in zip(pts, pts[1:]):
        acc.append(acc[-1] + math.dist(a, b))
    total = acc[-1]

    def f(s):
        d = max(0.0, min(1.0, s)) * total
        for i in range(len(pts) - 1):
            if d <= acc[i + 1] + 1e-9:
                k = (d - acc[i]) / max(1e-9, acc[i + 1] - acc[i])
                return (pts[i][0] + (pts[i + 1][0] - pts[i][0]) * k, pts[i][1] + (pts[i + 1][1] - pts[i][1]) * k)
        return pts[-1]
    return f, total


def lathe(mb, profile, sides, matrix, mat, uvfn, smooth=True, cap_top=None, cap_bottom=None, a0=0.0):
    """Révolution autour de l'axe Z local : profile = [(r, z), ...] du bas vers le haut.
    uvfn(angle, z, r, i_prof) -> (u, v). cap_* : (mat, uvfn_cap(x, y)) pour fermer le haut / le bas par un polygone."""
    rows = []
    for k, (r, z) in enumerate(profile):
        row = []
        for i in range(sides + 1):
            a = a0 + 2 * math.pi * i / sides
            row.append((matrix @ Vector((r * math.cos(a), r * math.sin(a), z)), a, z, r, k))
        rows.append(row)
    axis_pts = [matrix @ Vector((0, 0, z)) for _, z in profile]
    for k in range(len(profile) - 1):
        r0, r1 = profile[k][0], profile[k + 1][0]
        if r0 < 1e-6 and r1 < 1e-6:
            continue
        # Normale : le profil est parcouru avec l'extérieur à sa droite -> n = (dz, -dr) dans le plan (r, z).
        dr, dz = r1 - r0, profile[k + 1][1] - profile[k][1]
        for i in range(sides):
            q = [rows[k][i], rows[k][i + 1], rows[k + 1][i + 1], rows[k + 1][i]]
            am = a0 + 2 * math.pi * (i + 0.5) / sides
            radial = matrix.to_3x3() @ Vector((math.cos(am), math.sin(am), 0))
            out = radial * dz - (matrix.to_3x3() @ Vector((0, 0, 1))) * dr
            mb.face([p[0] for p in q], mat, [uvfn(p[1], p[2], p[3], p[4]) for p in q], smooth=smooth, ref=out)
    up = matrix.to_3x3() @ Vector((0, 0, 1))
    for cap, row, sgn in ((cap_top, rows[-1], 1), (cap_bottom, rows[0], -1)):
        if cap is None or row[0][3] < 1e-6:
            continue
        cm, cuv = cap
        pts = [p[0] for p in row[:-1]]
        uvs = [cuv(p[3] * math.cos(p[1]), p[3] * math.sin(p[1])) for p in row[:-1]]
        mb.face(pts, cm, uvs, ref=up * sgn)


def tube(mb, path, radius, sides, mat, uvfn, side_axis=(1, 0, 0), smooth=True):
    """Tube balayé le long de path (liste de points) ; radius : nombre ou liste (un rayon par point).
    side_axis : axe perpendiculaire au plan du chemin (chemins plans). uvfn(angle, longueur, rayon) -> (u, v)."""
    path = [Vector(p) for p in path]
    radii = radius if isinstance(radius, (list, tuple)) else [radius] * len(path)
    sx = Vector(side_axis).normalized()
    rings, length = [], 0.0
    for k, p in enumerate(path):
        if k > 0:
            length += (p - path[k - 1]).length
        t = (path[min(k + 1, len(path) - 1)] - path[max(k - 1, 0)]).normalized()
        a = (sx - t * sx.dot(t)).normalized()
        b = t.cross(a)
        rings.append([(p + (a * math.cos(2 * math.pi * i / sides) + b * math.sin(2 * math.pi * i / sides)) * radii[k],
                       2 * math.pi * i / sides, length, radii[k], p) for i in range(sides + 1)])
    for k in range(len(path) - 1):
        for i in range(sides):
            q = [rings[k][i], rings[k][i + 1], rings[k + 1][i + 1], rings[k + 1][i]]
            c = sum((x[0] for x in q), Vector()) / 4
            ax = (q[0][4] + q[2][4]) / 2
            mb.face([x[0] for x in q], mat, [uvfn(x[1], x[2], x[3]) for x in q], smooth=smooth, ref=c - ax)
    return rings


def band_v(band, t):
    v0, v1 = BANDS[band]
    m = 0.012
    return v0 + m + max(0.0, min(1.0, t)) * (v1 - v0 - 2 * m)


def wood_uv(size, band, uoff):
    L, dy, dz = size

    def f(lc, wc, n):
        ax = max(range(3), key=lambda i: abs(n[i]))
        if ax == 1:
            return ((lc.x + uoff) / WOOD_U_METERS, band_v(band, lc.z / dz + 0.5))
        if ax == 2:
            return ((lc.x + uoff) / WOOD_U_METERS, band_v(band, lc.y / dy + 0.5))
        return (uoff * 0.1 + (lc.y / dy + 0.5) * 0.22, band_v(band, lc.z / dz + 0.5))
    return f


def special_uv(name, size):
    k = SPECIAL[name]
    m = 0.02

    def f(lc, wc, n):
        ax = max(range(3), key=lambda i: abs(n[i]))
        a, b = [(1, 2), (0, 2), (0, 1)][ax]
        s = (lc[a] / size[a] + 0.5, lc[b] / size[b] + 0.5)
        return (k * 0.25 + m + s[0] * (0.25 - 2 * m), 0.75 + m + s[1] * (0.25 - 2 * m))
    return f


def world_uv(kind, offset=(0.0, 0.0, 0.0)):
    tile = WORLD_TILE[kind]

    def f(lc, wc, n_local):
        p = wc + Vector(offset)
        n = n_local
        ax = max(range(3), key=lambda i: abs(n[i]))
        if ax == 0:
            return (p.y / tile, p.z / tile)
        if ax == 1:
            return (p.x / tile, p.z / tile)
        return (p.x / tile, p.y / tile)
    return f


def world_uv_n(kind, rot, offset=(0.0, 0.0, 0.0)):
    """Comme world_uv, mais la normale locale est d'abord tournée par rot (pièces tournées)."""
    tile = WORLD_TILE[kind]
    r3 = rot.to_3x3()

    def f(lc, wc, n_local):
        p = wc + Vector(offset)
        n = r3 @ n_local
        ax = max(range(3), key=lambda i: abs(n[i]))
        if ax == 0:
            return (p.y / tile, p.z / tile)
        if ax == 1:
            return (p.x / tile, p.z / tile)
        return (p.x / tile, p.y / tile)
    return f


def frame_matrix(p0, p1, yaxis):
    """Repère d'une poutre : X le long de p0->p1, Y = yaxis (rendu orthogonal), centre au milieu."""
    p0, p1 = Vector(p0), Vector(p1)
    x = (p1 - p0).normalized()
    y = Vector(yaxis)
    y = (y - x * y.dot(x)).normalized()
    z = x.cross(y)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = (p0 + p1) / 2
    return m, (p1 - p0).length


# ------------------------------------------------------------------ bâtiment de base
class Builder:
    def __init__(self, seed):
        self.rng = random.Random(seed)
        self.colliders = []          # listes de points (enveloppes convexes)

    def jit(self, a):
        return self.rng.uniform(-a, a)

    def beam(self, mb, p0, p1, yaxis, dy, dz, mat=0, band=None, bevel=0.14, segments=1, wobble=0.8, ext=0.0):
        m, L = frame_matrix(p0, p1, yaxis)
        L += ext
        if wobble:
            m = m @ Matrix.Rotation(math.radians(self.jit(wobble)), 4, "Y") @ Matrix.Rotation(math.radians(self.jit(wobble * 0.6)), 4, "Z")
        size = (L, dy, dz)
        band = self.rng.randrange(3) if band is None else band
        mb.add(box_bm(size, bevel, segments), m, mat, wood_uv(size, band, self.rng.uniform(0, 4)))

    def block(self, mb, center, size, mat, uvfunc, bevel=0.0, rot=None):
        m = Matrix.Translation(Vector(center))
        if rot is not None:
            m = m @ rot
        mb.add(box_bm(size, bevel), m, mat, uvfunc)

    def collider(self, pts):
        self.colliders.append([Vector(p) for p in pts])

    def collider_box(self, center, size, rot=None):
        c, s = Vector(center), Vector(size) / 2
        pts = []
        for sx in (-1, 1):
            for sy in (-1, 1):
                for sz in (-1, 1):
                    v = Vector((sx * s.x, sy * s.y, sz * s.z))
                    if rot is not None:
                        v = rot.to_3x3() @ v
                    pts.append(c + v)
        self.collider(pts)

    def tiles(self, mb, origin, d, nrm, t0, t1, length, base_lift, eave_row_t=None):
        """Tuiles sur un pan : origin = point du haut du pan (y = 0), d = direction de la pente (vers le bas),
        nrm = normale du pan, t0..t1 = étendue le long de la pente, length = étendue le long de Y.
        Les tuiles reposent à base_lift au-dessus du pan (dessus des chevrons de rive)."""
        ncol = max(1, round(length / TILE_W))
        tw = length / ncol
        r = 0
        tb = t1
        while True:
            tt = max(tb - TILE_L, t0)
            cols = ncol + (r % 2)
            y0 = -length / 2 - (tw / 2 if r % 2 else 0)
            for k in range(cols):
                ya = max(y0 + k * tw, -length / 2)
                yb = min(y0 + (k + 1) * tw, length / 2)
                if yb - ya < 0.4:
                    continue
                self.tile(mb, origin, d, nrm, ya + 0.04, yb - 0.04, tt, tb + self.jit(0.18), r == 0, base_lift)
            if tt <= t0 + 0.05:
                break
            tb -= ROW
            r += 1

    def tile(self, mb, apex, d, nrm, ya, yb, tt, tb, eave_row, base_lift):
        lift_t = base_lift
        lift_b = base_lift + TILE_T + 0.03
        ang = math.radians(self.jit(3.0))
        ymid, tmid = (ya + yb) / 2, (tt + tb) / 2

        def P(t, y, h):
            dy, dt = y - ymid, t - tmid
            y2 = ymid + dy * math.cos(ang) - dt * math.sin(ang)
            t2 = tmid + dy * math.sin(ang) + dt * math.cos(ang)
            lift = lift_t + (lift_b - lift_t) * (t - tt) / (tb - tt)
            return apex + d * t2 + Vector((0, y2, 0)) + nrm * (lift + h)

        vt = [P(tt, ya, TILE_T), P(tt, yb, TILE_T), P(tb, yb, TILE_T), P(tb, ya, TILE_T)]
        vb = [P(tt, ya, 0), P(tt, yb, 0), P(tb, yb, 0), P(tb, ya, 0)]
        # Tuile fermée (6 faces) : chaque tuile est inclinée (plus soulevée en bas qu'en haut), il reste donc
        # un coin vide entre le voligeage et son dessous, visible de côté et d'en bas (rives, débords).
        # Sans dessous ni dos, on voyait le ciel à travers (back-face culling dans Unity).
        faces = [
            ("top", [vt[0], vt[3], vt[2], vt[1]]),
            ("front", [vt[3], vb[3], vb[2], vt[2]]),
            ("left", [vt[0], vb[0], vb[3], vt[3]]),
            ("right", [vt[2], vb[2], vb[1], vt[1]]),
            ("under", [vb[0], vb[1], vb[2], vb[3]]),
            ("back", [vt[1], vb[1], vb[0], vt[0]]),
        ]
        cell = self.rng.randrange(8)
        u0, v0 = (cell % 4) * 0.25, 0.5 if cell < 4 else 0.0
        flip = self.rng.random() < 0.5
        m = 0.01

        def cu(a):
            a = 1 - a if flip else a
            return u0 + m + a * (0.25 - 2 * m)

        def cv(b):
            return v0 + 0.5 - m - b * (0.5 - 2 * m)

        uvs = {
            "top": [(0, 0), (0, 1), (1, 1), (1, 0)],
            "front": [(0, 0.96), (0, 1), (1, 1), (1, 0.96)],
            "left": [(0.0, 0), (0.04, 0), (0.04, 1), (0.0, 1)],
            "right": [(1.0, 1), (0.96, 1), (0.96, 0), (1.0, 0)],
            "under": [(0, 0.2), (1, 0.2), (1, 0.6), (0, 0.6)],
            "back": [(1, 0.0), (1, 0.04), (0, 0.04), (0, 0.0)],
        }
        center = sum(vt + vb, Vector()) / 8
        for name, vs in faces:
            nv = [mb.bm.verts.new(v) for v in vs]
            f = mb.bm.faces.new(nv)
            f.smooth = False
            f.normal_update()
            if f.normal.dot(f.calc_center_median() - center) < 0:
                f.normal_flip()      # pan en miroir : face remise vers l'extérieur (sinon invisible dans Unity)
            for loop in f.loops:
                a, b = uvs[name][nv.index(loop.vert)]
                loop[mb.uv].uv = (cu(a), cv(b))

    def collider_objects(self, prefix, coll):
        """Crée un objet convexe par collider (« <prefix>_Col<n> »)."""
        obs = []
        for i, pts in enumerate(self.colliders):
            bm = bmesh.new()
            for p in pts:
                bm.verts.new(p)
            bmesh.ops.convex_hull(bm, input=list(bm.verts))
            for v in [v for v in bm.verts if not v.link_faces]:
                bm.verts.remove(v)
            me = bpy.data.meshes.new(f"{prefix}_Col{i}")
            bm.to_mesh(me)
            bm.free()
            ob = bpy.data.objects.new(me.name, me)
            coll.objects.link(ob)
            ob.display_type = "WIRE"
            ob.hide_render = True
            obs.append(ob)
        return obs


# ------------------------------------------------------------------ Blender : collection, matériaux, export
def reset_collection(name):
    coll = bpy.data.collections.get(name)
    if coll is None:
        coll = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(coll)
    for o in list(coll.objects):
        me = o.data
        bpy.data.objects.remove(o)
        if me and me.users == 0:
            bpy.data.meshes.remove(me)
    return coll


def tex_material(name, albedo, normal=None):
    """Matériau d'aperçu Blender (les noms sont remappés sur les matériaux du projet à l'import Unity)."""
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.85
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    path = albedo if os.path.isabs(albedo) else os.path.join(TEX_DIR, albedo)
    img = bpy.data.images.load(path, check_existing=True)
    img.reload()
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    nt.links.new(tex.outputs[0], bsdf.inputs["Base Color"])
    if normal:
        npath = os.path.join(TEX_DIR, normal)
        if os.path.exists(npath):
            nimg = bpy.data.images.load(npath, check_existing=True)
            nimg.reload()
            nimg.colorspace_settings.name = "Non-Color"
            nt2 = nt.nodes.new("ShaderNodeTexImage")
            nt2.image = nimg
            nm = nt.nodes.new("ShaderNodeNormalMap")
            nt.links.new(nt2.outputs[0], nm.inputs["Color"])
            nt.links.new(nm.outputs[0], bsdf.inputs["Normal"])
    return mat


MATERIALS = {
    "wood": ("M_Village_Wood", "T_Village_Wood_Albedo.png", "T_Village_Wood_Normal.png"),
    "wood_white": ("M_Village_WoodWhite", "T_Village_WoodWhite_Albedo.png", "T_Village_Wood_Normal.png"),
    "plaster": ("M_Village_Plaster", "T_Village_Plaster_Albedo.png", "T_Village_Plaster_Normal.png"),
    "stone": ("M_WallStone", WALL_TEX, None),
    "planks": ("M_Village_Planks", "T_Village_Planks_Albedo.png", "T_Village_Planks_Normal.png"),
    "planks_red": ("M_Village_RedPlanks", "T_Village_RedPlanks_Albedo.png", "T_Village_Planks_Normal.png"),
    "canvas": ("M_Village_Canvas", "T_Village_Canvas_Albedo.png", "T_Village_Canvas_Normal.png"),
    "paint_red": ("M_Village_PaintRed", "T_Village_PaintRed_Albedo.png", None),
    # Forge et accessoires
    "wood_dark": ("M_Village_WoodDark", "T_Village_Wood_Albedo.png", "T_Village_Wood_Normal.png"),   # bois teinté dans Unity
    "brick": ("M_Village_Brick", "T_Village_Brick_Albedo.png", "T_Village_Brick_Normal.png"),
    "brick_dark": ("M_Village_BrickDark", "T_Village_BrickDark_Albedo.png", "T_Village_Brick_Normal.png"),
    "metal": ("M_Village_Metal", "T_Village_Metal_Albedo.png", "T_Village_Metal_Normal.png"),
    "iron": ("M_Village_Iron", "T_Village_Iron_Albedo.png", "T_Village_Iron_Normal.png"),
    "embers": ("M_Village_Embers", "T_Village_Embers_Albedo.png", None),
    # Étals et accessoires (build_market.py, build_props.py)
    "stripes": ("M_Village_Stripes", "T_Village_Stripes_Albedo.png", None),
    "clay": ("M_Village_Clay", "T_Village_Clay_Albedo.png", None),
    "burlap": ("M_Village_Burlap", "T_Village_Burlap_Albedo.png", "T_Village_Burlap_Normal.png"),
    "hay": ("M_Village_Hay", "T_Village_Hay_Albedo.png", "T_Village_Hay_Normal.png"),
    "soil": ("M_Village_Soil", "T_Village_Soil_Albedo.png", "T_Village_Soil_Normal.png"),
    "water": ("M_Village_Water", "T_Village_Water_Albedo.png", None),
    "lamp": ("M_Village_LampGlass", "T_Village_LampGlass_Albedo.png", None),
    "leaf": ("M_Village_Leaf", "T_Village_Leaf_Albedo.png", None),
    "endgrain": ("M_Village_EndGrain", "T_Village_EndGrain_Albedo.png", None),
}


def material(key):
    if key.startswith("roof_"):
        c = key[5:]
        return tex_material(f"M_Roof_{c}", f"T_Village_RoofTile_{c}_Albedo.png", "T_Village_RoofTile_Normal.png")
    name, alb, nrm = MATERIALS[key]
    return tex_material(name, alb, nrm)


def assign(ob, keys):
    for k in keys:
        ob.data.materials.append(material(k))


def export_fbx(objects, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="OFF", use_mesh_modifiers=True, add_leaf_bones=False,
        bake_anim=False, path_mode="STRIP", embed_textures=False,
    )


def tri_count(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    me = ob.evaluated_get(dg).to_mesh()
    n = sum(len(p.vertices) - 2 for p in me.polygons)
    ob.evaluated_get(dg).to_mesh_clear()
    return n
