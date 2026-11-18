# Planche O01 — armes de ferme : Pelle (SM_Item_Shovel), Bâton (SM_Item_Stick), Fourche (SM_Item_Pitchfork).
# Planches : GameDesign/99_Attachments/Objets/Planches/Planche_O01_Pelle.png (référence de la pelle)
#            et Planche_O01_Armes_Ferme.png (bâton, fourche).
#
# Lancer dans Blender 5.2, fichier ArtSource/Objets/items_o01.blend :
#   exec(open(r"<dépôt>\ArtSource\Objets\build_items_o01.py", encoding="utf-8").read(), {"DO_EXPORT": True})
# Pose : manche vertical, partie utile en haut, face utile vers -Y, origine = centre du poing (§3 du plan).
import importlib
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

_DIR = os.path.join(os.environ.get("TEAMTIMO_PROJECT", r"C:\Users\gabin\Documents\Projets Unity\TeamTimo"), "ArtSource", "Objets")
if _DIR not in sys.path:
    sys.path.insert(0, _DIR)
import items_lib as il  # noqa: E402

importlib.reload(il)
from items_lib import TAU, band, band_edge, band_mat, cell  # noqa: E402

COLL = "Items_O01"


def rot_x(deg):
    return Matrix.Rotation(math.radians(deg), 4, "X")


def rot_y(deg):
    return Matrix.Rotation(math.radians(deg), 4, "Y")


def wood_lathe_uv(name, u_scale=2.5, u_off=0.0):
    return lambda a, z, r, k: band_mat(name, z / u_scale + u_off, a / TAU)


def wood_tube_uv(name, u_scale=2.5, u_off=0.0):
    return lambda a, length, r: band_mat(name, length / u_scale + u_off, a / TAU)


def cap_cell(name, rad=0.45):
    """Fermeture de lathe : (x, y) local -> case ; rad = rayon de la pièce (pour remplir le disque de la case)."""
    return lambda x, y: cell(name, 0.5 + 0.5 * x / rad, 0.5 + 0.5 * y / rad, margin=0.02)


def cap_ring(name):
    """Fermeture de tube : angle -> case (disque)."""
    return lambda a: cell(name, 0.5 + 0.47 * math.cos(a), 0.5 + 0.47 * math.sin(a), margin=0.0)


def rivet(mb, pos, r=0.06):
    """Tête de rivet bombée, axe vers -Y."""
    il.lathe(mb, [(r, 0.0), (r, 0.015), (r * 0.75, 0.04), (0.0, 0.052)], 8,
             Matrix.Translation(Vector(pos)) @ rot_x(90), lambda a, z, rr, k: band_edge("iron_blue", a / TAU, 0.5))


# ================================================================== PELLE (4,2 m, lame 1,1 × 1,3)
SH_GRIP_Z = 1.05      # centre du poing (juste au-dessus de la poignée en D)
SH_ZS = 2.90          # épaulement de la lame
SH_H = 1.30           # hauteur de la lame
SH_HW = 0.55          # demi-largeur de la lame
SH_TH = 0.07          # épaisseur de la lame
SH_BOW = 0.08         # creux de la lame (bords vers l'avant)


def blade_half_width(z):
    zz = z / SH_H
    if zz < 0.58:
        w = SH_HW - 0.06 * zz / 0.58
    else:
        k = (zz - 0.58) / 0.42
        w = (SH_HW - 0.06) * math.sqrt(max(0.0, 1 - k * k))
    w = max(w, 0.06)
    return w + 0.008 * math.sin(z * 23 + 1.0) * min(1.0, w / 0.2)     # bords un peu cabossés


def blade_point(s, t, side):
    z = t * SH_H
    x = (2 * s - 1) * blade_half_width(z)
    y = side * SH_TH / 2 - SH_BOW * (x / SH_HW) ** 2
    return Vector((x, y, SH_ZS + z))


def build_blade(mb, ns=6, nt=8, rim=3):
    mb.grid(lambda s, t: blade_point(s, t, -1), ns, nt, 0,
            lambda s, t, p: band_mat("iron_blue", p.x / 1.2, t), smooth=True, ref=lambda p: Vector((0, -1, 0)))
    mb.grid(lambda s, t: blade_point(s, t, 1), ns, nt, 0,
            lambda s, t, p: band_mat("iron_blue", p.x / 1.2 + 0.5, t), smooth=True, ref=lambda p: Vector((0, 1, 0)))
    # Bord arrondi (boucle de l'épaulement, côté droit, pointe, côté gauche) : arête usée plus claire.
    loop = [(i / ns, 0.0) for i in range(ns)] + [(1.0, j / nt) for j in range(nt)] + \
           [(1 - i / ns, 1.0) for i in range(ns)] + [(0.0, 1 - j / nt) for j in range(nt)]
    n = len(loop)
    F = [blade_point(s, t, -1) for s, t in loop]
    B = [blade_point(s, t, 1) for s, t in loop]
    outs = []
    for i in range(n):
        tg = (F[(i + 1) % n] - F[i - 1])
        o = Vector((tg.z, 0, -tg.x)).normalized()
        outs.append(o)
    arc = 0.0
    for i in range(n):
        j = (i + 1) % n
        seg = (F[j] - F[i]).length
        for r in range(rim):
            def P(k, rr):
                f, b, o = (F[k], B[k], outs[k])
                m = (f + b) / 2
                ph = math.pi * rr / rim
                return m + (f - m) * math.cos(ph) + o * (SH_TH * 0.55) * math.sin(ph)
            q = [P(i, r), P(j, r), P(j, r + 1), P(i, r + 1)]
            uvs = [band_edge("iron_blue", arc / 1.5, r / rim), band_edge("iron_blue", (arc + seg) / 1.5, r / rim),
                   band_edge("iron_blue", (arc + seg) / 1.5, (r + 1) / rim), band_edge("iron_blue", arc / 1.5, (r + 1) / rim)]
            mb.face(q, 0, uvs, smooth=True, ref=(outs[i] + outs[j]))
        arc += seg


def build_shovel(coll):
    mb = il.MB()
    # Poignée en D : traverse (bois de bout visible) + deux bras qui rejoignent le manche.
    il.lathe(mb, [(0.074, -0.40), (0.08, -0.36), (0.08, 0.36), (0.074, 0.40)], 8,
             Matrix.Translation((0, 0, 0.08)) @ rot_y(90), wood_lathe_uv("wood_honey", u_off=0.3),
             cap_top=cap_cell("endgrain", 0.08), cap_bottom=cap_cell("endgrain", 0.08))
    for sx in (-1, 1):
        pts = [(sx * 0.33, 0, 0.08), (sx * 0.355, 0, 0.30), (sx * 0.30, 0, 0.50), (sx * 0.18, 0, 0.66),
               (sx * 0.06, 0, 0.80), (sx * 0.02, 0, 0.90)]
        il.tube(mb, il.smooth_path(pts, 2), 0.068, 6, wood_tube_uv("wood_honey", u_off=0.6 if sx > 0 else 0.1),
                side_axis=(0, 1, 0))
    # Manche.
    il.lathe(mb, [(0.10, 0.70), (0.104, 1.70), (0.10, SH_ZS - 0.42)], 8, Matrix.Identity(4),
             wood_lathe_uv("wood_honey"), cap_bottom=cap_cell("endgrain", 0.1))
    # Douille (fermée en haut sous la lame) + languettes avant et arrière + rivets.
    il.lathe(mb, [(0.10, SH_ZS - 0.50), (0.128, SH_ZS - 0.50), (0.14, SH_ZS - 0.44), (0.15, SH_ZS - 0.06),
                  (0.12, SH_ZS + 0.04), (0.0, SH_ZS + 0.07)], 8, Matrix.Identity(4),
             lambda a, z, r, k: band_edge("iron_blue", a / TAU) if k in (0, 3) else band_mat("iron_blue", a / TAU, (z - SH_ZS + 0.5) / 0.6))
    for side, (y0, y1) in ((-1, (-SH_TH / 2 - 0.065, -SH_TH / 2 + 0.01)), (1, (SH_TH / 2 - 0.01, SH_TH / 2 + 0.05))):
        L = 0.55 if side < 0 else 0.40
        zc, rx, rz = SH_ZS + L * 0.45, 0.145, L * 0.55
        prof = [(-0.165, SH_ZS - 0.12), (0.165, SH_ZS - 0.12)] +                [(rx * math.cos(math.pi * i / 6), zc + rz * math.sin(math.pi * i / 6)) for i in range(7)]
        mb.add(il.vl.slab_bm(prof, y0, y1, 0.025, cap_bottom=True), Matrix.Identity(4), 0,
               lambda lc, wc, n: band_edge("iron_blue", lc.x) if abs(n.y) < 0.9 else band_mat("iron_blue", lc.x / 1.2 + 0.2, (lc.z - SH_ZS) / 0.7))
    rivet(mb, (0, -0.148, SH_ZS - 0.30))
    rivet(mb, (0, -SH_TH / 2 - 0.063, SH_ZS + 0.18))
    build_blade(mb)
    return il.finish(mb, "Shovel", coll, origin=(0, 0, SH_GRIP_Z))


# ================================================================== BÂTON (3,6 m × ⌀ 0,35)
def stick_axis(z):
    return Vector((0.07 * math.sin(z * 1.4 + 0.3), 0.04 * math.sin(z * 2.1 + 1.0), z))


def stick_radius(z):
    return 0.172 + 0.014 * math.sin(z * 3.1) + 0.022 * max(0.0, 1 - z / 0.5) - 0.012 * max(0.0, (z - 3.0) / 0.6)


def build_stick(coll):
    mb = il.MB()
    zs = [0.0, 0.25, 0.6, 1.0, 1.4, 1.8, 2.2, 2.6, 3.0, 3.35, 3.6]
    il.tube(mb, [stick_axis(z) for z in zs], [stick_radius(z) for z in zs], 8, wood_tube_uv("bark", 2.0),
            cap_start=cap_ring("endgrain_bark"), cap_end=cap_ring("endgrain_bark"))
    # Nœuds : moignons de branches coupées (bois de bout clair).
    for z, phi, ln in ((1.55, 0, 0.12), (2.35, 180, 0.13), (3.0, 15, 0.10), (2.0, -90, 0.09), (1.15, 160, 0.08)):
        c = stick_axis(z)
        d = Vector((math.cos(math.radians(phi)), math.sin(math.radians(phi)), 0.7)).normalized()
        r = stick_radius(z)
        il.tube(mb, [c + d * 0.04, c + d * (r * 0.9), c + d * (r + ln)], [0.13, 0.11, 0.095], 6,
                wood_tube_uv("bark", 2.0, u_off=phi / 360), side_axis=(0, 0, 1), cap_end=cap_ring("endgrain_bark"))
    # Ficelle rouge enroulée à la poignée (manchon légèrement plus épais, croisillons peints).
    zw = [0.29, 0.31, 0.45, 0.6, 0.75, 0.9, 1.04, 1.06]
    rw = [stick_radius(z) + (0.006 if k in (0, len(zw) - 1) else 0.028) for k, z in enumerate(zw)]
    il.tube(mb, [stick_axis(z) for z in zw], rw, 8, lambda a, length, r: band_mat("string_red", length * 0.26, a / TAU))
    return il.finish(mb, "Stick", coll, origin=stick_axis(0.68))


# ================================================================== FOURCHE (4,6 m, tête 1,3 de large)
PF_GRIP_Z = 1.0


def tine_bend(p):
    """Les dents s'incurvent un peu vers l'avant (-Y) en montant."""
    k = max(0.0, (p.z - 3.95) / 0.67)
    return Vector((p.x, p.y - 0.16 * k * k, p.z))


def build_pitchfork(coll):
    mb = il.MB()
    il.lathe(mb, [(0.0, 0.0), (0.06, 0.012), (0.092, 0.045), (0.10, 0.09), (0.103, 1.8), (0.10, 3.30)], 8,
             Matrix.Identity(4), wood_lathe_uv("wood_honey", u_off=0.45))
    # Bague dorée + col en métal clair.
    il.lathe(mb, [(0.10, 3.22), (0.135, 3.24), (0.148, 3.28), (0.148, 3.54), (0.135, 3.58), (0.10, 3.60)], 10,
             Matrix.Identity(4), lambda a, z, r, k: cell("brass", a / TAU, (z - 3.22) / 0.38))
    il.lathe(mb, [(0.10, 3.56), (0.10, 3.60), (0.085, 3.66), (0.06, 3.70)], 8, Matrix.Identity(4),
             lambda a, z, r, k: band_mat("steel_light", a / TAU, (z - 3.56) / 0.2))
    # Tête : U (dents extérieures) + dent centrale, bouts arrondis.
    steel = lambda a, length, r: band_mat("steel_light", length / 1.5, a / TAU)  # noqa: E731
    tip_cap = lambda a: band_edge("steel_light", 0.5 + 0.1 * math.cos(a), 0.5)   # noqa: E731
    u_pts = [(-0.56, 0, 4.60), (-0.56, 0, 4.25), (-0.55, 0, 3.98), (-0.47, 0, 3.80), (-0.28, 0, 3.71), (0, 0, 3.68),
             (0.28, 0, 3.71), (0.47, 0, 3.80), (0.55, 0, 3.98), (0.56, 0, 4.25), (0.56, 0, 4.60)]
    path = il.smooth_path(u_pts, 3)
    path = [Vector((-0.56, 0, 4.655)), Vector((-0.56, 0, 4.635))] + path + [Vector((0.56, 0, 4.635)), Vector((0.56, 0, 4.655))]
    radii = [0.03, 0.058] + [0.072] * (len(path) - 4) + [0.058, 0.03]
    il.tube(mb, [tine_bend(p) for p in path], radii, 7, steel, side_axis=(0, 1, 0), cap_start=tip_cap, cap_end=tip_cap)
    c_pts = [Vector((0, 0, z)) for z in (3.62, 3.85, 4.1, 4.35, 4.6, 4.635, 4.655)]
    il.tube(mb, [tine_bend(p) for p in c_pts], [0.072] * 5 + [0.058, 0.03], 7, steel, side_axis=(1, 0, 0), cap_end=tip_cap)
    # Ruban rouge noué sous la tête : anneau, nœud devant, deux pans qui pendent.
    il.lathe(mb, [(0.10, 3.02), (0.122, 3.04), (0.125, 3.15), (0.10, 3.17)], 8, Matrix.Identity(4),
             lambda a, z, r, k: cell("cloth_red", a / TAU, (z - 3.02) / 0.15))
    il.box(mb, (0, -0.13, 3.10), (0.14, 0.08, 0.12), il.cell_uvf("cloth_red"), bevel=0.03)
    cloth = lambda i, length: cell("cloth_red", 0.2 + 0.6 * (i in (1, 2)), length / 0.7)  # noqa: E731
    il.flat_strip(mb, il.smooth_path([(-0.04, -0.15, 3.09), (-0.11, -0.19, 2.95), (-0.17, -0.18, 2.78), (-0.20, -0.15, 2.58)], 2),
                  0.11, 0.022, (1, 0, 0.35), cloth)
    il.flat_strip(mb, il.smooth_path([(0.04, -0.15, 3.09), (0.10, -0.20, 2.97), (0.15, -0.21, 2.84), (0.16, -0.19, 2.70)], 2),
                  0.10, 0.022, (1, 0, -0.3), cloth)
    return il.finish(mb, "Pitchfork", coll, origin=(0, 0, PF_GRIP_Z))


ASSETS = [("SM_Item_Shovel", build_shovel), ("SM_Item_Stick", build_stick), ("SM_Item_Pitchfork", build_pitchfork)]


def main(do_export=False, only=None):
    coll = il.reset_collection(COLL)
    rep, x = {}, 0.0
    for fbx, fn in ASSETS:
        if only and fbx not in only:
            continue
        ob = fn(coll)
        if do_export:
            il.export([ob], fbx)
        rep[fbx] = il.report(ob)
        ob.location.x = x
        x += 2.0
    return rep


if __name__ == "__main__" or "DO_EXPORT" in globals():
    result = main(globals().get("DO_EXPORT", False), globals().get("ONLY"))
