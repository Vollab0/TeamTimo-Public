# Outils communs aux scripts du taureau : champs de distance (SDF), maillage, reperes du modele.
# Charge par les autres scripts :  exec(open(COMMON, encoding="utf-8").read())
# Conventions : Z vers le haut, le taureau regarde vers -Y, sa gauche est +X, unites en metres.
import bpy
import math
import os
import numpy as np

SRC_DIR = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo\ArtSource\Taureau"
TEX_DIR = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo\Assets\Art\Textures\Characters\Taureau"
FBX_PATH = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo\Assets\Art\Models\Characters\Bull.fbx"

F32 = np.float32


def rot_matrix(deg):
    """Matrice 3x3 (repere local -> monde) d'un Euler XYZ en degres (meme convention que Blender)."""
    rx, ry, rz = [math.radians(a) for a in deg]
    cx, sx, cy, sy, cz, sz = math.cos(rx), math.sin(rx), math.cos(ry), math.sin(ry), math.cos(rz), math.sin(rz)
    Rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    Ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    Rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    return Rz @ Ry @ Rx


class Prim:
    """Une forme : fn(X, Y, Z) -> distance signee, plus sa boite englobante."""
    def __init__(self, fn, lo, hi):
        self.fn = fn
        self.lo = np.array(lo, dtype=float)
        self.hi = np.array(hi, dtype=float)

    def __call__(self, X, Y, Z):
        return self.fn(X, Y, Z)


def _to_local(R, c, X, Y, Z):
    px, py, pz = X - c[0], Y - c[1], Z - c[2]
    lx = R[0, 0] * px + R[1, 0] * py + R[2, 0] * pz
    ly = R[0, 1] * px + R[1, 1] * py + R[2, 1] * pz
    lz = R[0, 2] * px + R[1, 2] * py + R[2, 2] * pz
    return lx, ly, lz


def ell(c, r, rot=(0, 0, 0)):
    """Ellipsoide de centre c et de demi-axes r, tourne de rot (Euler XYZ, degres)."""
    c = np.array(c, dtype=float)
    r = np.array(r, dtype=float)
    R = rot_matrix(rot)
    rmin = float(r.min())

    def fn(X, Y, Z):
        lx, ly, lz = _to_local(R, c, X, Y, Z)
        k0 = np.sqrt((lx / r[0]) ** 2 + (ly / r[1]) ** 2 + (lz / r[2]) ** 2)
        k1 = np.sqrt((lx / r[0] ** 2) ** 2 + (ly / r[1] ** 2) ** 2 + (lz / r[2] ** 2) ** 2)
        return np.where(k1 > 1e-6, k0 * (k0 - 1.0) / np.maximum(k1, 1e-6), -rmin)
    m = float(r.max())
    return Prim(fn, c - m, c + m)


def cone(a, b, ra, rb):
    """Cone arrondi (capsule a deux rayons) entre les points a et b."""
    a = np.array(a, dtype=float)
    b = np.array(b, dtype=float)
    ba = b - a
    l2 = float(ba @ ba)
    rr = ra - rb
    a2 = l2 - rr * rr
    il2 = 1.0 / l2

    def fn(X, Y, Z):
        pax, pay, paz = X - a[0], Y - a[1], Z - a[2]
        y = pax * ba[0] + pay * ba[1] + paz * ba[2]
        z = y - l2
        qx, qy, qz = pax * l2 - ba[0] * y, pay * l2 - ba[1] * y, paz * l2 - ba[2] * y
        x2 = qx * qx + qy * qy + qz * qz
        y2 = y * y * l2
        z2 = z * z * l2
        k = np.sign(rr) * rr * rr * x2
        d1 = np.sqrt(x2 + z2) * il2 - rb
        d2 = np.sqrt(x2 + y2) * il2 - ra
        d3 = (np.sqrt(x2 * a2 * il2) + y * rr) * il2 - ra
        return np.where(np.sign(z) * a2 * z2 > k, d1, np.where(np.sign(y) * a2 * y2 < k, d2, d3))
    m = max(ra, rb)
    return Prim(fn, np.minimum(a, b) - m, np.maximum(a, b) + m)


def rbox(c, half, rad, rot=(0, 0, 0), taper=None):
    """Boite aux aretes arrondies. taper(lz) -> facteur applique a la demi-largeur X selon la hauteur locale."""
    c = np.array(c, dtype=float)
    half = np.array(half, dtype=float)
    R = rot_matrix(rot)
    inner = half - rad

    def fn(X, Y, Z):
        lx, ly, lz = _to_local(R, c, X, Y, Z)
        if taper is not None:
            lx = lx / taper(lz)
        qx, qy, qz = np.abs(lx) - inner[0], np.abs(ly) - inner[1], np.abs(lz) - inner[2]
        outside = np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2 + np.maximum(qz, 0) ** 2)
        inside = np.minimum(np.maximum(qx, np.maximum(qy, qz)), 0)
        return outside + inside - rad
    m = float(np.linalg.norm(half)) * (1.6 if taper is not None else 1.0)
    return Prim(fn, c - m, c + m)


def squash(prim, c, s):
    """Ecrase une forme autour de c par les facteurs s = (sx, sy, sz)."""
    c = np.array(c, dtype=float)
    s = np.array(s, dtype=float)
    smin_ = float(s.min())

    def fn(X, Y, Z):
        return prim.fn(c[0] + (X - c[0]) / s[0], c[1] + (Y - c[1]) / s[1], c[2] + (Z - c[2]) / s[2]) * smin_
    lo = c + (prim.lo - c) * s
    hi = c + (prim.hi - c) * s
    return Prim(fn, np.minimum(lo, hi), np.maximum(lo, hi))


def mir(prim):
    """Symetrique d'une forme par rapport au plan X = 0."""
    return Prim(lambda X, Y, Z: prim.fn(-X, Y, Z),
                (-prim.hi[0], prim.lo[1], prim.lo[2]), (-prim.lo[0], prim.hi[1], prim.hi[2]))


def smin(a, b, k):
    h = np.maximum(k - np.abs(a - b), 0.0) / k
    return np.minimum(a, b) - h * h * k * 0.25


def smax(a, b, k):
    h = np.maximum(k - np.abs(a - b), 0.0) / k
    return np.maximum(a, b) + h * h * k * 0.25


class Field:
    """Grille de distances : union / soustraction lissees de formes, puis extraction du maillage."""
    def __init__(self, lo, hi, voxel):
        self.lo = np.array(lo, dtype=float)
        self.v = float(voxel)
        self.n = (np.ceil((np.array(hi, dtype=float) - self.lo) / self.v).astype(int) + 1)
        self.ax = [(self.lo[i] + np.arange(self.n[i]) * self.v).astype(F32) for i in range(3)]
        self.d = np.full(tuple(self.n), 10.0, dtype=F32)

    def _block(self, prim, pad):
        i0 = np.clip(np.floor((prim.lo - pad - self.lo) / self.v).astype(int), 0, self.n - 1)
        i1 = np.clip(np.ceil((prim.hi + pad - self.lo) / self.v).astype(int) + 1, 1, self.n)
        sl = tuple(slice(int(i0[i]), int(i1[i])) for i in range(3))
        X, Y, Z = np.meshgrid(self.ax[0][sl[0]], self.ax[1][sl[1]], self.ax[2][sl[2]], indexing="ij", sparse=True)
        dn = np.broadcast_to(prim(X, Y, Z), self.d[sl].shape).astype(F32)
        return sl, dn

    def add(self, prim, k=0.0, mirror=False):
        for p in ((prim, mir(prim)) if mirror else (prim,)):
            sl, dn = self._block(p, k + 2 * self.v)
            self.d[sl] = smin(self.d[sl], dn, k) if k > 0 else np.minimum(self.d[sl], dn)
        return self

    def cut(self, prim, k=0.0, mirror=False):
        for p in ((prim, mir(prim)) if mirror else (prim,)):
            sl, dn = self._block(p, k + 2 * self.v)
            self.d[sl] = smax(self.d[sl], -dn, k) if k > 0 else np.maximum(self.d[sl], -dn)
        return self

    def to_arrays(self):
        import openvdb as vdb
        g = vdb.FloatGrid(10.0)
        g.copyFromArray(np.ascontiguousarray(self.d))
        g.transform = vdb.createLinearTransform(voxelSize=self.v)
        pts, quads = g.convertToQuads(isovalue=0.0)
        pts = np.asarray(pts, dtype=np.float64) + self.lo
        return pts, np.asarray(quads, dtype=np.int32)


def mesh_from_arrays(name, pts, quads):
    """Cree (ou remplace) un objet maillage a partir de points et de quads."""
    old = bpy.data.objects.get(name)
    if old:
        me_old = old.data
        bpy.data.objects.remove(old, do_unlink=True)
        if me_old.users == 0:
            bpy.data.meshes.remove(me_old)
    me = bpy.data.meshes.new(name)
    nq = len(quads)
    me.vertices.add(len(pts))
    me.vertices.foreach_set("co", np.asarray(pts, dtype=F32).ravel())
    me.loops.add(nq * 4)
    me.loops.foreach_set("vertex_index", np.asarray(quads, dtype=np.int32).ravel())
    me.polygons.add(nq)
    me.polygons.foreach_set("loop_start", np.arange(0, nq * 4, 4, dtype=np.int32))
    me.polygons.foreach_set("loop_total", np.full(nq, 4, dtype=np.int32))
    me.polygons.foreach_set("use_smooth", np.ones(nq, dtype=bool))
    me.update(calc_edges=True)
    me.validate()
    ob = bpy.data.objects.new(name, me)
    get_collection("Bull").objects.link(ob)
    return ob


def spline(points, radii, sub):
    """Courbe de Catmull-Rom passant par les points (et rayons interpoles), `sub` segments par intervalle."""
    P = [np.array(p, dtype=float) for p in points]
    ext = [P[0] * 2 - P[1]] + P + [P[-1] * 2 - P[-2]]
    out_p, out_r = [], []
    for i in range(len(P) - 1):
        p0, p1, p2, p3 = ext[i], ext[i + 1], ext[i + 2], ext[i + 3]
        for s_ in range(sub):
            t = s_ / sub
            out_p.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                                + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t))
            out_r.append(radii[i] + (radii[i + 1] - radii[i]) * t)
    out_p.append(P[-1])
    out_r.append(radii[-1])
    return out_p, out_r


def get_collection(name):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    return col


def principled(mat):
    # Blender en francais traduit le nom des noeuds : on cherche par type
    return next(n for n in mat.node_tree.nodes if n.bl_idname == "ShaderNodeBsdfPrincipled")


def srgb(hexstr):
    hexstr = hexstr.lstrip('#')
    return tuple(int(hexstr[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


# --------------------------------------------------------------------------------------
# Reperes du modele (releves sur l'image de reference, hauteur totale 2,20 m cornes comprises)
# --------------------------------------------------------------------------------------
EYE_C = np.array((0.158, -0.272, 1.722))      # centre du globe oculaire gauche
EYE_R = 0.100
MUZZLE_C = np.array((0.0, -0.375, 1.478))     # museau creme
MUZZLE_HALF = np.array((0.205, 0.125, 0.147))
MUZZLE_RAD = 0.118
EAR_ROOT = np.array((0.275, -0.07, 1.762))
EAR_TIP = np.array((0.505, -0.045, 1.800))
HORN_PTS = [(0.225, -0.07, 1.92), (0.30, -0.07, 1.985), (0.345, -0.075, 2.045), (0.376, -0.082, 2.12), (0.380, -0.088, 2.17), (0.372, -0.092, 2.205)]
HORN_RAD = [0.078, 0.066, 0.054, 0.040, 0.025, 0.008]
BELLY_C = np.array((0.0, -0.33, 0.966))       # centre de la tache creme du ventre (vue de face)
BELLY_RX, BELLY_RZ = 0.33, 0.297
HOOF_X = 0.218
HOOF_TOP = 0.176
TAIL_PTS = [(0, 0.24, 0.80), (0, 0.35, 0.80), (0, 0.43, 0.75), (0, 0.47, 0.68), (0, 0.48, 0.61)]
