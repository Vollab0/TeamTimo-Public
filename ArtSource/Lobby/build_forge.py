# Construit la forge du Lobby (planches P06 et P07) et l'exporte en FBX pour Unity : un FBX par asset.
#   SM_Forge_Kiln     grand four : voûte de briques couverte de tôle rivetée, gueule de four en pierre, braises
#   SM_Forge_Furnace  petit fourneau : voûte de briques brunes, porte de fer, conduit coudé
#   SM_Forge_Deck     plancher de la forge (planches sombres sur lambourdes)
#   SM_Anvil          enclume sur son billot
#   SM_ToolRack       râtelier d'outils (marteaux, pinces, lame)
#   SM_Sawhorse       tréteau
#   SM_Barrel_Stack   tas de tonneaux couchés (3 + 2)
#   SM_Signpost       panneau indicateur (planche en flèche)
# Planches : GameDesign/99_Attachments/Lobby/Planches/Planche_P06_Forge_Fours.png, Planche_P07_Forge_Accessoires.png
#
# Lancer dans Blender 5.2 (MCP ou Scripting), fichier ArtSource/Lobby/forge.blend :
#   exec(open(r"<dépôt>\ArtSource\Lobby\build_forge.py", encoding="utf-8").read(), {"DO_EXPORT": True})
# Textures : python ArtSource/Lobby/gen_village_textures.py (briques, tôle, fer, braises).
#
# Repère : 1 unité = 1 m du jeu, pivot au sol au centre, façade vers -Y (= +Z dans Unity), Z haut.
# Objets exportés : <Prefix>_Body (matériaux listés dans ASSETS) + <Prefix>_Col<n> (colliders convexes).
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

importlib.reload(vl)
from village_lib import MB, box_bm, slab_bm, special_uv, u_profile, world_uv  # noqa: E402

BLEND_PATH = os.path.join(vl.PROJECT, "ArtSource", "Lobby", "forge.blend")
COLL = "Village_Forge"
BRICK = 11.0         # une répétition de la texture de briques = 11 m (briques de 2,75 × 1,4 m, comme la planche P06)
METAL_V = 7.0        # tôle : une répétition le long de l'arc = 7 m

# Grand four (P06 A) : 18 × 23 × 20
KILN_D = 23.0
KILN_RB = 8.6        # demi-largeur de la maçonnerie (voûte de rayon KILN_RB)
KILN_SHELL = 0.45    # épaisseur de la tôle
KILN_TOP = 20.0
KILN_ZS = KILN_TOP - KILN_RB - KILN_SHELL      # naissance de la voûte
MOUTH_HALF, MOUTH_ZS, MOUTH_DEPTH = 3.9, 3.7, 3.6   # gueule : 7,8 m de large, 7,6 m de haut
# Petit fourneau (P06 B) : 16 × 18 × 17 (conduit compris)
FURN_D, FURN_RB, FURN_ZS = 18.0, 7.6, 4.4
FURN_BAND = 0.45     # saillie des bandeaux de briques avant / arrière


def _box_uv(tile):
    def f(lc, wc, n):
        ax = max(range(3), key=lambda i: abs(n[i]))
        if ax == 0:
            return (wc.y / tile, wc.z / tile)
        if ax == 1:
            return (wc.x / tile, wc.z / tile)
        return (wc.x / tile, wc.y / tile)
    return f


def iron_uv(tile=3.0):
    return _box_uv(tile)


class Asset(vl.Builder):
    """Un asset = un mesh « Body » (plusieurs matériaux) + des colliders convexes."""

    def __init__(self, prefix, mats, seed):
        super().__init__(seed)
        self.prefix, self.mats = prefix, mats
        self.mb = MB()

    def m(self, key):
        return self.mats.index(key)

    def box(self, center, size, key, bevel=0.0, rot=None, uv=None):
        mm = Matrix.Translation(Vector(center))
        if rot is not None:
            mm = mm @ rot
        self.mb.add(box_bm(size, bevel), mm, self.m(key), uv or iron_uv())

    def wood(self, p0, p1, yaxis, dy, dz, key="wood", bevel=0.12, wobble=0.6, band=None, ext=0.0):
        vl.Builder.beam(self, self.mb, p0, p1, yaxis, dy, dz, self.m(key), band, bevel, 1, wobble, ext)

    def slab(self, profile, y0, y1, key, bevel=0.1, uv=None, cap_bottom=False, matrix=None):
        self.mb.add(slab_bm(profile, y0, y1, bevel, cap_bottom), matrix or Matrix.Identity(4), self.m(key), uv or _box_uv(BRICK))

    def finish(self, coll, weld=True):
        body = self.mb.to_object(f"{self.prefix}_Body", coll, weld=weld)
        vl.assign(body, self.mats)
        return [body] + self.collider_objects(self.prefix, coll)


# ------------------------------------------------------------------ outils de forme
def arch_ring(a, cz, ri, ro, n, y0, y1, key, gap=0.08, bevel=0.12, uv=None, a0=0.0, a1=math.pi, jitter=0.0):
    """Claveaux d'un arc (centre (0, cz), rayons ri -> ro, de l'angle a0 à a1), chacun un prisme biseauté."""
    for i in range(n):
        t0 = a0 + (a1 - a0) * i / n + gap / ro / 2
        t1 = a0 + (a1 - a0) * (i + 1) / n - gap / ro / 2
        r2 = ro + a.jit(jitter)
        prof = [(ri * math.cos(t0), cz + ri * math.sin(t0)), (r2 * math.cos(t0), cz + r2 * math.sin(t0)),
                (r2 * math.cos(t1), cz + r2 * math.sin(t1)), (ri * math.cos(t1), cz + ri * math.sin(t1))]
        a.slab(prof, y0 + a.jit(jitter * 0.5), y1, key, bevel, uv, cap_bottom=True)


def horseshoe(a, outer, inner, y, ns, key, uvfn, normal_y):
    """Face plane à y entre deux profils en U (outer(s), inner(s)) : façade percée d'une ouverture."""
    def fn(s, t):
        o, i = outer(s), inner(s)
        return Vector((o[0] + (i[0] - o[0]) * t, y, o[1] + (i[1] - o[1]) * t))
    a.mb.grid(fn, ns, 1, a.m(key), lambda s, t, p: uvfn(p), smooth=False, ref=lambda p: Vector((0, normal_y, 0)))


def extrude_u(a, prof, y0, y1, ns, key, uvfn, inward=False, cz=0.0, smooth=True):
    """Surface d'un profil en U extrudé le long de Y ; normales vers l'extérieur (ou vers l'axe si inward)."""
    def fn(s, t):
        x, z = prof(s)
        return Vector((x, y0 + (y1 - y0) * t, z))

    def ref(p):
        d = p - Vector((0, p.y, min(p.z, cz)))
        return -d if inward else d
    a.mb.grid(fn, ns, 1, a.m(key), uvfn, smooth=smooth, ref=ref)


def flat_profile_face(a, prof, n, y, key, uvfn, normal_y):
    pts = [Vector((prof(i / n)[0], y, prof(i / n)[1])) for i in range(n + 1)]
    pts = pts[:-1] if (pts[0] - pts[-1]).length < 1e-6 else pts
    a.mb.face(pts, a.m(key), [uvfn(p) for p in pts], ref=(0, normal_y, 0))


def barrel(a, matrix, length, radius, sides=12, hoops=(0.17, 0.83)):
    """Tonneau le long de l'axe Z local (z de -length/2 à +length/2), douelles + cercles de fer + fonds."""
    def r_at(t):
        return radius * (0.84 + 0.16 * math.sin(math.pi * t))
    ts = [0.0, 0.12, 0.3, 0.5, 0.7, 0.88, 1.0]
    prof = [(r_at(t), -length / 2 + length * t) for t in ts]
    plank = lambda ang, z, r, k: (ang / (2 * math.pi) * 1.67, (z + length / 2) / 8.0)   # 10 douelles
    r_end = r_at(0.0)
    # Fonds : jable (anneau en retrait) + fond plat.
    inset = 0.16
    lid = (a.m("planks"), lambda x, y: (x / 4.0 + 0.3, y / 4.0))
    full = [(r_end * 0.86, -length / 2 + inset)] + prof + [(r_end * 0.86, length / 2 - inset)]
    vl.lathe(a.mb, full, sides, matrix, a.m("planks"), plank, cap_top=lid, cap_bottom=lid)
    for h in hoops:
        z, w, r = -length / 2 + length * h, length * 0.07, r_at(h) + 0.07
        vl.lathe(a.mb, [(r - 0.08, z - w), (r, z - w), (r, z + w), (r - 0.08, z + w)], sides, matrix,
                 a.m("iron"), lambda ang, zz, rr, k: (ang * 0.4, zz * 0.3))


# ------------------------------------------------------------------ grand four
def build_kiln(coll):
    a = Asset("Kiln", ["brick", "metal", "stone", "embers", "iron"], 61)
    D, Rb, zs = KILN_D, KILN_RB, KILN_ZS
    yf, yb = -D / 2, D / 2
    outer, olen = u_profile(Rb, zs, 3, 24)
    inner, ilen = u_profile(MOUTH_HALF, MOUTH_ZS, 3, 14)
    buv = _box_uv(BRICK)
    # Maçonnerie : murs + voûte (rangées de briques le long de Y, empilées le long de l'arc).
    extrude_u(a, outer, yf, yb, 30, "brick", lambda s, t, p: (p.y / BRICK, s * olen / BRICK), cz=zs)
    horseshoe(a, outer, inner, yf, 30, "brick", lambda p: (p.x / BRICK, p.z / BRICK), -1)
    flat_profile_face(a, outer, 30, yb, "brick", lambda p: (p.x / BRICK, p.z / BRICK), 1)
    # Gueule : tunnel en briques, fond de braises, tas de braises au sol.
    yi = yf + MOUTH_DEPTH
    extrude_u(a, inner, yf, yi, 18, "brick", lambda s, t, p: (p.y / BRICK, s * ilen / BRICK), inward=True, cz=MOUTH_ZS)
    flat_profile_face(a, inner, 18, yi, "embers", lambda p: (p.x / 9.0 + 0.5, p.z / 9.0), -1)

    def pile(s, t):
        x, y = -MOUTH_HALF + 0.15 + (2 * MOUTH_HALF - 0.3) * s, yf + 0.1 + (MOUTH_DEPTH - 0.2) * t
        h = 0.25 + 1.1 * math.sin(math.pi * s) ** 0.8 * math.sin(math.pi * t) ** 0.6 * (0.8 + 0.2 * math.sin(7 * s + 3 * t))
        return Vector((x, y, h))
    a.mb.grid(pile, 8, 5, a.m("embers"), lambda s, t, p: (p.x / 4.0, p.y / 4.0), ref=lambda p: Vector((0, 0, 1)))
    # Encadrement de pierre : seuil, piédroits, claveaux.
    suv = _box_uv(16.0)      # pierre très agrandie : chaque bloc montre une seule pierre lisse
    sy0, sy1 = yf - 0.5, yf + 0.35
    ro = MOUTH_HALF + 1.6
    for k, (x0, x1) in enumerate(((-ro - 0.3, -1.6), (-1.5, 1.5), (1.6, ro + 0.3))):
        a.slab([(x0, 0.0), (x1, 0.0), (x1, 0.75 + a.jit(0.06)), (x0, 0.75)], yf - 1.3, sy1, "stone", 0.14, suv)
    for sgn in (-1, 1):
        x0, x1 = sgn * MOUTH_HALF, sgn * ro
        lo, hi = sorted((x0, x1))
        a.slab([(lo, 0.8), (hi, 0.8), (hi, 2.35), (lo, 2.35)], sy0 + a.jit(0.08), sy1, "stone", 0.14, suv, cap_bottom=True)
        a.slab([(lo, 2.42), (hi, 2.42), (hi, MOUTH_ZS - 0.04), (lo, MOUTH_ZS - 0.04)], sy0 + a.jit(0.08), sy1, "stone", 0.14, suv, cap_bottom=True)
    arch_ring(a, MOUTH_ZS, MOUTH_HALF, ro, 7, sy0, sy1, "stone", gap=0.1, bevel=0.14, uv=suv, jitter=0.08)
    # Pierres d'angle au pied des murs (vue de côté de la planche).
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = sx * (Rb - 0.5), sy * (D / 2 - 1.1)
            a.slab([(cx - 1.0, 0.0), (cx + 1.0, 0.0), (cx + 1.0, 2.4), (cx - 1.0, 2.4)], cy - 1.4, cy + 1.4, "stone", 0.18, suv)
    # Tôle rivetée sur la voûte : débord de 0,6 m devant et derrière, descend un peu sous la naissance.
    ov = 0.6
    Ly = D + 2 * ov
    ri, rm = Rb + 0.04, Rb + KILN_SHELL
    th0, th1 = math.radians(-7), math.radians(187)
    panel = Ly / 3

    def shell(r, sgn):
        def fn(s, t):
            th = th1 + (th0 - th1) * s
            return Vector((r * math.cos(th), -Ly / 2 + Ly * t, zs + r * math.sin(th)))
        a.mb.grid(fn, 26, 3, a.m("metal"), lambda s, t, p: ((p.y + Ly / 2) / panel, s * (th1 - th0) * rm / METAL_V),
                  ref=lambda p: (p - Vector((0, p.y, zs))) * sgn)
    shell(rm, 1)
    shell(ri, -1)
    for yy, sgn in ((-Ly / 2, -1), (Ly / 2, 1)):
        def rim(s, t, yy=yy):
            th = th1 + (th0 - th1) * s
            r = ri + (rm - ri) * t
            return Vector((r * math.cos(th), yy, zs + r * math.sin(th)))
        a.mb.grid(rim, 26, 1, a.m("metal"), lambda s, t, p: (0.02 + 0.03 * t, s * 3.0), smooth=False,
                  ref=lambda p, sgn=sgn: Vector((0, sgn, 0)))
        # Rivets du bord (petites pyramides de fer).
        for i in range(15):
            th = th1 + (th0 - th1) * (i + 0.5) / 15
            r = (ri + rm) / 2
            c = Vector((r * math.cos(th), yy + sgn * 0.02, zs + r * math.sin(th)))
            mm = Matrix.Translation(c) @ Matrix.Rotation(math.radians(-90 * sgn), 4, "X")
            vl.lathe(a.mb, [(0.17, 0.0), (0.12, 0.08), (0.0, 0.12)], 6, mm, a.m("iron"), lambda *_: (0.3, 0.3), smooth=True)
    for th, sgn in ((th0, -1), (th1, 1)):
        tang = Vector((-math.sin(th), 0, math.cos(th))) * sgn

        def edge(s, t, th=th):
            r = ri + (rm - ri) * t
            return Vector((r * math.cos(th), -Ly / 2 + Ly * s, zs + r * math.sin(th)))
        a.mb.grid(edge, 3, 1, a.m("metal"), lambda s, t, p: (p.y / panel, 0.01 * t), smooth=False, ref=lambda p, d=tang: d)
    # Collider : enveloppe convexe du four (tôle comprise).
    pts = []
    for yy in (yf - ov, yb + ov):
        for i in range(13):
            th = math.pi * i / 12
            pts.append((rm * math.cos(th), yy, zs + rm * math.sin(th)))
        pts += [(-rm, yy, 0.0), (rm, yy, 0.0)]
    a.collider(pts)
    return a.finish(coll)


# ------------------------------------------------------------------ petit fourneau
def build_furnace(coll):
    a = Asset("Furnace", ["brick_dark", "iron"], 62)
    D, Rb, zs = FURN_D, FURN_RB, FURN_ZS
    body, blen = u_profile(Rb, zs, 3, 22)
    band, bandlen = u_profile(Rb + FURN_BAND, zs, 3, 22)
    bw = 1.3   # largeur des bandeaux
    yf, yb = -D / 2, D / 2
    uvb = lambda s, t, p: (p.y / BRICK, s * blen / BRICK)
    uvband = lambda s, t, p: (p.y / BRICK + 0.25, s * bandlen / BRICK)
    extrude_u(a, body, yf + bw, yb - bw, 26, "brick_dark", uvb, cz=zs)
    for y0, y1, ystep, nstep in ((yf, yf + bw, yf + bw, 1), (yb - bw, yb, yb - bw, -1)):
        extrude_u(a, band, y0, y1, 26, "brick_dark", uvband, cz=zs)
        horseshoe(a, band, body, ystep, 26, "brick_dark", lambda p: (p.x / BRICK, p.z / BRICK), nstep)
    flat_profile_face(a, band, 26, yf, "brick_dark", lambda p: (p.x / BRICK, p.z / BRICK), -1)
    flat_profile_face(a, band, 26, yb, "brick_dark", lambda p: (p.x / BRICK, p.z / BRICK), 1)
    # Porte de foyer en fer : battant cintré, cadre, 2 pentures, loquet.
    dh, dz0, dzs = 3.0, 0.5, 3.6
    door, _ = u_profile(dh, dzs, 2, 12, z0=dz0)
    pts = [door(i / 16) for i in range(17)]
    iuv = iron_uv(3.0)
    a.slab(pts, yf - 0.35, yf + 0.05, "iron", 0.08, iuv, cap_bottom=True)
    # Cadre : piédroits, traverse basse et arc.
    for sgn in (-1, 1):
        lo, hi = sorted((sgn * dh, sgn * (dh + 0.55)))
        a.slab([(lo, dz0 - 0.4), (hi, dz0 - 0.4), (hi, dzs), (lo, dzs)], yf - 0.55, yf + 0.05, "iron", 0.1, iuv, cap_bottom=True)
    a.slab([(-dh - 0.55, dz0 - 0.4), (dh + 0.55, dz0 - 0.4), (dh + 0.55, dz0), (-dh - 0.55, dz0)], yf - 0.55, yf + 0.05, "iron", 0.1, iuv, cap_bottom=True)
    arch_ring(a, dzs, dh, dh + 0.55, 8, yf - 0.55, yf + 0.05, "iron", gap=0.0, bevel=0.06, uv=iuv)
    # Rivets du cadre.
    for i in range(9):
        th = math.pi * (i + 0.5) / 9
        c = Vector(((dh + 0.27) * math.cos(th), yf - 0.57, dzs + (dh + 0.27) * math.sin(th)))
        vl.lathe(a.mb, [(0.14, 0.0), (0.0, 0.1)], 6, Matrix.Translation(c) @ Matrix.Rotation(math.radians(90), 4, "X"),
                 a.m("iron"), lambda *_: (0.3, 0.3))
    for z in (1.7, 4.9):
        a.slab([(-dh - 0.6, z - 0.28), (-0.4, z - 0.22), (-0.2, z), (-0.4, z + 0.22), (-dh - 0.6, z + 0.28)],
               yf - 0.5, yf - 0.32, "iron", 0.05, iuv, cap_bottom=True)
        a.box((-dh - 0.45, yf - 0.55, z), (0.5, 0.5, 1.0), "iron", 0.1, uv=iuv)                 # gond
    a.box((dh - 0.9, yf - 0.48, 3.4), (0.45, 0.3, 1.5), "iron", 0.08, uv=iuv)                   # gâche
    a.box((dh - 1.4, yf - 0.62, 3.4), (1.4, 0.2, 0.28), "iron", 0.06, uv=iuv)                   # loquet
    # Conduit coudé en tôle sombre : monte puis part vers l'arrière, sommet à 17 m.
    py, r, bend = -1.0, 1.0, 1.6
    zt = 17.0 - r - bend
    path = [Vector((0, py, zs + Rb - 0.6)), Vector((0, py, zs + Rb + 0.6)), Vector((0, py, zt))]
    for i in range(1, 7):
        ph = math.radians(90 * i / 6)
        path.append(Vector((0, py + bend - bend * math.cos(ph), zt + bend * math.sin(ph))))
    path.append(path[-1] + Vector((0, 1.0, 0)))
    tuv = lambda ang, ln, rr: (ang * rr / 3.0, ln / 3.0)
    vl.tube(a.mb, path, r, 12, a.m("iron"), tuv)
    end = path[-1]
    vl.tube(a.mb, [end + Vector((0, -0.4, 0)), end + Vector((0, 0.05, 0))], 1.18, 12, a.m("iron"), tuv)
    ring_o = [end + Vector((1.18 * math.cos(2 * math.pi * i / 12), 0.05, 1.18 * math.sin(2 * math.pi * i / 12))) for i in range(12)]
    ring_i = [end + Vector((0.92 * math.cos(2 * math.pi * i / 12), 0.05, 0.92 * math.sin(2 * math.pi * i / 12))) for i in range(12)]
    for i in range(12):
        j = (i + 1) % 12
        a.mb.face([ring_o[i], ring_o[j], ring_i[j], ring_i[i]], a.m("iron"), [(0, 0), (0.1, 0), (0.1, 0.1), (0, 0.1)], ref=(0, 1, 0))
    a.mb.face([end + Vector((0.92 * math.cos(2 * math.pi * i / 12), -0.2, 0.92 * math.sin(2 * math.pi * i / 12))) for i in range(12)],
              a.m("iron"), [(0.95, 0.95)] * 12, ref=(0, 1, 0))                                     # fond noirci du conduit
    # Collerette à la base du conduit.
    vl.lathe(a.mb, [(1.55, zs + Rb - 0.5), (1.55, zs + Rb + 0.25), (1.2, zs + Rb + 0.55)], 12,
             Matrix.Translation((0, py, 0)), a.m("iron"), lambda ang, z, rr, k: (ang * 0.5, z * 0.3))
    pts = []
    R2 = Rb + FURN_BAND
    for yy in (yf - 0.6, yb):
        for i in range(13):
            th = math.pi * i / 12
            pts.append((R2 * math.cos(th), yy, zs + R2 * math.sin(th)))
        pts += [(-R2, yy, 0.0), (R2, yy, 0.0)]
    a.collider(pts)
    return a.finish(coll)


# ------------------------------------------------------------------ plancher de forge
def build_deck(coll):
    a = Asset("ForgeDeck", ["wood_dark"], 63)
    W, Dp, top = 16.0, 14.0, 0.8
    for x in (-7.2, -2.4, 2.4, 7.2):
        a.wood((x, -Dp / 2 + 0.1, 0.24), (x, Dp / 2 - 0.1, 0.24), (1, 0, 0), 0.8, 0.5, "wood_dark", wobble=0.3)
    n = 9
    w = Dp / n
    for i in range(n):
        y = -Dp / 2 + w * (i + 0.5)
        L = W + a.jit(0.5)
        x0 = -W / 2 + a.jit(0.25)
        z = top - 0.17 + a.jit(0.03)
        a.wood((x0, y, z), (x0 + L, y, z), (0, 1, 0), w - 0.1, 0.34, "wood_dark", bevel=0.08, wobble=0.5)
    # Bords du collider en pente douce (27°) : le CharacterController du joueur ne monte que 0,3 m de marche.
    ramp = 1.6
    a.collider([(sx * W / 2, sy * Dp / 2, top) for sx in (-1, 1) for sy in (-1, 1)] +
               [(sx * (W / 2 + ramp), sy * (Dp / 2 + ramp), 0.0) for sx in (-1, 1) for sy in (-1, 1)])
    return a.finish(coll)


# ------------------------------------------------------------------ enclume sur billot
def build_anvil(coll):
    a = Asset("Anvil", ["wood", "iron"], 64)
    prof = [(1.6, 0.0), (1.35, 0.25), (1.22, 0.7), (1.18, 1.25), (1.2, 1.7)]
    bark = lambda ang, z, r, k: (z / vl.WOOD_U_METERS, vl.band_v(0, ang / (2 * math.pi)))
    top = (a.m("wood"), lambda x, y: (0.38 + x * 0.03, vl.band_v(1, 0.5 + y * 0.2)))
    vl.lathe(a.mb, prof, 12, Matrix.Rotation(math.radians(8), 4, "Z"), a.m("wood"), bark, cap_top=top)
    iuv = iron_uv(2.0)
    z0 = 1.7
    a.slab([(-1.0, z0), (1.0, z0), (0.85, z0 + 0.35), (-0.85, z0 + 0.35)], -0.7, 0.7, "iron", 0.06, iuv, cap_bottom=True)
    a.slab([(-0.85, z0 + 0.35), (0.85, z0 + 0.35), (0.5, z0 + 0.8), (-0.5, z0 + 0.8)], -0.45, 0.45, "iron", 0.05, iuv, cap_bottom=True)
    a.slab([(-0.6, z0 + 0.8), (0.6, z0 + 0.8), (1.15, z0 + 1.3), (-1.15, z0 + 1.3)], -0.55, 0.55, "iron", 0.06, iuv, cap_bottom=True)
    a.box((-1.35, 0, z0 + 1.12), (0.45, 0.85, 0.36), "iron", 0.06, uv=iuv)                      # talon
    # Bigorne : cône le long de +X, légèrement courbé vers le bas.
    path = [Vector((1.0 + 1.5 * t, 0, z0 + 1.08 - 0.25 * t * t)) for t in (0.0, 0.33, 0.66, 1.0)]
    vl.tube(a.mb, path, [0.42, 0.32, 0.18, 0.04], 8, a.m("iron"), lambda ang, ln, r: (ang * 0.2, ln * 0.3), side_axis=(0, 1, 0))
    a.collider_box((0, 0, 0.85), (2.6, 2.6, 1.7))
    a.collider_box((0.3, 0, z0 + 0.65), (4.6, 1.4, 1.3))
    return a.finish(coll)


# ------------------------------------------------------------------ râtelier d'outils
def build_toolrack(coll):
    a = Asset("ToolRack", ["wood", "iron"], 65)
    for sx in (-1, 1):
        x = sx * 2.7
        a.wood((x, 0, -0.05), (x, 0, 5.7), (1, 0, 0), 0.55, 0.55, wobble=0.5)
        a.wood((x, -1.0, 0.25), (x, 1.0, 0.25), (1, 0, 0), 0.55, 0.5, ext=0.1)
        for sy in (-1, 1):
            a.wood((x, sy * 0.85, 0.45), (x, sy * 0.1, 1.9), (1, 0, 0), 0.32, 0.32, bevel=0.08)
    a.wood((-3.4, 0, 5.35), (3.4, 0, 5.35), (0, 1, 0), 0.6, 0.55, bevel=0.16)
    a.wood((-2.7, 0, 4.3), (2.7, 0, 4.3), (0, 1, 0), 0.4, 0.38)
    iuv = iron_uv(1.5)
    yt = -0.42
    # Outils accrochés devant la barre basse : (x, type)
    for x, kind in ((-1.95, "hammer"), (-1.0, "sledge"), (0.0, "tongs"), (1.0, "blade"), (1.9, "chisel")):
        a.box((x, yt + 0.1, 4.3), (0.16, 0.35, 0.16), "iron", 0.03, uv=iuv)                       # crochet
        if kind in ("hammer", "sledge"):
            ln = 1.7 if kind == "hammer" else 2.1
            a.wood((x, yt, 4.2), (x, yt, 4.2 - ln), (0, 1, 0), 0.16, 0.16, bevel=0.05, wobble=2)
            hs = (0.75, 0.32, 0.34) if kind == "hammer" else (0.95, 0.46, 0.46)
            a.box((x, yt, 4.2 - ln - hs[2] / 2 + 0.1), hs, "iron", 0.06, uv=iuv)
        elif kind == "tongs":
            for s in (-1, 1):
                rot = Matrix.Rotation(math.radians(5 * s), 4, "Y")
                a.box((x + 0.12 * s, yt, 3.2), (0.1, 0.1, 2.1), "iron", 0.02, rot=rot, uv=iuv)
                a.box((x - 0.05 * s, yt, 2.2), (0.08, 0.12, 0.4), "iron", 0.02, rot=Matrix.Rotation(math.radians(-25 * s), 4, "Y"), uv=iuv)
            a.box((x, yt, 3.6), (0.18, 0.16, 0.18), "iron", 0.04, uv=iuv)
        elif kind == "blade":
            a.wood((x, yt, 4.2), (x, yt, 3.5), (0, 1, 0), 0.2, 0.17, bevel=0.05)
            a.slab([(x - 0.24, 3.5), (x + 0.24, 3.5), (x + 0.24, 2.0), (x, 1.6), (x - 0.24, 1.85)],
                   yt - 0.03, yt + 0.03, "iron", 0.0, iuv, cap_bottom=True)
        else:
            a.wood((x, yt, 4.2), (x, yt, 3.5), (0, 1, 0), 0.18, 0.18, bevel=0.05)
            a.slab([(x - 0.12, 3.5), (x + 0.12, 3.5), (x + 0.12, 2.4), (x, 2.2), (x - 0.12, 2.4)],
                   yt - 0.07, yt + 0.07, "iron", 0.02, iuv, cap_bottom=True)
    a.collider_box((0, 0, 2.9), (6.4, 2.2, 5.8))
    return a.finish(coll)


# ------------------------------------------------------------------ tréteau
def build_sawhorse(coll):
    a = Asset("Sawhorse", ["wood"], 66)
    a.wood((-2.5, 0, 2.7), (2.5, 0, 2.7), (0, 1, 0), 0.75, 0.6, bevel=0.15, wobble=0.4)
    for sx in (-1, 1):
        for sy in (-1, 1):
            a.wood((sx * 1.8, sy * 0.2, 2.45), (sx * 2.1, sy * 0.95, -0.05), (1, 0, 0), 0.38, 0.38, bevel=0.1, wobble=1.0)
        a.wood((sx * 1.93, -0.62, 1.05), (sx * 1.93, 0.62, 1.05), (1, 0, 0), 0.3, 0.3, bevel=0.08, ext=0.3)
    a.collider_box((0, 0, 1.5), (5.0, 2.0, 3.0))
    return a.finish(coll)


# ------------------------------------------------------------------ tas de tonneaux couchés
def build_barrel_stack(coll):
    a = Asset("BarrelStack", ["planks", "iron"], 67)
    R, L = 1.22, 3.4
    lay = Matrix.Rotation(math.radians(90), 4, "X")
    spots = [(-2.5, 0.0, R), (0.0, 0.15, R), (2.5, -0.1, R), (-1.25, 0.05, R + 2.1), (1.25, 0.2, R + 2.1)]
    for i, (x, y, z) in enumerate(spots):
        mm = Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(a.jit(4)), 4, "Z") @ lay @ \
            Matrix.Rotation(math.radians(a.rng.uniform(0, 360)), 4, "Z")
        barrel(a, mm, L, R)
    a.collider_box((0, 0, R), (7.5, L, 2 * R))
    a.collider([(-2.6, -L / 2, 2 * R), (2.6, -L / 2, 2 * R), (-2.6, L / 2, 2 * R), (2.6, L / 2, 2 * R),
                (-1.2, -L / 2, 4.5), (1.2, -L / 2, 4.5), (-1.2, L / 2, 4.5), (1.2, L / 2, 4.5)])
    return a.finish(coll)


# ------------------------------------------------------------------ panneau indicateur
def build_signpost(coll):
    a = Asset("Signpost", ["wood"], 68)
    a.wood((0, 0, -0.2), (0, 0, 6.0), (1, 0, 0), 0.5, 0.5, bevel=0.12, wobble=0.8)
    a.box((0, 0, 6.05), (0.62, 0.62, 0.18), "wood", 0.06, uv=vl.wood_uv((0.62, 0.62, 0.18), 1, 0.3))
    prof = [(-0.6, 0.0), (2.0, -0.05), (2.75, 0.45), (2.0, 0.95), (-0.6, 0.9)]
    m = Matrix.Translation((-0.3, 0, 4.55)) @ Matrix.Rotation(math.radians(-3), 4, "Y")
    a.mb.add(slab_bm(prof, -0.4, -0.15, 0.06), m, a.m("wood"),
             lambda lc, wc, n: ((lc.x + 1.0) / vl.WOOD_U_METERS, vl.band_v(1, lc.z / 0.95)))
    a.collider_box((0, 0, 3.0), (0.6, 0.6, 6.2))
    return a.finish(coll)


ASSETS = [
    ("SM_Forge_Kiln", build_kiln), ("SM_Forge_Furnace", build_furnace), ("SM_Forge_Deck", build_deck),
    ("SM_Anvil", build_anvil), ("SM_ToolRack", build_toolrack), ("SM_Sawhorse", build_sawhorse),
    ("SM_Barrel_Stack", build_barrel_stack), ("SM_Signpost", build_signpost),
]


def main(do_export=False, only=None):
    coll = vl.reset_collection(COLL)
    report, x = {}, -30.0
    for fbx, fn in ASSETS:
        if only and fbx not in only:
            continue
        objs = fn(coll)
        if do_export:
            vl.export_fbx(objs, os.path.join(vl.OUT_DIR, fbx + ".fbx"))
        dims = objs[0].dimensions
        for o in objs:
            o.location.x = x + dims.x / 2
        x += dims.x + 6.0
        bpy.context.view_layer.update()
        report[fbx] = {"tris": vl.tri_count(objs[0]), "size": [round(v, 2) for v in dims], "colliders": len(objs) - 1}
    return report


if __name__ == "__main__" or "DO_EXPORT" in globals():
    result = main(globals().get("DO_EXPORT", False), globals().get("ONLY"))
