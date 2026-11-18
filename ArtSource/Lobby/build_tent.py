# Construit la tente du médecin du Lobby (T1) et l'exporte en FBX pour Unity.
# Planche : GameDesign/99_Attachments/Lobby/Planches/Planche_P08_Tente_Medecin.png ; recadrage Refs/Ref_Lobby_11_Tente_Medecin.jpg.
#
# Lancer dans Blender 5.2 (MCP ou Scripting), après build_barn.py dans le même fichier (barn_tent.blend) :
#   exec(open(r"<dépôt>\ArtSource\Lobby\build_tent.py", encoding="utf-8").read(), {"DO_EXPORT": True})
# Textures : python ArtSource/Lobby/gen_village_textures.py ; outils communs : village_lib.py.
#
# Tente visitable : murs de toile légèrement évasés en bas, toit à deux pans qui s'affaisse entre les
# perches, lambrequin le long des gouttières, entrée à cadre de bois avec rideaux noués, haubans et piquets,
# croix rouge sur chaque pan du toit et au-dessus de l'entrée (demande de Gabin, 2026-11-16). Repère : 1 unité = 1 m, pivot au sol au centre, entrée vers -Y (+Z dans Unity).
# Objets exportés (SM_Tent_Medic.fbx) :
#   Tent_Canvas  0 = toile (M_Village_Canvas) — surfaces épaissies par un modificateur Solidify (0,12 m)
#   Tent_Frame   0 = bois (M_Village_Wood), 1 = peinture rouge (M_Village_PaintRed), 2 = toile (cordes)
#   Tent_Col*    colliders convexes (calque Wall dans Unity)
import importlib
import math
import os
import sys

import bpy
from mathutils import Vector

_DIR = os.path.join(os.environ.get("TEAMTIMO_PROJECT", "C:/Users/gabin/Documents/Projets Unity/TeamTimo"), "ArtSource", "Lobby")
if _DIR not in sys.path:
    sys.path.insert(0, _DIR)
import village_lib as vl  # noqa: E402

importlib.reload(vl)

BLEND_PATH = os.path.join(vl.PROJECT, "ArtSource", "Lobby", "barn_tent.blend")
COLL = "Village_Tent"

XB, XE = 9.5, 8.6          # demi-largeur au sol / à la gouttière (murs évasés)
HY = 12.0                  # demi-profondeur (24 m)
ZE, ZR = 9.0, 16.0         # gouttière, faîtage
ROOF_OV, ROOF_GOV = 0.9, 0.6
DOOR_X, DOOR_Z = 3.0, 8.0  # demi-largeur et hauteur de l'entrée
SAG = 0.35                 # affaissement de la toile entre les perches (m)
POLE_STEP = 6.0            # espacement des perches le long du faîtage
THICK = 0.12               # épaisseur de la toile
CROSS_Z = 11.9             # centre de la croix du pignon avant (au-dessus de l'entrée)
WOOD, RED, ROPE = 0, 1, 2


def lerp(a, b, t):
    return a + (b - a) * t


def zroof(x):
    return ZR - (ZR - ZE) / XE * abs(x)


class Tent(vl.Builder):
    def __init__(self):
        super().__init__(31)
        self.canvas, self.frame = vl.MB(), vl.MB()
        self.slope_d = Vector((XE, 0, ZE - ZR)).normalized()          # pente côté +X (x > 0)
        self.slope_len = Vector((XE, 0, ZE - ZR)).length
        self.roof_len = self.slope_len + ROOF_OV

    # ---------------------------------------------------------- surfaces de toile
    def roof_point(self, side, y, t, lift=0.0):
        """Point du pan (side = ±1) : y le long du faîtage, t le long de la pente depuis le faîtage."""
        d = Vector((side * self.slope_d.x, 0, self.slope_d.z))
        nrm = Vector((side * -self.slope_d.z, 0, self.slope_d.x))
        tt = min(t / self.slope_len, 1.0)
        poles = 0.35 + 0.65 * abs(math.sin(math.pi * y / POLE_STEP))
        sag = SAG * math.sin(math.pi * tt) * poles
        wr = 0.05 * math.sin(y * 2.3 + t * 1.1) * math.sin(math.pi * tt)
        return Vector((0, y, ZR)) + d * t - nrm * (sag + wr) + nrm * lift

    def build_roof(self):
        yl = HY + ROOF_GOV
        for side in (-1, 1):
            self.canvas.grid(lambda s, t, side=side: self.roof_point(side, lerp(-yl, yl, s), t * self.roof_len),
                             32, 12, 0, lambda s, t, p: (p.y / 8.0, t * self.roof_len / 8.0))
            # Lambrequin le long de la gouttière.
            e = self.roof_point(side, 0, self.roof_len)
            self.canvas.grid(lambda s, t, side=side, e=e: Vector((
                side * (abs(e.x) + 0.15 * t + 0.05 * math.sin(lerp(-yl, yl, s) * 1.7)), lerp(-yl, yl, s), e.z - 1.5 * t)),
                32, 2, 0, lambda s, t, p: (p.y / 8.0, p.z / 8.0))
            # Croix rouge au centre du pan, qui suit l'affaissement.
            tc = self.slope_len * 0.48
            cell = 0.75
            for i in range(-3, 3):
                for j in range(-3, 3):
                    ci, cj = i + 0.5, j + 0.5
                    if not ((abs(ci) < 1 and abs(cj) < 3) or (abs(cj) < 1 and abs(ci) < 3)):
                        continue
                    y0, t0 = i * cell, tc + j * cell
                    pts = [self.roof_point(side, y0, t0, 0.05), self.roof_point(side, y0 + cell, t0, 0.05),
                           self.roof_point(side, y0 + cell, t0 + cell, 0.05), self.roof_point(side, y0, t0 + cell, 0.05)]
                    uvs = [(y0 / 3.0, t0 / 3.0), ((y0 + cell) / 3.0, t0 / 3.0), ((y0 + cell) / 3.0, (t0 + cell) / 3.0), (y0 / 3.0, (t0 + cell) / 3.0)]
                    f = self.frame.face(pts, RED, uvs)
                    f.normal_update()
                    if f.normal.z < 0:
                        f.normal_flip()

    def build_door_cross(self):
        """Croix rouge sur le pignon avant, au-dessus de l'entrée (visible en arrivant par le chemin)."""
        zc, cell, y = CROSS_Z, 0.6, -HY - 0.24
        for i in range(-3, 3):
            for j in range(-3, 3):
                ci, cj = i + 0.5, j + 0.5
                if not ((abs(ci) < 1 and abs(cj) < 3) or (abs(cj) < 1 and abs(ci) < 3)):
                    continue
                x0, z0 = i * cell, zc + j * cell
                pts = [Vector((x0, y, z0)), Vector((x0, y, z0 + cell)), Vector((x0 + cell, y, z0 + cell)), Vector((x0 + cell, y, z0))]
                uvs = [(x0 / 3.0, z0 / 3.0), (x0 / 3.0, (z0 + cell) / 3.0), ((x0 + cell) / 3.0, (z0 + cell) / 3.0), ((x0 + cell) / 3.0, z0 / 3.0)]
                f = self.frame.face(pts, RED, uvs)
                f.normal_update()
                if f.normal.y > 0:
                    f.normal_flip()

    def build_walls(self):
        for side in (-1, 1):
            def wall(s, t, side=side):
                y = lerp(-HY, HY, s)
                x = lerp(XB, XE, t)
                z = lerp(0.0, ZE, t)
                fold = 0.12 * math.sin(y * 2.4 + side) * (1 - 0.6 * t) + 0.2 * (1 - t) ** 2
                return Vector((side * (x + fold), y, z))
            self.canvas.grid(wall, 32, 8, 0, lambda s, t, p: (p.y / 8.0, p.z / 8.0))

    def gable_quad(self, ysign, a, b, c, d, ns, nt):
        """Quadrilatère du pignon (points (x, z) : bas-gauche, bas-droit, haut-droit, haut-gauche), plis légers."""
        def fn(s, t):
            bot = (lerp(a[0], b[0], s), lerp(a[1], b[1], s))
            top = (lerp(d[0], c[0], s), lerp(d[1], c[1], s))
            x, z = lerp(bot[0], top[0], t), lerp(bot[1], top[1], t)
            fold = 0.08 * math.sin(x * 2.6) * (1 - 0.5 * t) + 0.12 * (1 - t) ** 2
            return Vector((x, ysign * (HY + fold), z))
        self.canvas.grid(fn, ns, nt, 0, lambda s, t, p: (p.x / 8.0, p.z / 8.0))

    def build_gables(self):
        zd = zroof(DOOR_X)
        for sx in (-1, 1):
            # Avant : de part et d'autre de l'entrée, puis au-dessus.
            self.gable_quad(-1, (sx * XB, 0), (sx * DOOR_X, 0), (sx * DOOR_X, zd), (sx * XE, ZE), 8, 8)
            self.gable_quad(-1, (sx * DOOR_X, DOOR_Z), (0, DOOR_Z), (0, ZR), (sx * DOOR_X, zd), 4, 6)
            # Arrière : plein.
            self.gable_quad(1, (sx * XB, 0), (0, 0), (0, ZR), (sx * XE, ZE), 10, 10)
        self.collider([(x, y, z) for x, z in ((-XB, 0), (-DOOR_X, 0), (-DOOR_X, zd), (-XE, ZE)) for y in (-HY - 0.25, -HY + 0.3)])
        self.collider([(x, y, z) for x, z in ((XB, 0), (DOOR_X, 0), (DOOR_X, zd), (XE, ZE)) for y in (-HY - 0.25, -HY + 0.3)])
        self.collider([(x, y, z) for x, z in ((-DOOR_X, DOOR_Z), (DOOR_X, DOOR_Z), (DOOR_X, zd), (0, ZR), (-DOOR_X, zd)) for y in (-HY - 0.25, -HY + 0.3)])
        self.collider([(x, y, z) for x, z in ((-XB, 0), (XB, 0), (XE, ZE), (0, ZR), (-XE, ZE)) for y in (HY - 0.3, HY + 0.25)])
        for sx in (-1, 1):
            self.collider([(sx * x, y, z) for x, z in ((XB + 0.3, 0), (XB - 0.4, 0), (XE + 0.25, ZE), (XE - 0.4, ZE)) for y in (-HY, HY)])
        for sx in (-1, 1):
            pts = []
            for t in (-0.3, self.roof_len + 0.2):
                for y in (-HY - ROOF_GOV, HY + ROOF_GOV):
                    for h in (-0.7, 0.4):
                        p = Vector((0, y, ZR)) + Vector((sx * self.slope_d.x, 0, self.slope_d.z)) * t
                        nrm = Vector((sx * -self.slope_d.z, 0, self.slope_d.x))
                        pts.append(p + nrm * h)
            self.collider(pts)

    def build_curtains(self):
        """Rideaux relevés et noués de chaque côté de l'entrée."""
        for sx in (-1, 1):
            def curtain(s, t, sx=sx):
                z = lerp(DOOR_Z + 0.1, 0.05, t)
                tie = 0.55
                if t < tie:
                    w = lerp(2.5, 0.7, (t / tie) ** 0.8)
                else:
                    w = lerp(0.7, 1.5, ((t - tie) / (1 - tie)) ** 1.5)
                x = sx * (DOOR_X - 0.05 - s * w)
                depth = 0.22 * math.sin(math.pi * s * 4) * (0.4 + 0.6 * abs(t - tie) / max(tie, 1 - tie))
                return Vector((x, -HY + 0.45 + depth, z))
            self.canvas.grid(curtain, 8, 12, 0, lambda s, t, p: (p.x / 4.0, p.z / 4.0))
            # Embrasse (corde) autour du rideau.
            z = lerp(DOOR_Z + 0.1, 0.05, 0.55)
            self.beam(self.frame, (sx * (DOOR_X + 0.05), -HY + 0.45, z), (sx * (DOOR_X - 0.85), -HY + 0.45, z), (0, 1, 0), 0.55, 0.3,
                      mat=ROPE, bevel=0.1, wobble=2)

    # ---------------------------------------------------------- bois et cordes
    def build_frame(self):
        y = -HY - 0.15
        for sx in (-1, 1):
            self.beam(self.frame, (sx * (DOOR_X + 0.4), y, -0.1), (sx * (DOOR_X + 0.4), y, DOOR_Z + 0.5), (0, 1, 0), 0.85, 0.8,
                      bevel=0.15, segments=2, wobble=1.2)
            self.collider_box((sx * (DOOR_X + 0.4), y, DOOR_Z / 2), (0.9, 0.9, DOOR_Z + 0.6))
        self.beam(self.frame, (-DOOR_X - 1.3, y - 0.05, DOOR_Z + 0.55), (DOOR_X + 1.3, y - 0.05, DOOR_Z + 0.55), (0, 1, 0), 0.95, 1.05,
                  bevel=0.2, segments=2, wobble=0.8)
        # Perches intérieures : faîtage + mât du fond (visibles par l'entrée).
        self.beam(self.frame, (0, -HY + 0.3, ZR - 0.55), (0, HY - 0.3, ZR - 0.55), (1, 0, 0), 0.5, 0.5, bevel=0.1, wobble=0.2)
        self.beam(self.frame, (0, HY - 0.5, 0), (0, HY - 0.5, ZR - 0.6), (1, 0, 0), 0.6, 0.6, bevel=0.12, wobble=0.5)

    def build_ropes(self):
        yl = HY + ROOF_GOV - 0.2
        anchors = []
        for sx in (-1, 1):
            for sy in (-1, 1):
                anchors.append((self.roof_point(sx, sy * yl, self.roof_len - 0.1), Vector((sx * 14.0, sy * 17.0, 0.0))))
            for yy in (-6.0, 6.0):
                anchors.append((self.roof_point(sx, yy, self.roof_len - 0.1), Vector((sx * 15.5, yy * 1.05, 0.0))))
        for top, stake in anchors:
            dirv = (stake - top).normalized()
            knot = stake + Vector((0, 0, 1.1)) - Vector((dirv.x, dirv.y, 0)).normalized() * 0.15
            self.beam(self.frame, top, knot, (0, 0, 1), 0.16, 0.16, mat=ROPE, bevel=0.05, wobble=0)
            out = Vector((dirv.x, dirv.y, 0)).normalized()
            p0, p1 = stake - out * 0.25 + Vector((0, 0, -0.6)), stake + out * 0.2 + Vector((0, 0, 1.5))
            self.beam(self.frame, p0, p1, (0, 0, 1) if abs(out.z) < 0.9 else (1, 0, 0), 0.5, 0.5, bevel=0.1, wobble=4)
            self.beam(self.frame, knot - out * 0.05 + Vector((0, 0, -0.15)), knot - out * 0.05 + Vector((0, 0, 0.25)),
                      (out.y, -out.x, 0), 0.7, 0.7, mat=ROPE, bevel=0.15, wobble=0)

    def finish_canvas(self, coll):
        bm = self.canvas.bm
        bm.normal_update()
        c = Vector((0, 0, 6))
        for f in bm.faces:
            if f.normal.dot(f.calc_center_median() - c) < 0:
                f.normal_flip()
        ob = self.canvas.to_object("Tent_Canvas", coll, sharp_angle=89.0, weighted=False)
        sol = ob.modifiers.new("Epaisseur", "SOLIDIFY")
        sol.thickness = THICK
        sol.offset = -1.0
        sol.use_even_offset = True
        return ob

    def build(self, coll):
        self.build_roof()
        self.build_door_cross()
        self.build_walls()
        self.build_gables()
        self.build_curtains()
        self.build_frame()
        self.build_ropes()
        canvas = self.finish_canvas(coll)
        frame = self.frame.to_object("Tent_Frame", coll)
        cols = self.collider_objects("Tent", coll)
        return canvas, frame, cols


def main(do_export=False):
    coll = vl.reset_collection(COLL)
    t = Tent()
    canvas, frame, cols = t.build(coll)
    vl.assign(canvas, ["canvas"])
    vl.assign(frame, ["wood", "paint_red", "canvas"])
    if do_export:
        vl.export_fbx([canvas, frame] + cols, os.path.join(vl.OUT_DIR, "SM_Tent_Medic.fbx"))
    for o in [canvas, frame] + cols:
        o.location.x = -45.0                 # à côté de la grange dans le .blend (exporté à l'origine)
    bpy.context.view_layer.update()
    return {"tris": {o.name: vl.tri_count(o) for o in (canvas, frame)}, "colliders": len(cols),
            "dims": [round(v, 2) for v in canvas.dimensions]}


if __name__ == "__main__" or "DO_EXPORT" in globals():
    result = main(globals().get("DO_EXPORT", False))
