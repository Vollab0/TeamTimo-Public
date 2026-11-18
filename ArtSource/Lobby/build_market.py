# Construit les clôtures (planche P11) et les étals de la place (P09, P10) du Lobby : un FBX par asset.
#   SM_Fence_Wood    travée de 8 m (2 lisses, 1 poteau rond au début : les travées s'enchaînent sans doublon)
#   SM_Fence_Post    poteau seul (fin de clôture)
#   SM_Fence_Broken  travée cassée (lisse fendue, lisse tombée)
#   SM_Fence_Gate    portillon de 5 m
#   SM_Stall_Simple, SM_Stall_Double, SM_Stall_LeanTo, SM_Stall_Striped (auvent rayé + marchandises)
# Lancer dans Blender 5.2, fichier ArtSource/Lobby/props.blend :
#   exec(open(r"<dépôt>\ArtSource\Lobby\build_market.py", encoding="utf-8").read(), {"DO_EXPORT": True})
# Repère : 1 unité = 1 m du jeu, pivot au sol au centre, devant vers -Y (= +Z dans Unity), Z haut.
# Clôture : la travée court le long de Y ; son poteau est à y = +4 (= début de travée, z = -4 dans Unity,
# LobbyVillageBuilder.PlaceFence oriente +Z Unity dans le sens de la polyligne).
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
import build_props as bp  # noqa: E402

importlib.reload(vl)
importlib.reload(bp)
from village_lib import box_bm  # noqa: E402
from build_props import Asset, M, _box_uv, plank  # noqa: E402

COLL = "Village_Market"
I = Matrix.Identity(4)
BAY = 8.0
RAIL_Z = (1.15, 2.3)
POST_R = 0.34


# ------------------------------------------------------------------ clôtures
def round_post(a, y, h=3.15, r=POST_R):
    vl.lathe(a.mb, [(r * 1.05, -0.3), (r, 1.0), (r * 0.97, h)], 8, M((0, y, 0), rz=a.jit(20)), a.m("wood"),
             lambda ang, z, rr, k: (z / vl.WOOD_U_METERS, vl.band_v(1, ang / (2 * math.pi))),
             cap_top=(a.m("endgrain"), lambda x, yy: (0.5 + x / (2.2 * r), 0.5 + yy / (2.2 * r))))


def rail(a, y0, y1, z, z1=None, x=-0.4, wob=1.2):
    plank(a, I, (x, y0, z), (x + a.jit(0.05), y1, z if z1 is None else z1), (1, 0, 0), 0.24, 0.62, bevel=0.07, wobble=wob)


def build_fence(coll):
    a = Asset("FenceBay", ["wood", "endgrain"], 91)
    round_post(a, BAY / 2)
    for z in RAIL_Z:
        rail(a, -BAY / 2 - 0.15, BAY / 2 + 0.3, z + a.jit(0.06), z + a.jit(0.1))
    a.collider_box((-0.2, 0, 1.6), (0.9, BAY, 3.2))
    return a.finish(coll)


def build_fence_post(coll):
    a = Asset("FencePost", ["wood", "endgrain"], 92)
    round_post(a, 0.0, 3.4, 0.38)
    a.collider_box((0, 0, 1.7), (0.8, 0.8, 3.4))
    return a.finish(coll)


def build_fence_broken(coll):
    a = Asset("FenceBroken", ["wood", "endgrain"], 93)
    round_post(a, BAY / 2)
    z = RAIL_Z[1]
    rail(a, 0.25, BAY / 2 + 0.3, z, z + 0.05)                      # lisse haute fendue en deux
    rail(a, -BAY / 2 - 0.15, -0.2, z - 0.02, z - 0.45, wob=3)
    # lisse basse tombée : un bout encore cloué au poteau, l'autre au sol
    plank(a, I, (-0.45, BAY / 2 + 0.2, RAIL_Z[0]), (-0.9, -BAY / 2 + 1.0, 0.18), (1, 0, 0.3), 0.24, 0.62, bevel=0.07, wobble=2)
    a.collider_box((-0.2, 0, 1.6), (0.9, BAY, 3.2))
    return a.finish(coll)


def build_fence_gate(coll):
    a = Asset("FenceGate", ["wood", "endgrain", "iron"], 94)
    W = 5.0
    for y in (-W / 2, W / 2):
        round_post(a, y, 3.3)
    x = -0.4
    for z in (0.5, 2.5):
        plank(a, I, (x, -W / 2 + 0.35, z), (x, W / 2 - 0.35, z), (1, 0, 0), 0.22, 0.55, wobble=0.6)
    for y in (-W / 2 + 0.6, W / 2 - 0.6):
        plank(a, I, (x - 0.05, y, 0.25), (x - 0.05, y, 2.8), (1, 0, 0), 0.22, 0.5, wobble=0.6)
    plank(a, I, (x - 0.12, -W / 2 + 0.7, 0.55), (x - 0.12, W / 2 - 0.7, 2.45), (1, 0, 0), 0.2, 0.45, wobble=0.5)
    plank(a, I, (x - 0.12, W / 2 - 0.7, 0.55), (x - 0.12, -W / 2 + 0.7, 2.45), (1, 0, 0), 0.2, 0.45, wobble=0.5)
    for z in (0.5, 2.5):                                            # pentures
        a.box((x - 0.2, -W / 2 + 0.5, z), (0.08, 0.7, 0.18), "iron", 0.02, uv=_box_uv(1.0))
    a.collider_box((-0.2, 0, 1.6), (0.9, W + 0.6, 3.2))
    return a.finish(coll)


# ------------------------------------------------------------------ étals
class Stall:
    """Étal en bois : poteaux, chevrons en pente, toit de planches, comptoir(s) et étagère du fond."""

    def __init__(self, a, W, D, hf, hb, bays=1, counter=True, shelf=False, roof_along_slope=False, mid_post_up=0.0):
        self.a, self.W, self.D, self.hf, self.hb = a, W, D, hf, hb
        self.bays, self.counter, self.shelf, self.along = bays, counter, shelf, roof_along_slope
        self.mid_up = mid_post_up
        self.xs = [-W / 2 + 0.5 + (W - 1.0) * k / bays for k in range(bays + 1)]
        self.yf, self.yb = -D / 2 + 0.5, D / 2 - 0.5

    def z_roof(self, y):
        """Dessous des chevrons à l'ordonnée y (pente de l'avant vers l'arrière)."""
        return self.hf + (self.hb - self.hf) * (y - self.yf) / (self.yb - self.yf)

    def build(self):
        a, W = self.a, self.W
        for x in self.xs:
            for y in (self.yf, self.yb):
                up = self.mid_up if (x not in (self.xs[0], self.xs[-1]) and y == self.yf) else 0.0
                plank(a, I, (x, y, -0.2), (x + a.jit(0.08), y, self.z_roof(y) - 0.3 + up), (1, 0, 0), 0.75, 0.75, bevel=0.14, wobble=0.8)
                a.collider_box((x, y, 2.5), (0.8, 0.8, 5.0))
        # Sablières (le long de X) sur les poteaux, chevrons (le long de la pente) à chaque file de poteaux.
        for y in (self.yf, self.yb):
            plank(a, I, (-W / 2 - 0.3, y, self.z_roof(y) - 0.1), (W / 2 + 0.3, y, self.z_roof(y) - 0.1), (0, 1, 0), 0.7, 0.65, bevel=0.14, wobble=0.5)
        ov = 1.0
        for x in self.xs:
            p0 = Vector((x, self.yf - ov, self.z_roof(self.yf - ov) + 0.45))
            p1 = Vector((x, self.yb + ov, self.z_roof(self.yb + ov) + 0.45))
            plank(a, I, p0, p1, (1, 0, 0), 0.55, 0.5, bevel=0.1, wobble=0.4)
        # Couverture en planches.
        y0, y1 = self.yf - ov - 0.2, self.yb + ov + 0.2
        if self.along:
            n = max(3, round((W + 1.2) / 1.15))
            for k in range(n):
                x = -W / 2 - 0.6 + (W + 1.2) * (k + 0.5) / n
                p0 = Vector((x, y0, self.z_roof(y0) + 0.85 + a.jit(0.04)))
                p1 = Vector((x, y1, self.z_roof(y1) + 0.85 + a.jit(0.04)))
                plank(a, I, p0, p1, (1, 0, 0), (W + 1.2) / n - 0.06, 0.28, bevel=0.07, wobble=0.6)
        else:
            L = math.hypot(y1 - y0, self.z_roof(y1) - self.z_roof(y0))
            n = max(3, round(L / 1.05))
            for k in range(n):
                y = y0 + (y1 - y0) * (k + 0.5) / n
                z = self.z_roof(y) + 0.85 + 0.12 * (k % 2) + a.jit(0.04)
                plank(a, I, (-W / 2 - 0.7 + a.jit(0.15), y, z), (W / 2 + 0.7 + a.jit(0.15), y, z), (0, 1, 0), L / n + 0.12, 0.3, bevel=0.08, wobble=0.5)
        if self.counter:
            self.build_counters()
        if self.shelf:
            zs = 4.6
            for k in range(self.bays):
                xa, xb = self.xs[k] + 0.4, self.xs[k + 1] - 0.4
                plank(a, I, (xa, self.yb - 0.75, zs), (xb, self.yb - 0.75, zs), (0, 1, 0), 0.9, 0.25, wobble=0.6)

    def build_counters(self):
        """Comptoir en façade (et sur les côtés) : lisse haute + bardage de planches verticales."""
        a = self.a
        zt = 3.35
        uv = _box_uv(8.0)
        for k in range(self.bays):
            xa, xb = self.xs[k] + 0.38, self.xs[k + 1] - 0.38
            y = self.yf + 0.05
            plank(a, I, (xa - 0.3, y - 0.15, zt), (xb + 0.3, y - 0.15, zt), (0, 1, 0), 1.05, 0.32, bevel=0.1, wobble=0.5)
            a.box(((xa + xb) / 2, y, (zt - 0.15) / 2), (xb - xa, 0.28, zt - 0.15), "planks", 0.04, uv=uv)
            plank(a, I, (self.xs[k], y - 0.12, 1.6), (self.xs[k + 1], y - 0.12, 1.6), (0, 1, 0), 0.3, 0.45, wobble=0.6)
            a.collider_box(((xa + xb) / 2, y - 0.1, zt / 2), (xb - xa, 1.0, zt))
        for x in (self.xs[0], self.xs[-1]):
            ya, yb = self.yf + 0.38, self.yb - 0.38
            s = 1 if x > 0 else -1
            plank(a, I, (x + s * 0.05, ya - 0.3, zt), (x + s * 0.05, yb + 0.3, zt), (1, 0, 0), 1.0, 0.32, bevel=0.1, wobble=0.5)
            a.box((x, (ya + yb) / 2, (zt - 0.15) / 2), (0.28, yb - ya, zt - 0.15), "planks", 0.04, uv=uv)
            a.collider_box((x, (ya + yb) / 2, zt / 2), (1.0, yb - ya, zt))


def build_stall_simple(coll):
    a = Asset("StallSimple", ["wood", "planks", "endgrain"], 101)
    Stall(a, 11.0, 9.0, 10.0, 8.7).build()
    return a.finish(coll)


def build_stall_double(coll):
    a = Asset("StallDouble", ["wood", "planks", "endgrain"], 102)
    s = Stall(a, 22.0, 8.0, 10.0, 8.8, bays=2, mid_post_up=1.6)
    s.build()
    # Jambes de force du poteau central, sous la sablière avant.
    for sgn in (-1, 1):
        plank(a, I, (0.3 * sgn, s.yf, 7.2), (2.0 * sgn, s.yf, 9.3), (0, 1, 0), 0.4, 0.4, wobble=0.5)
    return a.finish(coll)


def build_stall_leanto(coll):
    a = Asset("StallLeanTo", ["wood", "planks", "endgrain"], 103)
    s = Stall(a, 12.0, 10.0, 9.0, 5.8, counter=False, shelf=False, roof_along_slope=True)
    s.build()
    for y in (s.yf,):
        for x, sgn in ((s.xs[0], 1), (s.xs[-1], -1)):
            plank(a, I, (x + 0.3 * sgn, y, s.z_roof(y) - 2.4), (x + 2.2 * sgn, y, s.z_roof(y) - 0.35), (0, 1, 0), 0.4, 0.4, wobble=0.5)
    return a.finish(coll)


def build_stall_striped(coll):
    """Étal du marchand (P10) : 4 poteaux, comptoir-table en façade, 2 étagères, auvent de toile rayée à festons."""
    a = Asset("StallStriped", ["wood", "planks", "stripes", "clay", "burlap", "hay", "endgrain"], 104)
    W, D = 13.0, 6.0
    xs, yf, yb = (-W / 2 + 0.5, W / 2 - 0.5), -D / 2 + 0.5, D / 2 - 0.5
    hf, hb = 7.6, 9.0
    for x in xs:
        plank(a, I, (x, yf, -0.2), (x, yf, hf - 0.15), (1, 0, 0), 0.7, 0.7, bevel=0.14, wobble=0.7)
        plank(a, I, (x, yb, -0.2), (x, yb, hb - 0.15), (1, 0, 0), 0.7, 0.7, bevel=0.14, wobble=0.7)
        plank(a, I, (x, yf - 0.3, hf - 0.45), (x, yb + 0.3, hb - 0.45), (1, 0, 0), 0.5, 0.45, wobble=0.2)    # chevron
        for y in (yf, yb):
            a.collider_box((x, y, 2.5), (0.8, 0.8, 5.0))
    plank(a, I, (-W / 2 - 0.3, yf, hf - 0.32), (W / 2 + 0.3, yf, hf - 0.32), (0, 1, 0), 0.6, 0.5, wobble=0.25)
    plank(a, I, (-W / 2 - 0.3, yb, hb - 0.32), (W / 2 + 0.3, yb, hb - 0.32), (0, 1, 0), 0.6, 0.5, wobble=0.25)
    # Comptoir-table en façade (plateau + traverse basse + 2 montants avant) et 2 étagères au fond sur consoles.
    for y, z, depth in ((yf - 0.2, 3.1, 1.6), (yb - 0.5, 3.6, 1.1), (yb - 0.5, 5.6, 1.0)):
        n = 2
        for k in range(n):
            yy = y - depth / 2 + depth * (k + 0.5) / n
            plank(a, I, (-W / 2 + 0.15, yy, z), (W / 2 - 0.15, yy, z), (0, 1, 0), depth / n - 0.06, 0.3, wobble=0.5)
    for x in xs:
        plank(a, I, (x, yf - 0.75, -0.1), (x, yf - 0.75, 3.05), (1, 0, 0), 0.38, 0.38, bevel=0.07, wobble=0.5)
        plank(a, I, (x, yf, 2.95), (x, yf - 0.85, 2.95), (1, 0, 0), 0.32, 0.32, bevel=0.06, wobble=0.3)
        for z in (3.6, 5.6):
            plank(a, I, (x, yb, z - 0.18), (x, yb - 0.95, z - 0.18), (1, 0, 0), 0.32, 0.32, bevel=0.06, wobble=0.3)
    plank(a, I, (-W / 2 + 0.6, yf, 1.3), (W / 2 - 0.6, yf, 1.3), (0, 1, 0), 0.35, 0.45, wobble=0.6)
    plank(a, I, (-W / 2 + 0.6, yb, 1.3), (W / 2 - 0.6, yb, 1.3), (0, 1, 0), 0.35, 0.45, wobble=0.6)
    a.collider_box((0, yf - 0.2, 1.6), (W - 1.0, 1.6, 3.2))
    # Auvent : toile tendue de la sablière arrière à l'avant (affaissée), épaisse de 0,08 m, visible des deux côtés.
    nst = 6
    xw = W / 2 + 0.4
    period = 2 * (2 * xw) / nst                      # 1 répétition de la texture = 1 rayure rouge + 1 blanche
    ya, yz = yb + 0.5, yf - 0.9
    za, zz = hb + 0.38, hf + 0.22

    def canvas(s, t, off):
        y = ya + (yz - ya) * t
        z = za + (zz - za) * t - 0.22 * math.sin(math.pi * t) + off
        return Vector((-xw + 2 * xw * s, y, z))
    uvc = lambda s, t, p: ((p.x + xw) / period, t * 0.8)
    a.mb.grid(lambda s, t: canvas(s, t, 0.04), nst * 2, 6, a.m("stripes"), uvc, ref=lambda p: Vector((0, -0.3, 1)))
    a.mb.grid(lambda s, t: canvas(s, t, -0.04), nst * 2, 6, a.m("stripes"), uvc, ref=lambda p: Vector((0, 0.3, -1)))
    for xs_ in (-xw, xw):            # chants latéraux
        a.mb.grid(lambda s, t, xs_=xs_: canvas(xs_ / (2 * xw) + 0.5, s, -0.04 + 0.08 * t), 6, 1, a.m("stripes"),
                  lambda s, t, p: (0.05, s), smooth=False, ref=lambda p, xs_=xs_: Vector((xs_, 0, 0)))
    # Lambrequin à festons sous le bord avant (une feston par rayure), deux faces.
    top_front = canvas(0, 1, 0)
    zt, hv, sw = top_front.z, 1.1, 2 * xw / nst
    for k in range(nst):
        x0 = -xw + k * sw
        pts = [Vector((x0, yz, zt)), Vector((x0 + sw, yz, zt))]
        for i in range(9):
            th = math.pi * i / 8
            pts.append(Vector((x0 + sw / 2 + sw / 2 * math.cos(th), yz, zt - hv + 0.45 - 0.55 * math.sin(th) * 1.0)))
        pts = pts[:2] + pts[2:]
        uvs = [((p.x + xw) / period, 0.2 + (zt - p.z) * 0.3) for p in pts]
        for off, ny in ((-0.04, -1), (0.04, 1)):
            a.mb.face([p + Vector((0, off, 0)) for p in pts], a.m("stripes"), uvs, ref=(0, ny, 0))
    # Marchandises (peu nombreuses, grosses, lisibles).
    zc = 3.25
    bp.crate_closed(a, M((-4.3, yf - 0.2, zc), rz=8), 1.3)
    bp.pot(a, M((-2.4, yf - 0.4, zc)), 0.9)
    bp.pot(a, M((3.0, yf - 0.1, zc)), 0.7)
    bp.sack(a, M((1.2, yf - 0.2, zc), rz=40), 0.55)
    bp.sack(a, M((4.6, yf - 0.3, zc), rz=-20), 0.6)
    zs = 3.75
    bp.sack(a, M((-4.0, yb - 0.6, zs)), 0.5)
    bp.pot(a, M((-1.5, yb - 0.6, zs)), 0.8)
    bp.crate_closed(a, M((2.6, yb - 0.6, zs), rz=-5), 1.2)
    bp.pot(a, M((0.8, yb - 0.6, 5.75)), 0.6)
    bp.basket(a, M((-3.2, yb - 0.6, 5.75)), 0.55)
    return a.finish(coll)


ASSETS = [
    ("SM_Fence_Wood", build_fence), ("SM_Fence_Post", build_fence_post), ("SM_Fence_Broken", build_fence_broken),
    ("SM_Fence_Gate", build_fence_gate), ("SM_Stall_Simple", build_stall_simple), ("SM_Stall_Double", build_stall_double),
    ("SM_Stall_LeanTo", build_stall_leanto), ("SM_Stall_Striped", build_stall_striped),
]


if __name__ == "__main__" or "DO_EXPORT" in globals():
    result = bp.main(globals().get("DO_EXPORT", False), globals().get("ONLY"), ASSETS, COLL)
