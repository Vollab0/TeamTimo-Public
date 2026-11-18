# Construit la grange du Lobby (G1, point d'apparition des joueurs) et l'exporte en FBX pour Unity.
# Planche : GameDesign/99_Attachments/Lobby/Planches/Planche_P05_Grange.png ; recadrage Refs/Ref_Lobby_01_Grange.jpg.
#
# Lancer dans Blender 5.2 (MCP ou Scripting) :
#   exec(open(r"<dépôt>\ArtSource\Lobby\build_barn.py", encoding="utf-8").read(), {"DO_EXPORT": True})
# Textures : python ArtSource/Lobby/gen_village_textures.py ; outils communs : village_lib.py.
#
# Grange visitable : murs de 0,8 m (bardage rouge dehors, planches brutes dedans), plancher, grande porte
# de 16 × 15 m aux deux vantaux ouverts, toit à la Mansart en ardoises sur voligeage visible de l'intérieur.
# Repère : 1 unité = 1 m, pivot au sol au centre, grande porte vers -Y (+Z dans Unity).
# Objets exportés (SM_Barn.fbx) :
#   Barn_Body  0 = bardage rouge (M_Village_RedPlanks), 1 = planches (M_Village_Planks),
#              2 = bois peint en blanc (M_Village_WoodWhite), 3 = bois (M_Village_Wood)
#   Barn_Roof  0 = ardoises (M_Roof_Slate)
#   Barn_Col*  colliders convexes (calque Wall dans Unity)
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

BLEND_PATH = os.path.join(vl.PROJECT, "ArtSource", "Lobby", "barn_tent.blend")
COLL = "Village_Barn"

W, D = 37.0, 40.0          # façade (X) × profondeur (Y)
H = 17.0                   # hauteur des murs
KNEE = (12.0, 28.5)        # cassure du toit à la Mansart (x, z) sur la ligne de pente
APEX = 34.4                # faîtage de la ligne de pente (≈ 36 m au sommet de la faîtière)
WT = 0.8                   # épaisseur des murs
OV, GOV = 1.8, 1.2         # débords du toit (gouttière, pignon)
TILE_BASE = 0.4
DOOR_W, DOOR_H = 16.0, 15.0
RED, PLANKS, WHITE, WOOD = 0, 1, 2, 3


class Barn(vl.Builder):
    def __init__(self):
        super().__init__(21)
        self.body, self.roof = vl.MB(), vl.MB()
        self.eave = Vector((W / 2, 0, H + 0.3))
        self.knee = Vector((KNEE[0], 0, KNEE[1]))
        self.apex = Vector((0, 0, APEX))

    # Matériau par face : dehors = bardage rouge, dedans = planches brutes.
    @staticmethod
    def wall_mat(out):
        out = Vector(out)

        def f(n):
            return PLANKS if n.dot(out) < -0.5 else RED
        return f

    def wall_box(self, x0, x1, y0, y1, z0, z1, out):
        c = ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)
        size = (x1 - x0, y1 - y0, z1 - z0)
        self.block(self.body, c, size, self.wall_mat(out), vl.world_uv("planks"))
        self.collider_box(c, size)

    def white(self, p0, p1, yaxis, dy, dz, wobble=0.3):
        self.beam(self.body, p0, p1, yaxis, dy, dz, mat=WHITE, bevel=0.12, wobble=wobble)

    # ---------------------------------------------------------- murs
    def build_walls(self):
        hx, hy = W / 2, D / 2
        dw = DOOR_W / 2
        # Façade : deux trumeaux + linteau au-dessus de la grande porte.
        self.wall_box(-hx, -dw, -hy, -hy + WT, 0, H, (0, -1, 0))
        self.wall_box(dw, hx, -hy, -hy + WT, 0, H, (0, -1, 0))
        self.wall_box(-dw, dw, -hy, -hy + WT, DOOR_H, H, (0, -1, 0))
        # Dos et côtés.
        self.wall_box(-hx, hx, hy - WT, hy, 0, H, (0, 1, 0))
        self.wall_box(-hx, -hx + WT, -hy + WT, hy - WT, 0, H, (-1, 0, 0))
        self.wall_box(hx - WT, hx, -hy + WT, hy - WT, 0, H, (1, 0, 0))
        # Pignons (pentagone sous le voligeage).
        k = self.knee
        prof = [(-hx, H), (hx, H), (k.x - 0.4, k.z - 0.9), (0, APEX - 0.9), (-k.x + 0.4, k.z - 0.9)]
        for y0, y1, out in ((-hy, -hy + WT, (0, -1, 0)), (hy - WT, hy, (0, 1, 0))):
            bm = vl.prism_bm(prof, y0, y1, cap_bottom=True)
            self.body.add(bm, Matrix.Identity(4), self.wall_mat(out), vl.world_uv("planks"))
            self.collider([(x, y, z) for x, z in prof for y in (y0, y1)])
        # Plancher (descend sous le terrain pour couvrir une légère pente).
        c, size = (0, 0, -0.85), (W - 2 * WT, D - 2 * WT, 2.3)
        self.block(self.body, c, size, PLANKS, vl.world_uv("planks", (1.3, 0, 0)))
        self.collider_box(c, size)
        # Seuil devant la porte.
        self.block(self.body, (0, -hy + 0.2, -0.6), (DOOR_W, 1.6, 1.8), PLANKS, vl.world_uv("planks"))

    # ---------------------------------------------------------- encadrements blancs
    def build_trims(self):
        hx, hy = W / 2, D / 2
        for sx in (-1, 1):
            for sy in (-1, 1):
                c = Vector((sx * (hx - 0.5), sy * (hy - 0.5), 0))
                self.white(c + Vector((0, 0, -0.1)), c + Vector((0, 0, H + 0.2)), (1, 0, 0), 1.3, 1.3, wobble=0.2)
        # Bandeau en haut des murs (sur les pignons, comme sur la planche) et plinthe.
        for sy in (-1, 1):
            y = sy * (hy + 0.12)
            self.white((-hx + 0.6, y, H - 0.2), (hx - 0.6, y, H - 0.2), (0, 1, 0), 0.5, 0.9)
            self.white((-hx + 0.6, y, 0.45), (-DOOR_W / 2 - 0.9, y, 0.45) if sy < 0 else (hx - 0.6, y, 0.45), (0, 1, 0), 0.4, 0.8)
            if sy < 0:
                self.white((DOOR_W / 2 + 0.9, y, 0.45), (hx - 0.6, y, 0.45), (0, 1, 0), 0.4, 0.8)
        for sx in (-1, 1):
            x = sx * (hx + 0.12)
            self.white((x, -hy + 0.6, 0.45), (x, hy - 0.6, 0.45), (1, 0, 0), 0.4, 0.8)
        # Encadrement de la grande porte.
        y = -hy - 0.15
        for sx in (-1, 1):
            self.white((sx * (DOOR_W / 2 + 0.4), y, -0.1), (sx * (DOOR_W / 2 + 0.4), y, DOOR_H + 0.2), (0, 1, 0), 0.6, 0.9)
        self.white((-DOOR_W / 2 - 1.3, y, DOOR_H + 0.55), (DOOR_W / 2 + 1.3, y, DOOR_H + 0.55), (0, 1, 0), 0.7, 1.1)

    def square_window(self, center, axis, out, size, panes):
        """Fenêtre carrée : fond noir + cadre blanc (+ petits bois en croix si panes)."""
        c, a, o = Vector(center), Vector(axis), Vector(out)
        up = Vector((0, 0, 1))
        m = Matrix((a, up.cross(a), up)).transposed().to_4x4()
        m.translation = c + o * 0.05
        gs = (size, 0.1, size)
        self.body.add(vl.box_bm(gs), m, WOOD, vl.special_uv("black", gs))
        h, fw = size / 2, 0.55
        p = c + o * 0.25
        for sz in (-1, 1):
            self.white(p + a * (-h - fw) + up * sz * (h + fw / 2), p + a * (h + fw) + up * sz * (h + fw / 2), o, 0.45, fw)
        for sa in (-1, 1):
            self.white(p + a * sa * (h + fw / 2) + up * (-h), p + a * sa * (h + fw / 2) + up * h, o, 0.45, fw)
        if panes:
            for k in (-1, 1):
                self.white(p + a * (k * h / 3) + up * -h, p + a * (k * h / 3) + up * h, o, 0.3, 0.22, wobble=0)
                self.white(p + a * -h + up * (k * h / 3), p + a * h + up * (k * h / 3), o, 0.3, 0.22, wobble=0)

    def build_openings(self):
        hx, hy = W / 2, D / 2
        # Fenêtre du fenil au-dessus de la porte, 3 petites fenêtres par côté.
        self.square_window((0, -hy, 22.6), (1, 0, 0), (0, -1, 0), 5.0, True)
        for sx in (-1, 1):
            for k in (-1, 0, 1):
                self.square_window((sx * hx, k * 11.0, 8.5), (0, 1, 0), (sx, 0, 0), 2.6, False)
        # Petite porte de derrière (planches + encadrement blanc).
        y = hy + 0.1
        for i in range(5):
            x = -1.6 + i * 0.8
            self.beam(self.body, (x, y, 0.0), (x, y, 6.5 + self.jit(0.1)), (0, 1, 0), 0.3, 0.78, mat=WOOD, bevel=0.08, wobble=0.5)
        for sx in (-1, 1):
            self.white((sx * 2.4, y + 0.1, -0.1), (sx * 2.4, y + 0.1, 6.9), (0, 1, 0), 0.5, 0.7)
        self.white((-3.0, y + 0.1, 7.0), (3.0, y + 0.1, 7.0), (0, 1, 0), 0.5, 0.8)

    def build_doors(self):
        """Deux vantaux rouges à croix blanche, ouverts vers l'extérieur (105°)."""
        hy = D / 2
        lw, th = DOOR_W / 2, 0.5
        up = Vector((0, 0, 1))
        for side in (-1, 1):
            hinge = Vector((side * DOOR_W / 2, -hy - 0.35, 0.05))
            a = math.radians(-105 if side < 0 else 285)
            closed_dir = Vector((1, 0, 0)) if side < 0 else Vector((-1, 0, 0))
            dirv = Vector((math.cos(a), math.sin(a), 0)) if side < 0 else Vector((math.cos(a), math.sin(a), 0))
            nrm = up.cross(dirv)
            rot = Matrix((dirv, nrm, up)).transposed().to_4x4()
            center = hinge + dirv * (lw / 2) + up * (DOOR_H / 2 - 0.1)
            m = Matrix.Translation(center) @ rot
            size = (lw - 0.1, th, DOOR_H - 0.2)
            self.body.add(vl.box_bm(size, 0.08), m, RED, vl.world_uv_n("planks", rot))
            self.collider_box(center, size, rot)

            def L(u, v, o):
                return hinge + dirv * u + up * v + nrm * o

            for o in (-(th / 2 + 0.2), th / 2 + 0.2):
                fw = 0.7
                self.white(L(0.1, fw / 2, o), L(lw - 0.1, fw / 2, o), nrm, 0.4, fw)
                self.white(L(0.1, DOOR_H - fw / 2 - 0.1, o), L(lw - 0.1, DOOR_H - fw / 2 - 0.1, o), nrm, 0.4, fw)
                self.white(L(fw / 2 + 0.05, fw, o), L(fw / 2 + 0.05, DOOR_H - fw, o), nrm, 0.4, fw)
                self.white(L(lw - fw / 2 - 0.1, fw, o), L(lw - fw / 2 - 0.1, DOOR_H - fw, o), nrm, 0.4, fw)
                self.white(L(fw, fw, o), L(lw - fw, DOOR_H - fw, o), nrm, 0.35, 0.6)
                self.white(L(lw - fw, fw, o), L(fw, DOOR_H - fw, o), nrm, 0.35, 0.6)
            del closed_dir

    # ---------------------------------------------------------- toit à la Mansart
    def pans(self):
        """Les 4 pans : (haut, direction de la pente, normale, longueur le long de la pente)."""
        out = []
        for s in (-1, 1):
            k = Vector((s * self.knee.x, 0, self.knee.z))
            e = Vector((s * self.eave.x, 0, self.eave.z))
            dl = (e - k).normalized()
            e2 = e + dl * (OV / abs(dl.x))
            for top, bot in ((self.apex, k), (k, e2)):
                d = (bot - top).normalized()
                nrm = Vector((s * -d.z, 0, d.x * s)) if s > 0 else Vector((d.z, 0, -d.x))
                if nrm.z < 0:
                    nrm = -nrm
                out.append((top, d, nrm, (bot - top).length, top is self.apex))
        return out

    def build_roof(self):
        L = D + 2 * GOV
        for top, d, nrm, length, upper in self.pans():
            thick = 0.6 + TILE_BASE
            ctr = top + d * (length / 2) + nrm * (TILE_BASE - 0.02 - thick / 2)
            self.beam(self.body, ctr - Vector((0, L / 2, 0)), ctr + Vector((0, L / 2, 0)), d, length + (0.2 if upper else 0.0), thick,
                      mat=WOOD, band=2, bevel=0.06, wobble=0)
            t1 = length + (0.6 if upper else 0.35)
            self.tiles(self.roof, top, d, nrm, -0.35 if upper else -0.1, t1, L + 0.5, TILE_BASE)
            # Collider du pan (épais, pour que la caméra ne passe pas à travers).
            pts = []
            for t in (-0.3, length + 0.3):
                for y in (-L / 2 - 0.3, L / 2 + 0.3):
                    for h in (-0.7, TILE_BASE + 0.7):
                        pts.append(top + d * t + Vector((0, y, 0)) + nrm * h)
            self.collider(pts)
            # Rives blanches sur les pignons.
            for sy in (-1, 1):
                y = sy * (D / 2 + GOV - 0.35)
                off = TILE_BASE + 0.02 - 0.7          # 1,4 m de haut : couvre tout le chant du voligeage
                self.white(top + Vector((0, y, 0)) + nrm * off, top + d * (length + (0.0 if upper else 0.3)) + Vector((0, y, 0)) + nrm * off,
                           (0, 1, 0), 0.95, 1.4, wobble=0)
        # Faîtière en ardoise.
        z = APEX + TILE_BASE + 0.45
        L2 = L + 0.8
        size = (L2, 1.6, 0.9)

        def uv(lc, wc, n):
            return (0.03 + 0.19 * (lc.x / L2 + 0.5), 0.55 + 0.4 * (lc.z / 0.9 + 0.5))
        m = Matrix.Translation((0, 0, z)) @ Matrix.Rotation(math.radians(90), 4, "Z")
        self.roof.add(vl.box_bm(size, 0.25, 2), m, 0, uv)

    def build(self, coll):
        self.build_walls()
        self.build_trims()
        self.build_openings()
        self.build_doors()
        self.build_roof()
        body = self.body.to_object("Barn_Body", coll)
        roof = self.roof.to_object("Barn_Roof", coll, weighted=False)
        cols = self.collider_objects("Barn", coll)
        return body, roof, cols


def main(do_export=False):
    coll = vl.reset_collection(COLL)
    b = Barn()
    body, roof, cols = b.build(coll)
    vl.assign(body, ["planks_red", "planks", "wood_white", "wood"])
    vl.assign(roof, ["roof_Slate"])
    if do_export:
        vl.export_fbx([body, roof] + cols, os.path.join(vl.OUT_DIR, "SM_Barn.fbx"))
    bpy.context.view_layer.update()
    return {"tris": {o.name: vl.tri_count(o) for o in (body, roof)}, "colliders": len(cols),
            "dims": [round(v, 2) for v in body.dimensions],
            "top_z": round(max((roof.matrix_world @ Vector(c)).z for c in roof.bound_box), 2)}


if __name__ == "__main__" or "DO_EXPORT" in globals():
    result = main(globals().get("DO_EXPORT", False))
