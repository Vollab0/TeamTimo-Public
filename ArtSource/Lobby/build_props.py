# Construit les petits accessoires du village du Lobby (planches P12 à P15) : un FBX par asset.
#   SM_Campfire, SM_Trough, SM_HayBale, SM_HayRack, SM_HayPile, SM_Garden_Bed, SM_LanternPost, SM_Sign_Small,
#   SM_Crate_01 (caisse fermée), SM_Crate_02 (cagette haute), SM_Crate_03 (cagette plate), SM_Barrel,
#   SM_Woodpile, SM_Stump, SM_ChoppingBlock, SM_LogRound, SM_Wheelbarrow, SM_Cart, SM_Basket, SM_Sack, SM_Pot
# Planches : GameDesign/99_Attachments/Lobby/Planches/Planche_P12 à P15.
# Les fonctions crate / pot / sack / basket servent aussi à garnir l'étal rayé (build_market.py).
#
# Lancer dans Blender 5.2, fichier ArtSource/Lobby/props.blend :
#   exec(open(r"<dépôt>\ArtSource\Lobby\build_props.py", encoding="utf-8").read(), {"DO_EXPORT": True})
# Repère : 1 unité = 1 m du jeu, pivot au sol au centre, devant vers -Y, Z haut.
import importlib
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

_DIR = os.path.join(os.environ.get("TEAMTIMO_PROJECT", "C:/Users/gabin/Documents/Projets Unity/TeamTimo"), "ArtSource", "Lobby")
if _DIR not in sys.path:
    sys.path.insert(0, _DIR)
import village_lib as vl  # noqa: E402
import build_forge as bf  # noqa: E402

importlib.reload(vl)
importlib.reload(bf)
from village_lib import box_bm, slab_bm  # noqa: E402
from build_forge import Asset, _box_uv, barrel  # noqa: E402

COLL = "Village_Props"
PLANKS = 8.0


def M(t=(0, 0, 0), rz=0.0, s=1.0, rx=0.0, ry=0.0):
    """Matrice : translation t, rotations (degrés) X puis Y puis Z appliquées à un objet d'échelle s."""
    return (Matrix.Translation(Vector(t)) @ Matrix.Rotation(math.radians(rz), 4, "Z") @
            Matrix.Rotation(math.radians(ry), 4, "Y") @ Matrix.Rotation(math.radians(rx), 4, "X") @ Matrix.Scale(s, 4))


def box_at(a, mm, center, size, key, bevel=0.0, uv=None):
    a.mb.add(box_bm(size, bevel), mm @ Matrix.Translation(Vector(center)), a.m(key), uv or _box_uv(PLANKS))


def plank(a, mm, p0, p1, yaxis, dy, dz, key="wood", bevel=0.06, wobble=0.6):
    """Planche / poutre (fil du bois le long de p0 -> p1) dans le repère mm."""
    vl.Builder.beam(a, a.mb, mm @ Vector(p0), mm @ Vector(p1), mm.to_3x3() @ Vector(yaxis), dy, dz, a.m(key), None, bevel, 1, wobble, 0.0)


def auto_face(a, pts, key, ref, tile=PLANKS):
    """Polygone avec UV en projection boîte (d'après sa normale) ; ref = sens voulu de la normale."""
    pts = [Vector(p) for p in pts]
    n = (pts[1] - pts[0]).cross(pts[-1] - pts[0])
    ax = max(range(3), key=lambda i: abs(n[i]))
    i, j = [(1, 2), (0, 2), (0, 1)][ax]
    a.mb.face(pts, a.m(key), [(p[i] / tile, p[j] / tile) for p in pts], ref=ref)


def open_box(a, bot, top, wall, key, tile=PLANKS, floor=True):
    """Bac ouvert (auge, brouette) : bot / top = 4 coins (sens trigo vu de dessus) du fond et du bord, épaisseur wall."""
    bot, top = [Vector(p) for p in bot], [Vector(p) for p in top]
    c = sum(bot + top, Vector()) / 8

    def inset(ring, z_off):
        cc = sum(ring, Vector()) / 4
        return [cc + (p - cc) * (1 - wall / max(0.01, (p - cc).xy.length)) + Vector((0, 0, z_off)) for p in ring]
    ibot, itop = inset(bot, wall), inset(top, 0.0)
    auto_face(a, bot, key, (0, 0, -1), tile)
    if floor:
        auto_face(a, ibot, key, (0, 0, 1), tile)
    for k in range(4):
        j = (k + 1) % 4
        o = [bot[k], bot[j], top[j], top[k]]
        oc = sum(o, Vector()) / 4
        auto_face(a, o, key, oc - c, tile)
        ii = [ibot[k], ibot[j], itop[j], itop[k]]
        ic = sum(ii, Vector()) / 4
        auto_face(a, ii, key, c - ic, tile)
        auto_face(a, [top[k], top[j], itop[j], itop[k]], key, (0, 0, 1), tile)
    return ibot, itop


# ------------------------------------------------------------------ générateurs réutilisables
def crate_closed(a, mm, s=3.0):
    """Caisse fermée détaillée (proportionnelle à s) : doublure, 5 planches 3D par face, cadre 12 arêtes et diagonales encastrées."""
    h = s / 2
    sc = s / 3.0
    # Doublure interne pour boucher les jours entre planches.
    box_at(a, mm, (0, 0, h), (s - 0.56 * sc, s - 0.56 * sc, s - 0.56 * sc), "planks", 0.02 * sc)

    # Panneaux de 5 planches 3D sur les 4 faces latérales et le dessus/dessous (face extérieure à h - 0.19*sc).
    n_planks = 5
    span = s - 0.62 * sc
    pw = span / n_planks - 0.02 * sc
    pd = 0.16 * sc
    p_off = h - 0.27 * sc
    bev_p = 0.04 * sc
    for sy in (-1, 1):
        for k in range(n_planks):
            x = -span / 2 + span * (k + 0.5) / n_planks
            plank(a, mm, (x, sy * p_off, 0.28 * sc), (x, sy * p_off, s - 0.28 * sc), (0, 1, 0), pd, pw, bevel=bev_p, wobble=0.15)
    for sx in (-1, 1):
        for k in range(n_planks):
            y = -span / 2 + span * (k + 0.5) / n_planks
            plank(a, mm, (sx * p_off, y, 0.28 * sc), (sx * p_off, y, s - 0.28 * sc), (1, 0, 0), pd, pw, bevel=bev_p, wobble=0.15)
    for z_face in (0.27 * sc, s - 0.27 * sc):
        for k in range(n_planks):
            y = -span / 2 + span * (k + 0.5) / n_planks
            plank(a, mm, (-h + 0.28 * sc, y, z_face), (h - 0.28 * sc, y, z_face), (0, 0, 1), pd, pw, bevel=bev_p, wobble=0.15)

    # Cadre extérieur continu sur les 12 arêtes (nu extérieur à ±h, épaisseur 0.36*sc).
    fc = h - 0.18 * sc
    ft = 0.36 * sc
    bev_f = 0.07 * sc
    for sx in (-1, 1):
        for sy in (-1, 1):
            plank(a, mm, (sx * fc, sy * fc, 0.0), (sx * fc, sy * fc, s), (1, 0, 0), ft, ft, bevel=bev_f, wobble=0.0)
    for z in (0.18 * sc, s - 0.18 * sc):
        for sy in (-1, 1):
            plank(a, mm, (-h + 0.22 * sc, sy * fc, z), (h - 0.22 * sc, sy * fc, z), (0, 1, 0), ft, ft, bevel=bev_f, wobble=0.0)
        for sx in (-1, 1):
            plank(a, mm, (sx * fc, -h + 0.22 * sc, z), (sx * fc, h - 0.22 * sc, z), (1, 0, 0), ft, ft, bevel=bev_f, wobble=0.0)

    # Traverses diagonales encastrées dans le cadre sur les 4 faces latérales et sur le dessus.
    d_off = h - 0.17 * sc
    dt, dw, bev_d = 0.14 * sc, 0.28 * sc, 0.04 * sc
    for sy in (-1, 1):
        plank(a, mm, (-h + 0.28 * sc, sy * d_off, 0.28 * sc), (h - 0.28 * sc, sy * d_off, s - 0.28 * sc), (0, 1, 0), dt, dw, bevel=bev_d, wobble=0.0)
    for sx in (-1, 1):
        plank(a, mm, (sx * d_off, h - 0.28 * sc, 0.28 * sc), (sx * d_off, -h + 0.28 * sc, s - 0.28 * sc), (1, 0, 0), dt, dw, bevel=bev_d, wobble=0.0)
    plank(a, mm, (-h + 0.28 * sc, -h + 0.28 * sc, s - 0.17 * sc), (h - 0.28 * sc, h - 0.28 * sc, s - 0.17 * sc), (0, 0, 1), dt, dw, bevel=bev_d, wobble=0.0)


def crate_slatted(a, mm, s=3.0, height=3.0, slats=3):
    h = s / 2
    n_floor = 4
    fspan = s - 0.45
    fw = fspan / n_floor - 0.03
    for k in range(n_floor):
        y = -fspan / 2 + fspan * (k + 0.5) / n_floor
        plank(a, mm, (-h + 0.16, y, 0.12), (h - 0.16, y, 0.12), (0, 0, 1), 0.16, fw, bevel=0.035, wobble=0.3)
    for sx in (-1, 1):
        for sy in (-1, 1):
            plank(a, mm, (sx * (h - 0.18), sy * (h - 0.18), 0), (sx * (h - 0.18), sy * (h - 0.18), height + 0.22), (1, 0, 0), 0.34, 0.34, bevel=0.06, wobble=0.3)
    for k in range(slats):
        z = 0.34 + (height - 0.54) * (k + 0.5) / slats + 0.08
        sh = min(0.55, (height - 0.25) / max(1, slats) * 0.62)
        for sy in (-1, 1):
            plank(a, mm, (-h + 0.08, sy * (h - 0.08), z), (h - 0.08, sy * (h - 0.08), z), (0, 1, 0), 0.14, sh, bevel=0.035, wobble=0.5)
        for sx in (-1, 1):
            plank(a, mm, (sx * (h - 0.08), -h + 0.14, z), (sx * (h - 0.08), h - 0.14, z), (1, 0, 0), 0.14, sh, bevel=0.035, wobble=0.5)


def pot(a, mm, s=1.0):
    prof = [(0.45, 0.0), (0.78, 0.3), (0.85, 0.7), (0.62, 1.1), (0.5, 1.2), (0.62, 1.32), (0.5, 1.36), (0.4, 1.1)]
    prof = [(r * s, z * s) for r, z in prof]
    vl.lathe(a.mb, prof, 10, mm, a.m("clay"), lambda ang, z, r, k: (ang / 3.0, z / 2.0),
             cap_top=(a.m("clay"), lambda x, y: (0.9, 0.9)), cap_bottom=(a.m("clay"), lambda x, y: (x, y)))


def sack(a, mm, s=1.0):
    prof = [(0.75, 0.0), (1.05, 0.25), (1.18, 0.8), (1.05, 1.45), (0.5, 1.9), (0.32, 2.05), (0.52, 2.3), (0.36, 2.55), (0.0, 2.6)]
    prof = [(r * s, z * s) for r, z in prof]
    vl.lathe(a.mb, prof, 10, mm, a.m("burlap"), lambda ang, z, r, k: (ang * 0.6, z / 2.0),
             cap_bottom=(a.m("burlap"), lambda x, y: (x * 0.3, y * 0.3)), a0=0.3)
    vl.lathe(a.mb, [(0.3 * s, 2.0 * s), (0.42 * s, 2.06 * s), (0.42 * s, 2.16 * s), (0.3 * s, 2.2 * s)], 8, mm,
             a.m("hay"), lambda ang, z, r, k: (ang * 0.3, z))          # ficelle


def basket(a, mm, s=1.0):
    prof = [(0.75, 0.0), (1.0, 0.25), (1.05, 1.25), (1.12, 1.35), (1.0, 1.45), (0.92, 1.35), (0.95, 1.28), (0.72, 0.25)]
    prof = [(r * s, z * s) for r, z in prof]
    vl.lathe(a.mb, prof, 12, mm, a.m("hay"), lambda ang, z, r, k: (ang * 0.8, z * 0.9),
             cap_top=(a.m("hay"), lambda x, y: (x * 0.4, y * 0.4)), cap_bottom=(a.m("hay"), lambda x, y: (x * 0.4, y * 0.4)))
    path = [mm @ Vector((0.98 * s * math.cos(t), 0, s * (1.35 + 1.1 * math.sin(t)))) for t in [math.pi * i / 8 for i in range(9)]]
    vl.tube(a.mb, path, 0.08 * s, 6, a.m("hay"), lambda ang, ln, r: (ang * 0.1, ln), side_axis=mm.to_3x3() @ Vector((0, 1, 0)))


def log(a, mm, length, r, sides=6, bark="wood"):
    """Bûche / rondin le long de l'axe Z local, bouts en bois de bout."""
    eg = (a.m("endgrain"), lambda x, y: (0.5 + x / (2.2 * r), 0.5 + y / (2.2 * r)))
    vl.lathe(a.mb, [(r, -length / 2), (r * 1.02, 0.0), (r, length / 2)], sides, mm, a.m(bark),
             lambda ang, z, rr, k: (z / vl.WOOD_U_METERS, vl.band_v(k % 3, ang / (2 * math.pi))), smooth=True,
             cap_top=eg, cap_bottom=eg, a0=a.rng.uniform(0, 1))


# ------------------------------------------------------------------ assets
def build_campfire(coll):
    a = Asset("Campfire", ["stone", "wood", "embers", "endgrain"], 71)
    suv = _box_uv(16.0)
    n = 11
    for i in range(n):
        t = 2 * math.pi * (i + a.jit(0.15)) / n
        mm = M((2.05 * math.cos(t), 2.05 * math.sin(t), 0.32), rz=math.degrees(t) + 90 + a.jit(8))
        a.mb.add(box_bm((1.15 + a.jit(0.12), 0.85, 0.72 + a.jit(0.1)), 0.24, 1), mm, a.m("stone"), suv)
    vl.lathe(a.mb, [(1.75, 0.02), (1.3, 0.22), (0.6, 0.38), (0.0, 0.42)], 10, Matrix.Identity(4), a.m("embers"),
             lambda ang, z, r, k: (r * math.cos(ang) / 3.0, r * math.sin(ang) / 3.0))
    for i in range(5):
        t = 2 * math.pi * i / 5 + 0.3
        base = Vector((1.35 * math.cos(t), 1.35 * math.sin(t), 0.15))
        top = Vector((0.12 * math.cos(t), 0.12 * math.sin(t), 2.1 + a.jit(0.15)))
        d = top - base
        mm = Matrix.Translation((base + top) / 2) @ d.to_track_quat("Z", "X").to_matrix().to_4x4()
        log(a, mm, d.length + 0.3, 0.24)
    a.collider([(2.7 * math.cos(2 * math.pi * i / 10), 2.7 * math.sin(2 * math.pi * i / 10), z) for i in range(10) for z in (0.0, 0.7)])
    return a.finish(coll)


def build_trough(coll):
    a = Asset("Trough", ["wood", "planks", "water"], 72)
    L, W = 6.0, 1.7
    bot = [(-L / 2 + 0.3, -W / 2 + 0.25, 0.95), (L / 2 - 0.3, -W / 2 + 0.25, 0.95), (L / 2 - 0.3, W / 2 - 0.25, 0.95), (-L / 2 + 0.3, W / 2 - 0.25, 0.95)]
    top = [(-L / 2, -W / 2, 2.0), (L / 2, -W / 2, 2.0), (L / 2, W / 2, 2.0), (-L / 2, W / 2, 2.0)]
    open_box(a, bot, top, 0.28, "planks", tile=PLANKS)
    a.mb.face([(-L / 2 + 0.35, -W / 2 + 0.3, 1.8), (L / 2 - 0.35, -W / 2 + 0.3, 1.8), (L / 2 - 0.35, W / 2 - 0.3, 1.8), (-L / 2 + 0.35, W / 2 - 0.3, 1.8)],
              a.m("water"), [(0, 0), (1.5, 0), (1.5, 0.4), (0, 0.4)], ref=(0, 0, 1))
    for sx in (-1, 1):
        x = sx * (L / 2 - 0.9)
        for sy in (-1, 1):
            plank(a, Matrix.Identity(4), (x + sx * 0.2 * sy, sy * 1.05, 0.0), (x - sx * 0.2 * sy, -sy * 0.55, 1.15), (1, 0, 0), 0.38, 0.38, wobble=1.0)
    a.collider_box((0, 0, 1.0), (L, W + 0.4, 2.0))
    return a.finish(coll)


def build_haybale(coll):
    a = Asset("HayBale", ["hay", "burlap"], 73)
    a.box((0, 0, 0.78), (3.0, 1.7, 1.56), "hay", 0.2, uv=_box_uv(3.0))
    for x in (-0.75, 0.75):
        a.box((x, 0, 0.78), (0.12, 1.78, 1.64), "burlap", 0.04, uv=_box_uv(2.0))
    a.collider_box((0, 0, 0.78), (3.0, 1.7, 1.56))
    return a.finish(coll)


def build_garden_bed(coll):
    a = Asset("GardenBed", ["wood", "soil", "leaf"], 74)
    S, rows = 12.0, 5
    for sy in (-1, 1):
        plank(a, Matrix.Identity(4), (-S / 2, sy * (S / 2 - 0.25), 0.25), (S / 2, sy * (S / 2 - 0.25), 0.25), (0, 1, 0), 0.5, 0.6, wobble=0.5)
    for sx in (-1, 1):
        plank(a, Matrix.Identity(4), (sx * (S / 2 - 0.25), -S / 2 + 0.5, 0.25), (sx * (S / 2 - 0.25), S / 2 - 0.5, 0.25), (1, 0, 0), 0.5, 0.6, wobble=0.5)
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.box((sx * (S / 2 - 0.2), sy * (S / 2 - 0.2), 0.3), (0.75, 0.75, 0.9), "wood", 0.1, uv=vl.wood_uv((0.75, 0.75, 0.9), 0, 1.0))
    inner = S / 2 - 0.5

    def soil(s, t):
        x = -inner + 2 * inner * s
        return Vector((x, -inner + 2 * inner * t, 0.32 + 0.16 * math.cos(2 * math.pi * rows * s)))
    a.mb.grid(soil, rows * 8, 2, a.m("soil"), lambda s, t, p: (p.x / 6.0, p.y / 6.0), ref=lambda p: Vector((0, 0, 1)))
    # Jeunes pousses sur les buttes : 2 feuilles croisées, visibles des deux côtés.
    for i in range(rows):
        x = -inner + 2 * inner * i / rows
        for j in range(5):
            y = -inner + 1.1 + (2 * inner - 2.2) * j / 4 + a.jit(0.3)
            c = Vector((x + a.jit(0.15), y, 0.44))
            ang0 = a.rng.uniform(0, 360)
            for k in range(3):                       # 3 feuilles en losange, ouvertes en V
                ang = math.radians(ang0 + 120 * k)
                d = Vector((math.cos(ang), math.sin(ang), 0))
                side = Vector((-d.y, d.x, 0)) * 0.17
                up = Vector((0, 0, 1))
                q = [c, c + d * 0.22 + up * 0.3 + side, c + d * 0.6 + up * 0.5, c + d * 0.22 + up * 0.3 - side]
                uv = [(0.5, 0.0), (0.1, 0.5), (0.5, 1.0), (0.9, 0.5)]
                nrm = (d * 0.6 + up * 0.5).cross(side)
                a.mb.face(q, a.m("leaf"), uv, ref=nrm)
                a.mb.face(q, a.m("leaf"), uv, ref=-nrm)
    return a.finish(coll, weld=False)


def build_lanternpost(coll):
    a = Asset("LanternPost", ["wood", "iron", "lamp"], 75)
    iuv = _box_uv(2.0)
    plank(a, Matrix.Identity(4), (0, 0, -0.2), (0, 0, 9.0), (1, 0, 0), 0.62, 0.62, bevel=0.14, wobble=0.6)
    plank(a, Matrix.Identity(4), (-0.6, 0, 8.3), (2.7, 0, 8.3), (0, 1, 0), 0.48, 0.5, bevel=0.12, wobble=0.4)
    plank(a, Matrix.Identity(4), (0.1, 0, 6.5), (1.55, 0, 8.15), (0, 1, 0), 0.32, 0.32, wobble=0.5)
    lx = 2.2
    a.box((lx, 0, 8.25), (0.65, 0.62, 0.78), "iron", 0.06, uv=iuv)          # collier
    for k, z in enumerate((7.75, 7.45)):                                    # chaîne : 2 maillons
        rot = Matrix.Rotation(math.radians(90 * k), 4, "Z")
        a.box((lx, 0, z), (0.1, 0.32, 0.38), "iron", 0.03, rot=rot, uv=iuv)
    vl.lathe(a.mb, [(0.62, 7.05), (0.2, 7.35), (0.0, 7.42)], 4, M((lx, 0, 0), rz=45), a.m("iron"), lambda *_: (0.2, 0.2),
             cap_bottom=(a.m("iron"), lambda x, y: (0.2, 0.2)), smooth=False)
    a.box((lx, 0, 6.6), (0.7, 0.7, 0.95), "lamp", 0.0, uv=_box_uv(1.0))
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.box((lx + sx * 0.36, sy * 0.36, 6.6), (0.12, 0.12, 1.0), "iron", 0.02, uv=iuv)
    a.box((lx, 0, 6.06), (0.9, 0.9, 0.2), "iron", 0.05, uv=iuv)
    a.collider_box((0, 0, 4.5), (0.7, 0.7, 9.2))
    return a.finish(coll)


def build_sign_small(coll):
    a = Asset("SignSmall", ["wood", "iron"], 76)
    plank(a, Matrix.Identity(4), (0, 0, -0.2), (0, 0, 2.3), (1, 0, 0), 0.3, 0.3, wobble=1.5)
    m = M((0, -0.22, 1.85), ry=4)
    a.mb.add(box_bm((1.9, 0.14, 0.95), 0.05), m, a.m("wood"), vl.wood_uv((1.9, 0.14, 0.95), 1, 0.7))
    for z in (0.25, -0.25):
        a.mb.add(box_bm((0.1, 0.06, 0.1)), m @ Matrix.Translation((0, -0.09, z)), a.m("iron"), _box_uv(1.0))
    a.collider_box((0, 0, 1.2), (0.4, 0.4, 2.5))
    return a.finish(coll)


def build_crate(coll, kind):
    a = Asset(f"Crate{kind:02d}", ["wood", "planks"], 77 + kind)
    if kind == 1:
        crate_closed(a, Matrix.Identity(4))
        a.collider_box((0, 0, 1.5), (3.0, 3.0, 3.0))
    elif kind == 2:
        crate_slatted(a, Matrix.Identity(4), 3.0, 2.8, 3)
        a.collider_box((0, 0, 1.5), (3.0, 3.0, 3.05))
    else:
        crate_slatted(a, Matrix.Identity(4), 3.0, 0.9, 1)
        a.collider_box((0, 0, 0.55), (3.0, 3.0, 1.1))
    return a.finish(coll)


def build_barrel(coll):
    a = Asset("Barrel", ["planks", "iron"], 81)
    barrel(a, Matrix.Translation((0, 0, 1.75)), 3.5, 1.25, sides=14)
    a.collider([(1.2 * math.cos(2 * math.pi * i / 10), 1.2 * math.sin(2 * math.pi * i / 10), z) for i in range(10) for z in (0.0, 3.5)])
    return a.finish(coll)


def build_woodpile(coll):
    a = Asset("Woodpile", ["wood", "endgrain"], 82)
    for sx in (-1, 1):
        for sy in (-1, 1):
            plank(a, Matrix.Identity(4), (sx * 2.85, sy * 0.85, -0.2), (sx * 2.85 + a.jit(0.1), sy * 0.85, 3.2), (1, 0, 0), 0.32, 0.32, wobble=2.0)
    r = 0.37
    for row in range(4):
        n = 7 if row % 2 == 0 else 6
        for k in range(n):
            x = -2.35 + (k + (0.5 if row % 2 else 0.0)) * 0.78 + a.jit(0.05)
            mm = M((x, a.jit(0.15), r + row * 0.66), rx=90, rz=a.jit(4))
            log(a, mm, 2.2 + a.jit(0.25), r + a.jit(0.04))
    a.collider_box((0, 0, 1.5), (6.0, 2.4, 3.0))
    return a.finish(coll)


def build_stump(coll):
    a = Asset("Stump", ["wood", "endgrain"], 83)
    prof = [(1.5, -0.1), (1.3, 0.3), (1.2, 0.85), (1.17, 1.6), (1.15, 2.0)]
    bark = lambda ang, z, r, k: (z / vl.WOOD_U_METERS, vl.band_v(2, ang / (2 * math.pi)))
    vl.lathe(a.mb, prof, 10, Matrix.Identity(4), a.m("wood"), bark,
             cap_top=(a.m("endgrain"), lambda x, y: (0.5 + x / 2.4, 0.5 + y / 2.4)))
    for i in range(5):
        t = 2 * math.pi * i / 5 + a.jit(0.3)
        d = Vector((math.cos(t), math.sin(t), 0))
        path = [d * 0.9 + Vector((0, 0, 0.75)), d * 1.6 + Vector((0, 0, 0.25)), d * 2.3 + Vector((0, 0, -0.05))]
        vl.tube(a.mb, path, [0.42, 0.28, 0.08], 6, a.m("wood"), lambda ang, ln, r: (ln / 4.0, vl.band_v(2, ang / 6.3)),
                side_axis=Vector((-d.y, d.x, 0)))
    a.collider([(1.4 * math.cos(2 * math.pi * i / 10), 1.4 * math.sin(2 * math.pi * i / 10), z) for i in range(10) for z in (0.0, 2.0)])
    return a.finish(coll)


def build_loground(coll):
    a = Asset("LogRound", ["wood", "endgrain"], 84)
    bark = lambda ang, z, r, k: (z / 2.0, vl.band_v(2, ang / (2 * math.pi)))
    vl.lathe(a.mb, [(2.0, -0.5), (2.06, 0.0), (1.98, 0.42), (1.9, 0.5)], 14, Matrix.Identity(4), a.m("wood"), bark,
             cap_top=(a.m("endgrain"), lambda x, y: (0.5 + x / 3.9, 0.5 + y / 3.9)))
    # Collider en tronc de cône : on y monte en marchant (le joueur ne passe que 0,3 m de marche).
    a.collider([(r * math.cos(2 * math.pi * i / 12), r * math.sin(2 * math.pi * i / 12), z)
                for i in range(12) for r, z in ((1.9, 0.5), (3.1, -0.3))])
    return a.finish(coll)


def build_wheelbarrow(coll):
    a = Asset("Wheelbarrow", ["wood", "planks", "iron"], 85)
    bot = [(-1.0, -0.8, 1.15), (1.5, -0.8, 1.15), (1.5, 0.8, 1.15), (-1.0, 0.8, 1.15)]
    top = [(-1.45, -1.2, 2.3), (2.25, -1.2, 2.3), (2.25, 1.2, 2.3), (-1.45, 1.2, 2.3)]
    open_box(a, bot, top, 0.18, "planks")
    I = Matrix.Identity(4)
    for sy in (-1, 1):
        plank(a, I, (2.35, sy * 0.45, 0.75), (-2.6, sy * 1.0, 1.75), (0, 0, 1), 0.3, 0.32, wobble=0.5)
        plank(a, I, (-1.0, sy * 0.85, 1.4), (-1.15, sy * 0.95, 0.0), (1, 0, 0), 0.28, 0.28, wobble=1.0)
    plank(a, I, (-1.0, -0.9, 0.5), (-1.0, 0.9, 0.5), (1, 0, 0), 0.2, 0.2)
    wm = M((2.35, 0, 0.75), rx=90)
    vl.lathe(a.mb, [(0.0, -0.17), (0.66, -0.17), (0.66, 0.17), (0.0, 0.17)], 12, wm, a.m("wood"),
             lambda ang, z, r, k: (r * math.cos(ang) / 3 + 0.3, vl.band_v(1, 0.5 + r * math.sin(ang) / 1.6)), smooth=False)
    vl.lathe(a.mb, [(0.62, -0.21), (0.78, -0.21), (0.78, 0.21), (0.62, 0.21)], 12, wm, a.m("iron"), lambda ang, z, r, k: (ang * 0.3, z))
    a.box((2.35, 0, 0.75), (0.22, 0.95, 0.22), "iron", 0.04, uv=_box_uv(1.0))
    a.collider([p for p in top] + [(2.35, -0.3, 0.0), (2.35, 0.3, 0.0), (-1.1, -0.95, 0.0), (-1.1, 0.95, 0.0), (-2.6, -1.0, 1.75), (-2.6, 1.0, 1.75)])
    return a.finish(coll)


def build_basket(coll):
    a = Asset("Basket", ["hay"], 86)
    basket(a, Matrix.Identity(4), 1.0)
    a.collider_box((0, 0, 0.75), (2.1, 2.1, 1.5))
    return a.finish(coll)


def build_sack(coll):
    a = Asset("Sack", ["burlap", "hay"], 87)
    sack(a, Matrix.Identity(4), 0.8)
    a.collider_box((0, 0, 1.0), (1.9, 1.9, 2.0))
    return a.finish(coll)


def build_hayrack(coll):
    """Mangeoire à foin en lattes (Planche P13 #2) : 5 × 2,2 × 3 m."""
    a = Asset("HayRack", ["wood", "hay"], 88)
    L, W, H = 5.0, 2.2, 3.0
    xp, yp = L / 2 - 0.25, W / 2 - 0.25
    # 4 poteaux d'angle + cadres bas et haut.
    for sx in (-1, 1):
        for sy in (-1, 1):
            plank(a, Matrix.Identity(4), (sx * xp, sy * yp, -0.15), (sx * xp, sy * yp, H), (1, 0, 0), 0.42, 0.42, bevel=0.08, wobble=0.6)
    for z in (0.72, 2.72):
        for sy in (-1, 1):
            plank(a, Matrix.Identity(4), (-xp - 0.15, sy * yp, z), (xp + 0.15, sy * yp, z), (0, 1, 0), 0.34, 0.34, bevel=0.07, wobble=0.4)
        for sx in (-1, 1):
            plank(a, Matrix.Identity(4), (sx * xp, -yp, z), (sx * xp, yp, z), (1, 0, 0), 0.34, 0.34, bevel=0.07, wobble=0.4)
    # Fond en planches.
    for k in range(4):
        y = -yp + 0.28 + (2 * yp - 0.56) * k / 3
        plank(a, Matrix.Identity(4), (-xp, y, 0.68), (xp, y, 0.68), (0, 0, 1), 0.38, 0.18, bevel=0.04, wobble=0.3)
    # Barreaux verticaux (5 par long côté, 2 par bout).
    for sy in (-1, 1):
        for k in range(5):
            x = -1.6 + 3.2 * k / 4
            plank(a, Matrix.Identity(4), (x, sy * (yp - 0.04), 0.72), (x, sy * (yp + 0.04), 2.72), (1, 0, 0), 0.2, 0.22, bevel=0.04, wobble=0.7)
    for sx in (-1, 1):
        for sy in (-0.42, 0.42):
            plank(a, Matrix.Identity(4), (sx * xp, sy, 0.72), (sx * xp, sy, 2.72), (0, 1, 0), 0.2, 0.22, bevel=0.04, wobble=0.7)
    # Garniture de foin à l'intérieur.
    huv = _box_uv(3.0)
    box_at(a, Matrix.Identity(4), (0, 0, 1.32), (L - 0.9, W - 0.8, 1.15), "hay", bevel=0.28, uv=huv)
    box_at(a, M((0.4, 0.05, 1.62), rz=4, rx=3), (0, 0, 0), (3.1, 1.25, 0.9), "hay", bevel=0.25, uv=huv)
    a.collider_box((0, 0, H / 2), (L, W, H))
    return a.finish(coll)


def build_haypile(coll):
    """Tas de foin en vrac (Planche P13 #4) : ⌀ 4 × 2,2 m."""
    a = Asset("HayPile", ["hay"], 89)
    prof = [(2.05, -0.1), (2.1, 0.22), (1.82, 0.85), (1.32, 1.52), (0.62, 1.98), (0.0, 2.2)]
    vl.lathe(a.mb, prof, 14, Matrix.Identity(4), a.m("hay"), lambda ang, z, r, k: (ang * 0.8, z * 0.7))
    huv = _box_uv(3.0)
    for i in range(7):
        t = 2 * math.pi * i / 7 + a.jit(0.2)
        r = 1.45 + a.jit(0.25)
        mm = M((r * math.cos(t), r * math.sin(t), 0.42), rz=math.degrees(t) + a.jit(20), rx=a.jit(12))
        box_at(a, mm, (0, 0, 0), (1.15, 0.95, 0.9), "hay", bevel=0.26, uv=huv)
    box_at(a, M((0.1, -0.1, 1.75), rz=25, ry=10), (0, 0, 0), (1.2, 1.1, 0.8), "hay", bevel=0.25, uv=huv)
    a.collider([(2.1 * math.cos(2 * math.pi * i / 8), 2.1 * math.sin(2 * math.pi * i / 8), 0.0) for i in range(8)] +
               [(1.3 * math.cos(2 * math.pi * i / 8), 1.3 * math.sin(2 * math.pi * i / 8), 1.8) for i in range(8)])
    return a.finish(coll)


def build_choppingblock(coll):
    """Billot avec une hache plantée dedans (Planche P15 #7) : ⌀ 2,2 × 2,6 m."""
    a = Asset("ChoppingBlock", ["wood", "endgrain", "iron"], 90)
    prof = [(1.15, -0.1), (1.04, 0.35), (0.98, 1.2), (1.03, 1.82)]
    bark = lambda ang, z, r, k: (z / vl.WOOD_U_METERS, vl.band_v(2, ang / (2 * math.pi)))
    vl.lathe(a.mb, prof, 12, Matrix.Identity(4), a.m("wood"), bark,
             cap_top=(a.m("endgrain"), lambda x, y: (0.5 + x / 2.2, 0.5 + y / 2.2)))
    for i in range(4):
        t = 2 * math.pi * i / 4 + 0.4 + a.jit(0.2)
        d = Vector((math.cos(t), math.sin(t), 0))
        path = [d * 0.8 + Vector((0, 0, 0.55)), d * 1.35 + Vector((0, 0, 0.1)), d * 1.6 + Vector((0, 0, -0.05))]
        vl.tube(a.mb, path, [0.34, 0.2, 0.08], 6, a.m("wood"), lambda ang, ln, r: (ln / 4.0, vl.band_v(2, ang / 6.3)),
                side_axis=Vector((-d.y, d.x, 0)))
    # Hache plantée en biais dans le billot.
    ax_m = M((0.15, 0.0, 1.84), ry=18, rz=-12)
    blade_prof = [(-0.18, -0.25), (0.48, -0.35), (0.46, 0.38), (-0.18, 0.22)]
    a.mb.add(slab_bm(blade_prof, -0.09, 0.09, 0.04, True), ax_m, a.m("iron"), _box_uv(1.5))
    handle_path = [ax_m @ Vector((-0.05, 0, 0.2)), ax_m @ Vector((-0.6, 0, 0.32)),
                   ax_m @ Vector((-1.15, 0, 0.5)), ax_m @ Vector((-1.55, 0, 0.65))]
    vl.tube(a.mb, handle_path, [0.11, 0.095, 0.1, 0.13], 8, a.m("wood"),
            lambda ang, ln, r: (ln / 2.0, vl.band_v(0, ang / (2 * math.pi))), side_axis=ax_m.to_3x3() @ Vector((0, 1, 0)))
    a.collider([(1.15 * math.cos(2 * math.pi * i / 10), 1.15 * math.sin(2 * math.pi * i / 10), z) for i in range(10) for z in (0.0, 2.3)])
    return a.finish(coll)


def build_cart(coll):
    """Charrette à bras à deux roues et bâche (Planche P15 #12 + Place décorée) : ~4,5 × 8,0 × 5,5 m (échelle 1,4×)."""
    a = Asset("Cart", ["wood", "planks", "iron", "canvas"], 95)
    I = Matrix.Identity(4)
    sc = 1.4
    # Longerons de caisse + brancards vers -Y (inclinés vers le sol devant).
    for sx in (-1, 1):
        x = sx * 0.95
        plank(a, I, (x, -0.5, 1.02), (x, 2.75, 1.02), (0, 0, 1), 0.28, 0.28, bevel=0.06, wobble=0.3)
        plank(a, I, (x, -0.45, 1.02), (x, -2.85, 0.42), (0, 0, 1), 0.24, 0.24, bevel=0.05, wobble=0.3)
        plank(a, I, (x, -0.25, 1.0), (x, -0.35, -0.1), (1, 0, 0), 0.24, 0.24, bevel=0.05, wobble=0.4)
    # Plancher de la charrette.
    for k in range(6):
        y = -0.3 + 2.9 * (k + 0.5) / 6
        plank(a, I, (-1.22, y, 1.2), (1.22, y, 1.2), (0, 1, 0), 0.44, 0.16, bevel=0.04, wobble=0.3)
    # Poteaux (4 coins + 2 intermédiaires par côté) et ridelles à claire-voie.
    for sx in (-1, 1):
        x = sx * 1.14
        for y, top_z in ((-0.35, 3.35), (0.65, 2.3), (1.65, 2.3), (2.6, 3.35)):
            plank(a, I, (x, y, 1.12), (x, y, top_z), (1, 0, 0), 0.22, 0.22, bevel=0.05, wobble=0.3)
        for z in (1.65, 2.12):
            plank(a, I, (x, -0.4, z), (x, 2.65, z), (0, 0, 1), 0.16, 0.24, bevel=0.04, wobble=0.4)
    for z in (1.65, 2.12):
        plank(a, I, (-1.18, 2.6, z), (1.18, 2.6, z), (0, 0, 1), 0.16, 0.24, bevel=0.04, wobble=0.4)
    # Charpente haute et toit de toile bombé (toile placée au-dessus des pannes pour éviter tout chevauchement).
    for sx in (-1, 1):
        plank(a, I, (sx * 1.14, -0.5, 3.32), (sx * 1.14, 2.75, 3.32), (0, 0, 1), 0.22, 0.22, bevel=0.05, wobble=0.2)
    plank(a, I, (0, -0.55, 3.68), (0, 2.8, 3.68), (1, 0, 0), 0.22, 0.22, bevel=0.05, wobble=0.2)
    for y in (-0.35, 2.6):
        plank(a, I, (-1.14, y, 3.32), (0, y, 3.66), (0, 1, 0), 0.2, 0.2, bevel=0.04, wobble=0.2)
        plank(a, I, (1.14, y, 3.32), (0, y, 3.66), (0, 1, 0), 0.2, 0.2, bevel=0.04, wobble=0.2)

    def canopy(s, t, off):
        x = -1.32 + 2.64 * s
        y = -0.6 + 3.45 * t
        arch = 0.42 * math.sin(math.pi * s) - 0.04 * math.sin(math.pi * t)
        return Vector((x, y, 3.44 + arch + off))

    cuv = lambda s, t, p: (s * 0.9, t * 1.1)
    a.mb.grid(lambda s, t: canopy(s, t, 0.04), 8, 6, a.m("canvas"), cuv, ref=lambda p: Vector((0, 0, 1)))
    a.mb.grid(lambda s, t: canopy(s, t, -0.04), 8, 6, a.m("canvas"), cuv, ref=lambda p: Vector((0, 0, -1)))
    for s_edge, nx in ((0.0, -1), (1.0, 1)):
        def flap(u, v, se=s_edge, nx=nx):
            p = canopy(se, u, 0.0)
            return Vector((p.x + nx * 0.05 * v, p.y, p.z - 0.36 * v))
        a.mb.grid(flap, 6, 1, a.m("canvas"), lambda u, v, p: (u * 1.1, v * 0.2), smooth=False, ref=lambda p, nx=nx: Vector((nx, 0, 0)))
        a.mb.grid(flap, 6, 1, a.m("canvas"), lambda u, v, p: (u * 1.1, v * 0.2), smooth=False, ref=lambda p, nx=nx: Vector((-nx, 0, 0)))
    # Essieu et 2 grandes roues cerclées de fer.
    iuv = _box_uv(2.0)
    a.box((0, 1.15, 0.95), (3.15, 0.22, 0.22), "iron", 0.04, uv=iuv)
    for sx in (-1, 1):
        wm = M((sx * 1.42, 1.15, 0.95), ry=90)
        vl.lathe(a.mb, [(0.68, -0.11), (0.89, -0.11), (0.89, 0.11), (0.68, 0.11), (0.68, -0.11)], 14, wm, a.m("wood"),
                 lambda ang, z, r, k: (ang * 0.5, vl.band_v(1, 0.5 + z)), smooth=False)
        vl.lathe(a.mb, [(0.88, -0.13), (0.95, -0.13), (0.95, 0.13), (0.88, 0.13)], 14, wm, a.m("iron"),
                 lambda ang, z, r, k: (ang * 0.3, z), smooth=False)
        vl.lathe(a.mb, [(0.0, -0.2), (0.24, -0.2), (0.24, 0.2), (0.0, 0.2)], 10, wm, a.m("wood"),
                 lambda ang, z, r, k: (ang * 0.3, vl.band_v(0, 0.5 + z)))
        for k in range(4):
            ang = math.radians(45 * k)
            dy, dz = 0.72 * math.cos(ang), 0.72 * math.sin(ang)
            plank(a, I, (sx * 1.42, 1.15 - dy, 0.95 - dz), (sx * 1.42, 1.15 + dy, 0.95 + dz), (1, 0, 0), 0.13, 0.13, bevel=0.03, wobble=0.2)
    a.collider_box((0, 1.15, 1.85), (3.15, 3.55, 3.7))
    a.collider_box((0, -1.6, 0.55), (2.2, 2.4, 1.1))
    for v in a.mb.bm.verts:
        v.co *= sc
    a.colliders = [[p * sc for p in pts] for pts in a.colliders]
    return a.finish(coll)


def build_pot(coll):
    """Pot en terre cuite : ⌀ ~1,9 × 1,6 m."""
    a = Asset("Pot", ["clay"], 96)
    pot(a, Matrix.Identity(4), 1.15)
    a.collider_box((0, 0, 0.8), (1.9, 1.9, 1.6))
    return a.finish(coll)


ASSETS = [
    ("SM_Campfire", build_campfire), ("SM_Trough", build_trough), ("SM_HayBale", build_haybale),
    ("SM_HayRack", build_hayrack), ("SM_HayPile", build_haypile),
    ("SM_Garden_Bed", build_garden_bed), ("SM_LanternPost", build_lanternpost), ("SM_Sign_Small", build_sign_small),
    ("SM_Crate_01", lambda c: build_crate(c, 1)), ("SM_Crate_02", lambda c: build_crate(c, 2)), ("SM_Crate_03", lambda c: build_crate(c, 3)),
    ("SM_Barrel", build_barrel), ("SM_Woodpile", build_woodpile), ("SM_Stump", build_stump),
    ("SM_ChoppingBlock", build_choppingblock), ("SM_LogRound", build_loground),
    ("SM_Wheelbarrow", build_wheelbarrow), ("SM_Cart", build_cart),
    ("SM_Basket", build_basket), ("SM_Sack", build_sack), ("SM_Pot", build_pot),
]


def main(do_export=False, only=None, assets=None, coll_name=COLL):
    coll = vl.reset_collection(coll_name)
    report, x = {}, -40.0
    for fbx, fn in (assets or ASSETS):
        if only and fbx not in only:
            continue
        objs = fn(coll)
        if do_export:
            vl.export_fbx(objs, os.path.join(vl.OUT_DIR, fbx + ".fbx"))
        dims = objs[0].dimensions.copy()
        for o in objs:
            o.location.x = x + dims.x / 2
        x += dims.x + 3.0
        bpy.context.view_layer.update()
        report[fbx] = {"tris": vl.tri_count(objs[0]), "size": [round(v, 2) for v in dims], "colliders": len(objs) - 1}
    return report


if __name__ == "__main__" or "DO_EXPORT" in globals():
    result = main(globals().get("DO_EXPORT", False), globals().get("ONLY"))
