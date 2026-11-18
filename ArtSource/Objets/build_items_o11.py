# Planche O11 (partie validée) — coffre du labyrinthe (SM_Chest_Maze, couvercle Chest_Maze_Lid parenté),
# pièce d'or (SM_Item_Coin) et bourse (SM_Item_CoinPouch).
# Le coffre du Lobby et le socle des fragments sont EXCLUS : à revoir par Gabin (2026-11-18).
# Planche : GameDesign/99_Attachments/Objets/Planches/Planche_O11_Coffres_Socle_Pieces.png
#
# Lancer dans Blender 5.2, fichier ArtSource/Objets/items_o11.blend :
#   exec(open(r"<dépôt>\ArtSource\Objets\build_items_o11.py", encoding="utf-8").read(), {"DO_EXPORT": True})
# Coffre : origine au sol au centre, façade (serrure) vers -Y. Couvercle : origine sur l'axe de la charnière
# (arête haute arrière du coffre) ; il s'ouvre par rotation autour de X local (négative dans Blender).
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
from items_lib import TAU, band_edge, band_mat, cell  # noqa: E402

COLL = "Items_O11"

# ------------------------------------------------------------------ coffre du labyrinthe (3 × 2 × 2,2 m)
CH_W = 3.0          # largeur (X)
CH_WALL_Y = 0.95    # nu extérieur des planches avant / arrière
CH_WALL_X = 1.45    # nu extérieur des planches des côtés
CH_PLANK = 0.14     # épaisseur des planches
CH_HB = 1.35        # hauteur de la caisse (charnière)
CH_RY, CH_RZ = 0.95, 0.85   # demi-profondeur et flèche du couvercle bombé
HINGE = Vector((0, CH_WALL_Y + 0.02, CH_HB))


def rot_x(deg):
    return Matrix.Rotation(math.radians(deg), 4, "X")


def plank(mb, center, size, u_off=0.0):
    il.band_box(mb, center, size, "wood_chest", bevel=0.035, u_scale=3.0, u_off=u_off)


def iron(mb, center, size, bevel=0.022):
    il.band_box(mb, center, size, "iron_dark", bevel=bevel, u_scale=1.5)


def inner_face(mb, pts, ref):
    """Face d'intérieur sombre (vue quand le coffre est ouvert)."""
    mb.face(pts, 0, [cell("black", 0.2, 0.2), cell("black", 0.8, 0.2), cell("black", 0.8, 0.8), cell("black", 0.2, 0.8)], ref=ref)


def ell(th, sy=1.0, sz=1.0, dr=0.0):
    """Point du profil bombé du couvercle (θ = 0 à l'arrière, π à l'avant), décalé de dr vers l'extérieur."""
    return Vector((0, (CH_RY + dr) * sy * math.cos(th), CH_HB + (CH_RZ + dr) * sz * math.sin(th)))


def arc_sector(mb, x0, x1, th0, th1, dr_out, dr_in, n, uv_out, uv_in, uv_rim):
    """Secteur de voûte (entre les profils décalés de dr_in et dr_out), de x0 à x1, fermé de tous côtés.
    uv_out(i/n, j) / uv_in(i/n, j) : surfaces extérieure / intérieure ; uv_rim : chants et bouts."""
    ths = [th0 + (th1 - th0) * i / n for i in range(n + 1)]
    O = [ell(t, dr=dr_out) for t in ths]
    I = [ell(t, dr=dr_in) for t in ths]
    X = lambda p, x: Vector((x, p.y, p.z))  # noqa: E731
    axis = Vector((0, 0, CH_HB))
    for i in range(n):
        q = [X(O[i], x0), X(O[i], x1), X(O[i + 1], x1), X(O[i + 1], x0)]
        mid = (O[i] + O[i + 1]) / 2
        mb.face(q, 0, [uv_out(i / n, 0), uv_out(i / n, 1), uv_out((i + 1) / n, 1), uv_out((i + 1) / n, 0)],
                smooth=True, ref=mid - axis)
        q = [X(I[i], x0), X(I[i], x1), X(I[i + 1], x1), X(I[i + 1], x0)]
        mb.face(q, 0, [uv_in(i / n, 0), uv_in(i / n, 1), uv_in((i + 1) / n, 1), uv_in((i + 1) / n, 0)],
                smooth=True, ref=axis - mid)
        for x, sx in ((x0, -1), (x1, 1)):
            mb.face([X(O[i], x), X(O[i + 1], x), X(I[i + 1], x), X(I[i], x)], 0,
                    [uv_rim(0), uv_rim(0.3), uv_rim(0.6), uv_rim(0.9)], ref=(sx, 0, 0))
    for k, sgn in ((0, -1), (n, 1)):
        tg = (ell(ths[k] + 0.01) - ell(ths[k] - 0.01)).normalized()
        mb.face([X(O[k], x0), X(O[k], x1), X(I[k], x1), X(I[k], x0)], 0,
                [uv_rim(0), uv_rim(0.5), uv_rim(0.6), uv_rim(0.1)], ref=tg * sgn)


def build_chest_body(mb):
    zp = [(0.15, 0.53), (0.54, 0.93), (0.94, 1.33)]          # 3 planches horizontales par face
    yc = CH_WALL_Y - CH_PLANK / 2
    xc = CH_WALL_X - CH_PLANK / 2
    for k, (z0, z1) in enumerate(zp):
        for sy in (-1, 1):
            plank(mb, (0, sy * yc, (z0 + z1) / 2), (2 * CH_WALL_X, CH_PLANK, z1 - z0), u_off=0.37 * k + 0.2 * sy)
        for sx in (-1, 1):
            plank(mb, (sx * xc, 0, (z0 + z1) / 2), (CH_PLANK, 2 * (CH_WALL_Y - CH_PLANK), z1 - z0), u_off=0.29 * k + 0.5 * sx)
    plank(mb, (0, 0, 0.08), (2 * CH_WALL_X - 0.04, 2 * CH_WALL_Y - 0.04, 0.16))
    # Intérieur sombre.
    ix, iy, z0, z1 = CH_WALL_X - CH_PLANK - 0.01, CH_WALL_Y - CH_PLANK - 0.01, 0.17, CH_HB - 0.02
    inner_face(mb, [(-ix, -iy, z0), (ix, -iy, z0), (ix, iy, z0), (-ix, iy, z0)], (0, 0, 1))
    for sy in (-1, 1):
        inner_face(mb, [(-ix, sy * iy, z0), (ix, sy * iy, z0), (ix, sy * iy, z1), (-ix, sy * iy, z1)], (0, -sy, 0))
    for sx in (-1, 1):
        inner_face(mb, [(sx * ix, -iy, z0), (sx * ix, iy, z0), (sx * ix, iy, z1), (sx * ix, -iy, z1)], (-sx, 0, 0))
    # Ferrures : bandeaux bas et haut, cornières verticales, coins renforcés.
    W, Y = CH_WALL_X + 0.025, CH_WALL_Y + 0.025
    for zc, hz in ((0.12, 0.24), (CH_HB - 0.1, 0.2)):
        for sy in (-1, 1):
            iron(mb, (0, sy * Y, zc), (2 * W + 0.05, 0.05, hz))
        for sx in (-1, 1):
            iron(mb, (sx * W, 0, zc), (0.05, 2 * Y + 0.05, hz))
    for sx in (-1, 1):
        for sy in (-1, 1):
            iron(mb, (sx * (CH_WALL_X - 0.12), sy * (CH_WALL_Y + 0.04), CH_HB / 2), (0.30, 0.08, CH_HB))
            iron(mb, (sx * (CH_WALL_X + 0.04), sy * (CH_WALL_Y - 0.12), CH_HB / 2), (0.08, 0.30, CH_HB))
            # Coins renforcés du bas : deux plaques par coin, à l'extérieur des planches.
            iron(mb, (sx * (CH_WALL_X - 0.13), sy * (CH_WALL_Y + 0.06), 0.18), (0.36, 0.10, 0.36), bevel=0.035)
            iron(mb, (sx * (CH_WALL_X + 0.06), sy * (CH_WALL_Y - 0.13), 0.18), (0.10, 0.36, 0.36), bevel=0.035)
    # Serrure ronde (trou de serrure peint) et anse au-dessus.
    il.lathe(mb, [(0.29, 0.0), (0.32, 0.02), (0.32, 0.07), (0.29, 0.095)], 14,
             Matrix.Translation((0, -(CH_WALL_Y + 0.03), 1.10)) @ rot_x(90),
             lambda a, z, r, k: band_edge("iron_dark", a / TAU) if k >= 1 else band_mat("iron_dark", a / TAU, 0.5),
             cap_top=lambda x, y: cell("lock_face", 0.5 + x / 0.66, 0.5 + y / 0.66, margin=0.0))
    path = [Vector((0.15 * math.cos(math.pi * i / 8), -(CH_WALL_Y + 0.08), 1.30 + 0.17 * math.sin(math.pi * i / 8))) for i in range(9)]
    il.tube(mb, path, 0.045, 6, lambda a, ln, r: band_mat("iron_dark", ln / 1.5, a / TAU), side_axis=(0, 1, 0))


def build_chest_lid(mb):
    n = 4
    gaps = 0.012
    # Doublure sombre continue derrière les planches : les joints restent sombres et on ne voit pas le jour au travers.
    dark = lambda *a: cell("black", 0.5, 0.5)  # noqa: E731
    arc_sector(mb, -CH_WALL_X + 0.03, CH_WALL_X - 0.03, 0.02, math.pi - 0.02, -0.10, -0.13, 8, dark, dark, dark)
    for k in range(n):
        t0 = math.pi * k / n + (gaps if k else 0)
        t1 = math.pi * (k + 1) / n - (gaps if k < n - 1 else 0)
        uo = 0.31 * k
        arc_sector(mb, -CH_WALL_X + 0.02, CH_WALL_X - 0.02, t0, t1, 0.0, -CH_PLANK, 3,
                   lambda s, j, uo=uo: band_mat("wood_chest", uo + j * 0.95, 0.1 + 0.8 * s),
                   lambda s, j: cell("black", 0.2 + 0.6 * s, 0.2 + 0.6 * j),
                   lambda f, uo=uo: band_edge("wood_chest", uo + f * 0.1))
    # Joues (demi-ellipses en planches) aux deux bouts.
    m = 12
    for sx in (-1, 1):
        xo, xi = sx * CH_WALL_X, sx * (CH_WALL_X - CH_PLANK)
        arc = [ell(math.pi * i / m) for i in range(m + 1)]
        uvo = [band_mat("wood_chest", 0.6 + p.y / 3.0, (p.z - CH_HB) / CH_RZ) for p in arc]
        mb.face([Vector((xo, p.y, p.z)) for p in arc], 0, uvo, ref=(sx, 0, 0))
        mb.face([Vector((xi, p.y, p.z)) for p in arc], 0, [cell("black", 0.5, 0.5)] * len(arc), ref=(-sx, 0, 0))
        for i in range(m):
            a, b = arc[i], arc[i + 1]
            mb.face([Vector((xo, a.y, a.z)), Vector((xo, b.y, b.z)), Vector((xi, b.y, b.z)), Vector((xi, a.y, a.z))], 0,
                    [band_edge("wood_chest", i * 0.05), band_edge("wood_chest", (i + 1) * 0.05),
                     band_edge("wood_chest", (i + 1) * 0.05, 1), band_edge("wood_chest", i * 0.05, 1)],
                    ref=(a + b) / 2 - Vector((0, 0, CH_HB)))
        mb.face([Vector((xo, arc[0].y, CH_HB)), Vector((xo, arc[-1].y, CH_HB)), Vector((xi, arc[-1].y, CH_HB)), Vector((xi, arc[0].y, CH_HB))],
                0, [cell("black", 0.5, 0.5)] * 4, ref=(0, 0, -1))
        # Arceau de fer sur la joue.
        xa, xb = sorted((sx * (CH_WALL_X - 0.10), sx * (CH_WALL_X + 0.05)))
        arc_sector(mb, xa, xb, 0.0, math.pi, 0.05, -0.05, 12,
                   lambda s, j: band_mat("iron_dark", s * 2.5, 0.2 + 0.6 * j), lambda s, j: band_mat("iron_dark", s * 2.5, 0.5),
                   lambda f: band_edge("iron_dark", f))
    # Bandeaux du bas du couvercle (avant et arrière) et moraillon devant.
    for sy in (-1, 1):
        iron(mb, (0, sy * (CH_WALL_Y + 0.025), CH_HB + 0.08), (2 * CH_WALL_X - 0.12, 0.05, 0.16))
    arc_sector(mb, -0.13, 0.13, math.pi * 0.80, math.pi, 0.04, -0.02, 3,
               lambda s, j: band_mat("iron_dark", s, 0.5), lambda s, j: band_mat("iron_dark", s, 0.5),
               lambda f: band_edge("iron_dark", f))


def build_chest(coll):
    mb = il.MB()
    build_chest_body(mb)
    body = il.finish(mb, "Chest_Maze", coll, origin=(0, 0, 0))
    mb = il.MB()
    build_chest_lid(mb)
    lid = il.finish(mb, "Chest_Maze_Lid", coll, origin=HINGE)
    lid.parent = body
    lid.location = HINGE
    return [body, lid]


# ------------------------------------------------------------------ pièce d'or (⌀ 0,5) et bourse (0,8)
def coin(mb, matrix, r=0.25, th=0.1, sides=14):
    h = th / 2
    face = lambda x, y: cell("coin_paw", 0.5 + 0.48 * x / r, 0.5 + 0.48 * y / r, margin=0.0)  # noqa: E731
    il.lathe(mb, [(r * 0.84, -h), (r, -h * 0.64), (r, h * 0.64), (r * 0.84, h)], sides, matrix,
             lambda a, z, rr, k: cell("gold", a / TAU, 0.5 + z / th), cap_top=face, cap_bottom=face)


def build_coin(coll):
    mb = il.MB()
    coin(mb, rot_x(90))           # face frappée vers -Y
    return [il.finish(mb, "Coin", coll)]


POUCH_PROFILE = [(0.0, 0.0), (0.18, 0.02), (0.31, 0.10), (0.37, 0.22), (0.36, 0.34), (0.29, 0.45), (0.17, 0.54),
                 (0.13, 0.60), (0.15, 0.65), (0.22, 0.72), (0.27, 0.78), (0.24, 0.81), (0.17, 0.78), (0.11, 0.72), (0.0, 0.70)]
POUCH_NECK = 0.59


def build_pouch(coll):
    mb = il.MB()

    def rmod(a, z, k):
        if 1 <= k <= 6:
            return 1 + 0.06 * math.sin(5 * a + 1.3) + 0.03 * math.sin(3 * a + z * 6)
        if 9 <= k <= 12:
            return 1 + 0.14 * math.sin(7 * a + 0.4)
        return 1.0

    def uv(a, z, r, k, fk):
        if fk >= 11:
            return cell("black", 0.5, 0.5)
        return band_mat("leather", a / TAU * 1.6, min(1.0, z / 0.8))
    il.lathe(mb, POUCH_PROFILE, 12, Matrix.Identity(4), uv, rmod=rmod)
    # Cordon noué au col, deux bouts qui pendent devant.
    rope = lambda a, ln, r: cell("rope", (ln / 0.5) % 1.0, a / TAU)  # noqa: E731
    ring = [Vector((0.155 * math.cos(TAU * i / 12), 0.155 * math.sin(TAU * i / 12), POUCH_NECK)) for i in range(13)]
    il.tube(mb, ring, 0.034, 5, rope, side_axis=(0, 0, 1))
    il.box(mb, (0, -0.18, POUCH_NECK - 0.01), (0.11, 0.08, 0.09), il.cell_uvf("rope"), bevel=0.03)
    end = lambda a: cell("rope", 0.5 + 0.3 * math.cos(a), 0.5 + 0.3 * math.sin(a))  # noqa: E731
    for path in ([(0.03, -0.19, 0.57), (0.06, -0.235, 0.48), (0.08, -0.25, 0.37)],
                 [(-0.03, -0.19, 0.57), (-0.06, -0.225, 0.47), (-0.05, -0.24, 0.40)]):
        il.tube(mb, il.smooth_path(path, 2), 0.028, 5, rope, side_axis=(1, 0, 0), cap_end=end)
    # Deux pièces qui dépassent de l'ouverture.
    coin(mb, Matrix.Translation((0.06, 0.02, 0.80)) @ Matrix.Rotation(math.radians(20), 4, "Z") @ rot_x(75), r=0.12, th=0.05, sides=10)
    coin(mb, Matrix.Translation((-0.07, -0.03, 0.78)) @ Matrix.Rotation(math.radians(-35), 4, "Z") @ rot_x(60), r=0.12, th=0.05, sides=10)
    return [il.finish(mb, "CoinPouch", coll, origin=(0, 0, POUCH_NECK))]


ASSETS = [("SM_Chest_Maze", build_chest), ("SM_Item_Coin", build_coin), ("SM_Item_CoinPouch", build_pouch)]


def main(do_export=False, only=None):
    coll = il.reset_collection(COLL)
    rep, x = {}, 0.0
    for fbx, fn in ASSETS:
        if only and fbx not in only:
            continue
        obs = fn(coll)
        if do_export:
            il.export(obs, fbx)
        r = il.report(obs[0])
        if len(obs) > 1:
            r["children"] = {o.name: il.report(o) for o in obs[1:]}
        rep[fbx] = r
        obs[0].location.x = x
        x += obs[0].dimensions.x / 2 + 2.0
    return rep


if __name__ == "__main__" or "DO_EXPORT" in globals():
    result = main(globals().get("DO_EXPORT", False), globals().get("ONLY"))
