# Construit le kit « maison de village » du Lobby (modules S, M, L) et l'exporte en FBX pour Unity.
# Planches : GameDesign/99_Attachments/Lobby/Planches/Planche_P01_Maison_S.png et Planche_P02_Maisons_M_L.png
# Style : la maison de Gabin (GameDesign/99_Attachments/Pasted image 20261115161334.png).
#
# Lancer dans Blender 5.2 (MCP ou Scripting), fichier vide ou ArtSource/Lobby/houses.blend :
#   exec(open(r"<dépôt>\ArtSource\Lobby\build_houses.py", encoding="utf-8").read(), {"DO_EXPORT": True})
# Textures : python ArtSource/Lobby/gen_village_textures.py (à lancer avant, écrit dans Assets/Art/Textures/Environment/Village/).
#
# Repère : 1 unité = 1 m du jeu, pivot au sol au centre de l'emprise, porte (pignon avant) vers -Y
# (elle regarde +Z dans Unity). X = largeur de la façade pignon, Y = profondeur (faîtage le long de Y).
# Outils communs : village_lib.py (même dossier).
# Objets exportés par module (SM_House_<S|M|L>.fbx) :
#   House_<S>_Body           0 = bois (M_Village_Wood), 1 = enduit (M_Village_Plaster), 2 = pierre (M_WallStone)
#   House_<S>_Roof           0 = tuiles (M_Roof_Red / Blue / Yellow / Wood : la couleur se change par matériau)
#   House_<S>_Chimney        option « cheminee » : 0 = pierre, 1 = bois (noir du conduit)
#   House_<S>_Dormer         option « lucarne » (corps) : 0 = bois, 1 = enduit
#   House_<S>_DormerRoof     option « lucarne » (toit) : 0 = tuiles
#   House_<S>_LeanTo         option « appentis » : 0 = bois
#   House_<S>_Awning         option « auvent_toile » : 0 = bois, 1 = toile (M_Village_Canvas)
#   House_<S>_Porch          option « perron » : 0 = bois
#   House_<S>_CrossGable     option « pignon_croise » (corps) : 0 = bois, 1 = enduit
#   House_<S>_CrossGableRoof option « pignon_croise » (toit) : 0 = tuiles
#   House_<S>_Col0..1        colliders convexes du bâtiment (calque Wall dans Unity)
#   House_<S>_*Col*          colliders des options (appentis, perron, pignon_croise)
import importlib
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

_DIR = os.path.join(os.environ.get("TEAMTIMO_PROJECT", "C:/Users/gabin/Documents/Projets Unity/TeamTimo"), "ArtSource", "Lobby")
if _DIR not in sys.path:
    sys.path.insert(0, _DIR)
import village_lib as vl  # noqa: E402

importlib.reload(vl)
from village_lib import MB, box_bm, special_uv, world_uv  # noqa: E402

PROJECT, OUT_DIR = vl.PROJECT, vl.OUT_DIR
BLEND_PATH = os.path.join(PROJECT, "ArtSource", "Lobby", "houses.blend")
COLL = "Village_Houses"

# Modules : largeur (pignon), profondeur, hauteur des murs, hauteur au faîtage, travées, soubassement de pierre.
MODULES = {
    "S": dict(W=12.0, D=12.0, H=9.0, RIDGE=17.0, bays=1, plinth=False, seed=11),
    "M": dict(W=12.0, D=16.0, H=9.0, RIDGE=18.0, bays=2, plinth=True, seed=12),
    "L": dict(W=13.0, D=24.0, H=9.0, RIDGE=20.0, bays=3, plinth=True, seed=13),
}

# Charpente
T = 0.9            # section des poteaux d'angle
PROTRUDE = 0.35    # saillie du bois devant l'enduit
OV = 1.2           # débord du toit côté gouttière (horizontal)
GOV = 1.1          # débord du toit côté pignon
DECK = 0.35        # épaisseur du voligeage sous la ligne de pente
TILE_BASE = 0.4    # les tuiles reposent à 0,4 m au-dessus de la ligne de pente = dessus des chevrons de rive
RAFTER = 1.05      # hauteur des chevrons de rive
RAIL_Z = 1.6       # lisse basse


class House(vl.Builder):
    def __init__(self, key, W, D, H, RIDGE, bays, plinth, seed):
        super().__init__(seed)
        self.key, self.W, self.D, self.H, self.bays, self.plinth = key, W, D, H, bays, plinth
        self.body, self.roof, self.chim = MB(), MB(), MB()
        self.dormer, self.dormer_roof = MB(), MB()
        self.leanto, self.awning, self.porch = MB(), MB(), MB()
        self.cross_gable, self.cross_gable_roof = MB(), MB()
        self.opt_colliders = {}      # nom_col -> liste de nuages de points convexes
        # Ligne de pente = dessus du voligeage. Passe 0,3 m au-dessus du haut du mur, au nu extérieur.
        self.Ra = RIDGE - 1.25                       # faîtage du voligeage (poutre faîtière au-dessus)
        self.Hp = H + 0.3
        self.slope = (self.Ra - self.Hp) / (W / 2)
        self.theta = math.atan(self.slope)

    # ---------------------------------------------------------- outils
    def beam(self, p0, p1, yaxis, dy, dz, band=None, bevel=0.14, segments=1, wobble=0.8, ext=0.0, mb=None):
        vl.Builder.beam(self, mb or self.body, p0, p1, yaxis, dy, dz, 0, band, bevel, segments, wobble, ext)

    def block(self, center, size, mat, uvfunc, mb=None, bevel=0.0, rot=None):
        vl.Builder.block(self, mb or self.body, center, size, mat, uvfunc, bevel, rot)

    def opt_collider_box(self, key, center, size):
        c, s = Vector(center), Vector(size) / 2
        pts = [c + Vector((sx * s.x, sy * s.y, sz * s.z)) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
        self.opt_colliders.setdefault(key, []).append(pts)

    @staticmethod
    def copy_transformed_mb(dst, src, mat):
        """Copie les faces de src dans dst en appliquant mat aux positions et en conservant les UV."""
        vmap = {v: dst.bm.verts.new(mat @ v.co) for v in src.bm.verts}
        for f in src.bm.faces:
            nf = dst.bm.faces.new([vmap[v] for v in f.verts])
            nf.material_index = f.material_index
            nf.smooth = f.smooth
            for l_new, l_old in zip(nf.loops, f.loops):
                l_new[dst.uv].uv = l_old[src.uv].uv
        src.bm.free()

    def z_roof(self, x):
        return self.Ra - self.slope * abs(x)

    # Murs : 0 avant (-Y), 1 droit (+X), 2 arrière (+Y), 3 gauche (-X). s = abscisse le long du mur.
    def wall(self, w):
        W, D = self.W, self.D
        if w == 0:
            return Vector((0, -D / 2, 0)), Vector((1, 0, 0)), Vector((0, -1, 0)), W / 2
        if w == 1:
            return Vector((W / 2, 0, 0)), Vector((0, 1, 0)), Vector((1, 0, 0)), D / 2
        if w == 2:
            return Vector((0, D / 2, 0)), Vector((-1, 0, 0)), Vector((0, 1, 0)), W / 2
        return Vector((-W / 2, 0, 0)), Vector((0, -1, 0)), Vector((-1, 0, 0)), D / 2

    def wp(self, w, s, z, depth):
        """Point sur le mur w : abscisse s, hauteur z, centre d'une pièce d'épaisseur depth affleurant le nu du bois."""
        o, t, n, _ = self.wall(w)
        return o + t * s + Vector((0, 0, z)) + n * (-depth / 2)

    def wall_beam(self, w, s0, z0, s1, z1, width, depth=0.7, **kw):
        _, _, n, _ = self.wall(w)
        self.beam(self.wp(w, s0, z0, depth), self.wp(w, s1, z1, depth), n, depth, width, **kw)

    # ---------------------------------------------------------- pièces
    def build_plaster(self):
        W, D = self.W, self.D
        pw, pd = W / 2 - PROTRUDE, D / 2 - PROTRUDE
        under = DECK / math.cos(self.theta) + 0.03
        zr = self.z_roof(pw) - under
        prof = [(-pw, 0.0), (pw, 0.0), (pw, self.H), (pw, zr), (0.0, self.Ra - under), (-pw, zr), (-pw, self.H)]
        bm = bmesh.new()
        front = [bm.verts.new((x, -pd, z)) for x, z in prof]
        back = [bm.verts.new((x, pd, z)) for x, z in prof]
        bm.faces.new(front[::-1])
        bm.faces.new(back)
        n = len(prof)
        for i in range(n):
            j = (i + 1) % n
            if i == 0:
                continue                                  # pas de face au sol
            bm.faces.new([front[i], front[j], back[j], back[i]])
        bm.normal_update()
        center = Vector((0, 0, self.H * 0.6))
        for f in bm.faces:
            if f.normal.dot(f.calc_center_median() - center) < 0:
                f.normal_flip()
        bm.normal_update()
        self.body.add(bm, Matrix.Identity(4), 1, world_uv("plaster"))

    def build_plinth(self):
        W, D = self.W, self.D
        hz, out = 1.5, 0.15
        pw, pd = W / 2 - PROTRUDE + out, D / 2 - PROTRUDE + out
        uv = world_uv("stone", (3.1, 1.7, 0.0))
        self.block((pw - 0.4, 0, hz / 2), (0.8, 2 * pd, hz), 2, uv, bevel=0.1)
        self.block((-pw + 0.4, 0, hz / 2), (0.8, 2 * pd, hz), 2, uv, bevel=0.1)
        self.block((0, pd - 0.4, hz / 2), (2 * pw, 0.8, hz), 2, uv, bevel=0.1)
        dw = 2.6
        seg = (pw - dw) / 2
        for sgn in (-1, 1):
            self.block((sgn * (dw + seg), -pd + 0.4, hz / 2), (2 * seg, 0.8, hz), 2, uv, bevel=0.1)

    def build_frame(self):
        W, D, H = self.W, self.D, self.H
        z0 = 0.0
        # Poteaux d'angle (montent jusque sous la sablière).
        for sx in (-1, 1):
            for sy in (-1, 1):
                c = Vector((sx * (W / 2 - T / 2), sy * (D / 2 - T / 2), 0))
                self.beam(c + Vector((0, 0, z0 - 0.1)), c + Vector((0, 0, H - 0.5)), (1, 0, 0), T, T, bevel=0.16, segments=2)
        # Sablières (haut des murs) : pignons et longs pans.
        top = H - 0.4
        for w in (0, 2):
            self.wall_beam(w, -W / 2 - 0.6, top, W / 2 + 0.6, top, 0.85, 0.8, bevel=0.15, segments=2)
        for w in (1, 3):
            self.wall_beam(w, -D / 2 + 0.2, top, D / 2 - 0.2, top, 0.8, 0.75)
        # Poteaux intermédiaires des longs pans (une travée = une fenêtre).
        inner = D / 2 - T
        for w in (1, 3):
            for b in range(1, self.bays):
                s = -inner + 2 * inner * b / self.bays
                self.wall_beam(w, s, 0.0, s, top - 0.4, 0.8, 0.75)
        # Lisses basses.
        rz = RAIL_Z + (0.2 if self.plinth else 0.0)
        for w in (1, 2, 3):
            half = (W if w == 2 else D) / 2 - T
            self.wall_beam(w, -half - 0.1, rz, half + 0.1, rz, 0.6, 0.6)
        dw = 2.6
        for sgn in (-1, 1):
            self.wall_beam(0, sgn * (W / 2 - T + 0.1), rz, sgn * (dw - 0.1), rz, 0.6, 0.6)

    def build_door(self):
        dw, dh = 2.0, 6.5
        for i in range(5):
            s = -dw + 0.4 + i * 0.8
            h = dh + self.jit(0.12)
            self.wall_beam(0, s, 0.0, s, h, 0.78, 0.3, wobble=0.6, bevel=0.08)
        for sgn in (-1, 1):
            self.wall_beam(0, sgn * (dw + 0.3), -0.05, sgn * (dw + 0.3), dh, 0.65, 0.65)
        self.wall_beam(0, -dw - 0.9, dh + 0.45, dw + 0.9, dh + 0.45, 0.9, 0.8, bevel=0.15, segments=2)
        # Poignée en laiton.
        p = self.wp(0, dw - 0.7, 3.3, -0.25)
        self.block(p, (0.35, 0.25, 0.7), 0, special_uv("brass", (0.35, 0.25, 0.7)), bevel=0.08)

    def window(self, w, s, zc, size, kind):
        """Fenêtre : vitre (verre ou croisillons) + cadre de 4 baguettes (+ meneau en croix pour le verre)."""
        half = size / 2
        o, t, n, _ = self.wall(w)
        gp = o + t * s + Vector((0, 0, zc)) + n * (-PROTRUDE + 0.06)
        up = Vector((0, 0, 1))
        gm = Matrix.Translation(gp) @ Matrix((t, up.cross(t), up)).transposed().to_4x4()   # repère direct (pas de miroir)
        gsize = (size, 0.1, size)
        self.body.add(box_bm(gsize), gm, 0, special_uv(kind, gsize))
        fw = 0.42
        self.wall_beam(w, s - half - fw, zc - half - fw / 2, s + half + fw, zc - half - fw / 2, fw * 1.3, 0.55, bevel=0.1)
        self.wall_beam(w, s - half - fw, zc + half + fw / 2, s + half + fw, zc + half + fw / 2, fw, 0.5, bevel=0.1)
        self.wall_beam(w, s - half - fw / 2, zc - half, s - half - fw / 2, zc + half, fw, 0.5, bevel=0.1)
        self.wall_beam(w, s + half + fw / 2, zc - half, s + half + fw / 2, zc + half, fw, 0.5, bevel=0.1)
        if kind == "glass":
            self.wall_beam(w, s, zc - half, s, zc + half, 0.22, 0.42, bevel=0.06, wobble=0)
            self.wall_beam(w, s - half, zc, s + half, zc, 0.22, 0.42, bevel=0.06, wobble=0)

    def build_gables(self):
        W, H = self.W, self.H
        top = H
        for w in (0, 2):
            for sgn in (-1, 1):
                s = sgn * 1.95
                self.wall_beam(w, s, top - 0.1, s, self.z_roof(s) - 0.5, 0.6, 0.55)
        self.window(0, 0.0, H + 2.4, 2.1, "glass")

    def build_side_windows(self):
        inner = self.D / 2 - T
        for w in (1, 3):
            for b in range(self.bays):
                s = -inner + inner * (2 * b + 1) / self.bays
                self.window(w, s, 4.7 + (0.2 if self.plinth else 0.0), 2.3, "lattice")

    def build_roof_structure(self):
        W, D = self.W, self.D
        c, sn = math.cos(self.theta), math.sin(self.theta)
        tmax = (W / 2 + OV) / c
        L = D + 2 * GOV
        L_deck = L - 1.3                               # s'arrête à l'intérieur des chevrons de rive (zéro Z-fighting)
        for side in (-1, 1):
            d = Vector((side * c, 0, -sn))
            nrm = Vector((side * sn, 0, c))
            apex = Vector((0, 0, self.Ra))
            # Voligeage : en retrait des chevrons de rive et sous les tuiles.
            thick = DECK
            ctr = apex + d * (tmax / 2 + 0.05) + nrm * (TILE_BASE - 0.08 - thick / 2)
            self.beam(ctr - Vector((0, L_deck / 2, 0)), ctr + Vector((0, L_deck / 2, 0)), d, tmax - 0.25, thick, band=2, bevel=0.04, wobble=0)
            # Chevrons de rive (grosses poutres des pignons) : coupés en onglet à x = 0 pour ne pas se chevaucher.
            off = TILE_BASE + 0.02 - RAFTER / 2
            for sy in (-1, 1):
                y = sy * (D / 2 + GOV - 0.5)
                p0 = apex + Vector((0, y, 0)) + nrm * off
                p1 = apex + d * (tmax + 0.5) + Vector((0, y, 0)) + nrm * off
                m, Lr = vl.frame_matrix(p0 - d * 0.25, p1, (0, 1, 0))
                size = (Lr, 0.95, RAFTER)
                bm = box_bm(size, 0.18, 2)
                for v in bm.verts:
                    v.co = m @ v.co
                bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                                       plane_co=(side * 0.002, 0, 0), plane_no=(side, 0, 0),
                                       clear_inner=True, clear_outer=False)
                b_edges = [e for e in bm.edges if e.is_boundary]
                if b_edges:
                    bmesh.ops.holes_fill(bm, edges=b_edges)
                bm.normal_update()
                m_inv = m.inverted()
                r_inv = m_inv.to_3x3()
                base_uv = vl.wood_uv(size, self.rng.randrange(3), self.rng.uniform(0, 4))
                self.body.add(bm, Matrix.Identity(4), 0, lambda lc, wc, n, mi=m_inv, ri=r_inv, bu=base_uv: bu(mi @ lc, wc, ri @ n))
        # Faîtière + têtes sculptées aux deux bouts.
        rz = self.Ra + TILE_BASE + 0.55
        self.beam((0, -L / 2 - 0.4, rz), (0, L / 2 + 0.4, rz), (1, 0, 0), 1.05, 1.1, band=1, bevel=0.2, segments=2, wobble=0.3)
        for sy in (-1, 1):
            y = sy * (D / 2 + GOV - 0.45)
            self.beam((0, y - 0.65, rz + 0.15), (0, y + 0.65, rz + 0.15), (1, 0, 0), 1.6, 1.5, band=0, bevel=0.3, segments=2, wobble=1.5)

    def build_tiles(self):
        W, D = self.W, self.D
        c, sn = math.cos(self.theta), math.sin(self.theta)
        tmax = (W / 2 + OV) / c
        L = D + 2 * GOV + 0.5
        for side in (-1, 1):
            d = Vector((side * c, 0, -sn))
            nrm = Vector((side * sn, 0, c))
            self.tiles(self.roof, Vector((0, 0, self.Ra)), d, nrm, -0.35, tmax + 0.35, L, TILE_BASE)

    def build_colliders(self):
        """Colliders (calque Wall dans Unity) : murs, puis prisme du toit (débords compris, hors de portée du joueur)."""
        W, D, H = self.W, self.D, self.H
        self.collider_box((0, 0, H / 2), (W, D, H))
        c = math.cos(self.theta)
        xe = W / 2 + OV + 0.3
        ze = self.z_roof(xe) - DECK / c - 0.2
        top = self.Ra + TILE_BASE + 1.2
        ye = D / 2 + GOV + 0.5
        self.collider([(sx * xe, sy * ye, ze) for sx in (-1, 1) for sy in (-1, 1)] + [(0, sy * ye, top) for sy in (-1, 1)])

    def build_chimney(self):
        W, D = self.W, self.D
        x, y = W / 4 + 0.3, D / 4
        zb = self.z_roof(x + 1.4) - 0.6
        zt = self.Ra + TILE_BASE + 3.6
        uv = world_uv("stone", (0.7, 2.3, 0.4))
        self.block((x, y, (zb + zt) / 2), (2.5, 2.5, zt - zb), 0, uv, mb=self.chim, bevel=0.12,
                   rot=Matrix.Rotation(math.radians(2.5), 4, "Z"))
        self.block((x, y, zt + 0.35), (3.1, 3.1, 0.8), 0, uv, mb=self.chim, bevel=0.15,
                   rot=Matrix.Rotation(math.radians(-2.0), 4, "Z"))
        self.block((x, y, zt + 0.8), (2.0, 2.0, 0.12), 1, special_uv("black", (2.0, 2.0, 0.12)), mb=self.chim)

    def _extrude_x_profile(self, mb, prof_yz, x0, x1, mat, uvfunc):
        """Extrude un polygone convexe (y, z) le long de X de x0 à x1 avec normales vers l'extérieur."""
        bm = bmesh.new()
        v0 = [bm.verts.new((x0, y, z)) for y, z in prof_yz]
        v1 = [bm.verts.new((x1, y, z)) for y, z in prof_yz]
        bm.faces.new(v0[::-1])
        bm.faces.new(v1)
        n = len(prof_yz)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new([v0[i], v0[j], v1[j], v1[i]])
        bm.normal_update()
        c = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
        for f in bm.faces:
            if f.normal.dot(f.calc_center_median() - c) < 0:
                f.normal_flip()
        bm.normal_update()
        mb.add(bm, Matrix.Identity(4), mat, uvfunc)

    def _add_cross_roof(self, body_mb, roof_mb, x_in, x_out_over, hy, z_eave, z_ridge, ov_y, r_scale=1.0):
        """Construit un toit à deux pans dont le faîtage court le long de X (lucarne ou pignon croisé)."""
        slope = (z_ridge - z_eave) / hy
        theta = math.atan(slope)
        c, sn = math.cos(theta), math.sin(theta)
        tmax = (hy + ov_y) / c
        x_lo, x_hi = min(x_in, x_out_over), max(x_in, x_out_over)
        L = x_hi - x_lo
        xc = (x_lo + x_hi) / 2
        thick = DECK * r_scale
        rafter_h = RAFTER * r_scale
        rafter_w = 0.72 * r_scale
        # Voligeage et chevrons de rive sur les deux pans.
        for side_y in (-1, 1):
            d = Vector((0, side_y * c, -sn))
            nrm = Vector((0, side_y * sn, c))
            apex = Vector((xc, 0, z_ridge))
            ctr = apex + d * (tmax / 2 + 0.05) + nrm * (TILE_BASE - 0.08 - thick / 2)
            self.beam(ctr - Vector((L / 2 - 0.2, 0, 0)), ctr + Vector((L / 2 - 0.2, 0, 0)), d, tmax - 0.2, thick,
                      band=2, bevel=0.04, wobble=0, mb=body_mb)
            # Chevrons de rive sur la face extérieure (celle la plus éloignée du centre maison).
            xf = x_out_over + (0.45 if x_out_over < x_in else -0.45)
            off = TILE_BASE + 0.02 - rafter_h / 2
            p0 = Vector((xf, 0, z_ridge)) + nrm * off
            p1 = Vector((xf, 0, z_ridge)) + d * (tmax + 0.35) + nrm * off
            m, Lr = vl.frame_matrix(p0 - d * 0.2, p1, (1, 0, 0))
            size = (Lr, rafter_w, rafter_h)
            bm = box_bm(size, 0.14 * r_scale, 2)
            for v in bm.verts:
                v.co = m @ v.co
            bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                                   plane_co=(0, side_y * 0.002, 0), plane_no=(0, side_y, 0),
                                   clear_inner=True, clear_outer=False)
            b_edges = [e for e in bm.edges if e.is_boundary]
            if b_edges:
                bmesh.ops.holes_fill(bm, edges=b_edges)
            bm.normal_update()
            m_inv = m.inverted()
            r_inv = m_inv.to_3x3()
            base_uv = vl.wood_uv(size, self.rng.randrange(3), self.rng.uniform(0, 4))
            body_mb.add(bm, Matrix.Identity(4), 0, lambda lc, wc, n, mi=m_inv, ri=r_inv, bu=base_uv: bu(mi @ lc, wc, ri @ n))
        # Faîtière et tête sculptée côté extérieur.
        rz = z_ridge + TILE_BASE + 0.45 * r_scale
        self.beam((x_lo - (0.3 if x_out_over < x_in else 0.0), 0, rz),
                  (x_hi + (0.3 if x_out_over > x_in else 0.0), 0, rz),
                  (0, 1, 0), 0.85 * r_scale, 0.9 * r_scale, band=1, bevel=0.16 * r_scale, segments=2, wobble=0.25, mb=body_mb)
        xf_head = x_out_over + (0.45 if x_out_over < x_in else -0.45)
        self.beam((xf_head - 0.5 * r_scale, 0, rz + 0.12 * r_scale),
                  (xf_head + 0.5 * r_scale, 0, rz + 0.12 * r_scale),
                  (0, 1, 0), 1.3 * r_scale, 1.2 * r_scale, band=0, bevel=0.24 * r_scale, segments=2, wobble=1.2, mb=body_mb)
        # Tuiles générées dans un MB temporaire puis tournées de +90° autour de Z (faîtage le long de X).
        tmp = MB()
        for side in (-1, 1):
            d = Vector((side * c, 0, -sn))
            nrm = Vector((side * sn, 0, c))
            self.tiles(tmp, Vector((0, 0, z_ridge)), d, nrm, -0.25, tmax + 0.25, L + 0.3, TILE_BASE)
        rot = Matrix.Translation((xc, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
        self.copy_transformed_mb(roof_mb, tmp, rot)

    def build_dormer(self):
        """Option « lucarne » (P03 #1) : fenêtre de toit à deux pans posée sur le pan +X."""
        xf = self.W / 2 - 0.15
        hy = 2.0
        z_tiles = self.z_roof(xf) + (TILE_BASE + vl.TILE_T + 0.04) / math.cos(self.theta)
        z_base = z_tiles - 0.05
        z_bot = max(self.H + 0.35, z_base - 1.1)
        z_eave = z_base + 2.25
        z_ridge = z_base + 3.95
        x_ridge_back = (self.Ra - z_ridge) / self.slope
        x_in = max(0.35, x_ridge_back - 0.55)
        # Volume d'enduit (façade et joues triangulaires qui s'encastrent dans le pan principal).
        pw = hy - 0.22
        prof = [(-pw, z_bot), (pw, z_bot), (pw, z_eave - 0.15), (0.0, z_ridge - 0.2), (-pw, z_eave - 0.15)]
        self._extrude_x_profile(self.dormer, prof, x_in, xf - 0.18, 1, world_uv("plaster"))
        # Cadre de colombage sur la façade (+X).
        fx = xf - 0.08
        self.beam((fx, -hy - 0.15, z_base + 0.12), (fx, hy + 0.15, z_base + 0.12), (1, 0, 0), 0.45, 0.45, bevel=0.08, mb=self.dormer)
        self.beam((fx, -pw, z_eave), (fx, pw, z_eave), (1, 0, 0), 0.42, 0.42, bevel=0.08, mb=self.dormer)
        for sy in (-1, 1):
            self.beam((fx, sy * pw, z_base), (fx, sy * pw, z_eave), (1, 0, 0), 0.42, 0.42, bevel=0.08, mb=self.dormer)
            self.beam((x_in, sy * pw, z_eave), (fx + 0.1, sy * pw, z_eave), (0, 0, 1), 0.4, 0.4, bevel=0.08, mb=self.dormer)
        # Fenêtre à 4 carreaux centrée sur la façade de la lucarne.
        win_s, win_z = 1.55, z_base + 1.2
        gx = xf - 0.12
        gsize = (win_s, 0.1, win_s)
        gm = Matrix.Translation((gx, 0, win_z)) @ Matrix.Rotation(math.radians(90), 4, "Z")
        self.dormer.add(box_bm(gsize), gm, 0, special_uv("glass", gsize))
        hs, fw = win_s / 2, 0.28
        self.beam((fx + 0.05, -hs - fw, win_z - hs - fw / 2), (fx + 0.05, hs + fw, win_z - hs - fw / 2), (1, 0, 0), 0.38, fw * 1.2, bevel=0.06, mb=self.dormer)
        self.beam((fx + 0.05, -hs - fw, win_z + hs + fw / 2), (fx + 0.05, hs + fw, win_z + hs + fw / 2), (1, 0, 0), 0.35, fw, bevel=0.06, mb=self.dormer)
        for sy in (-1, 1):
            self.beam((fx + 0.05, sy * (hs + fw / 2), win_z - hs), (fx + 0.05, sy * (hs + fw / 2), win_z + hs), (1, 0, 0), 0.35, fw, bevel=0.06, mb=self.dormer)
        self.beam((fx + 0.02, 0, win_z - hs), (fx + 0.02, 0, win_z + hs), (1, 0, 0), 0.28, 0.16, bevel=0.04, wobble=0, mb=self.dormer)
        self.beam((fx + 0.02, -hs, win_z), (fx + 0.02, hs, win_z), (1, 0, 0), 0.28, 0.16, bevel=0.04, wobble=0, mb=self.dormer)
        # Petit toit à deux pans de la lucarne.
        self._add_cross_roof(self.dormer, self.dormer_roof, x_in, xf + 0.85, hy, z_eave, z_ridge, 0.55, r_scale=0.68)

    def build_leanto(self):
        """Option « appentis » (P03 #3) : abri en planches à une pente contre le mur -X, sur 2 poteaux."""
        xw = -self.W / 2 + 0.15
        xp = -self.W / 2 - 4.0
        hy = 3.5
        yp = 3.05
        zh, zl = 7.8, 5.6
        # 2 poteaux avant + sablière avant + lisse murale + jambes de force.
        for sy in (-1, 1):
            self.beam((xp, sy * yp, -0.15), (xp, sy * yp, zl - 0.1), (1, 0, 0), 0.62, 0.62, bevel=0.12, mb=self.leanto)
            self.beam((xp, sy * yp, zl - 1.7), (xp, sy * (yp - 1.35), zl - 0.15), (1, 0, 0), 0.42, 0.42, bevel=0.08, mb=self.leanto)
            self.beam((xw, sy * yp, zh - 1.3), (xp, sy * yp, zl - 0.25), (0, 1, 0), 0.42, 0.42, bevel=0.08, mb=self.leanto)
        self.beam((xp, -hy, zl), (xp, hy, zl), (1, 0, 0), 0.6, 0.6, bevel=0.12, mb=self.leanto)
        self.beam((xw, -hy, zh), (xw, hy, zh), (1, 0, 0), 0.55, 0.55, bevel=0.1, mb=self.leanto)
        # Chevrons en pente et couverture de 7 planches épaisses le long de la pente.
        slope_v = Vector((xp - xw, 0, zl - zh)).normalized()
        p_top = Vector((xw - 0.1, 0, zh + 0.22))
        p_bot = p_top + slope_v * ((abs(xp - xw) + 0.95) / abs(slope_v.x))
        for y in (-yp, 0.0, yp):
            self.beam(p_top + Vector((0, y, 0)), p_bot + Vector((0, y, 0)), (0, 1, 0), 0.44, 0.44, bevel=0.08, mb=self.leanto)
        n_planks = 7
        span_y = 2 * hy + 0.4
        pw = span_y / n_planks - 0.04
        nrm_r = Vector((-slope_v.z, 0, slope_v.x))
        for k in range(n_planks):
            y = -span_y / 2 + span_y * (k + 0.5) / n_planks
            off = nrm_r * 0.34 + Vector((0, y, self.jit(0.03)))
            self.beam(p_top + off, p_bot + off + slope_v * self.jit(0.12), (0, 1, 0), pw, 0.26,
                      band=1 + (k % 2), bevel=0.06, wobble=0.4, mb=self.leanto)
        # Colliders optionnels : 2 poteaux + dalle de toit (l'espace dessous reste praticable).
        for sy in (-1, 1):
            self.opt_collider_box("LeanTo", (xp, sy * yp, zl / 2), (0.75, 0.75, zl))
        mid_r = (p_top + p_bot) / 2 + nrm_r * 0.3
        r_len = (p_bot - p_top).length
        ang_r = math.atan2(zl - zh, xp - xw)
        rot_r = Matrix.Rotation(ang_r, 4, "Y")
        c, s = Vector(mid_r), Vector((r_len, span_y, 0.5)) / 2
        self.opt_colliders.setdefault("LeanTo", []).append(
            [c + (rot_r.to_3x3() @ Vector((sx * s.x, sy * s.y, sz * s.z))) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)])

    def build_awning(self):
        """Option « auvent_toile » (P03 #4) : toile beige tendue sur 2 poteaux fins contre le mur +X."""
        xw = self.W / 2 - 0.1
        xp = self.W / 2 + 4.4
        hy = 3.0
        zh, zl = 7.4, 5.4
        # Poutre murale, 2 poteaux fins, traverse avant et chevilles.
        self.beam((xw, -hy - 0.3, zh), (xw, hy + 0.3, zh), (1, 0, 0), 0.52, 0.55, bevel=0.1, mb=self.awning)
        for sy in (-1, 1):
            y = sy * (hy - 0.25)
            self.beam((xp + 0.15, y, -0.15), (xp, y, zl - 0.05), (1, 0, 0), 0.36, 0.36, bevel=0.08, mb=self.awning)
            self.beam((xp - 0.15, y, zl + 0.05), (xp + 0.42, y, zl + 0.05), (0, 1, 0), 0.2, 0.2, bevel=0.05, mb=self.awning)
        self.beam((xp, -hy - 0.25, zl), (xp, hy + 0.25, zl), (1, 0, 0), 0.38, 0.38, bevel=0.08, mb=self.awning)
        # Toile tendue et légèrement affaissée, épaissie (dessus, dessous, bords).
        hw_c = hy - 0.22

        def pt(s, t, off):
            x = xw + 0.15 + (xp + 0.05 - xw - 0.15) * t
            y = -hw_c + 2 * hw_c * s
            sag = 0.32 * math.sin(math.pi * t) * (0.45 + 0.55 * math.sin(math.pi * s))
            z = (zh + 0.2) + (zl + 0.15 - zh - 0.2) * t - sag + off
            return Vector((x, y, z))

        uvc = lambda s, t, p: (s * 0.9, t * 0.8)
        self.awning.grid(lambda s, t: pt(s, t, 0.045), 10, 8, 1, uvc, ref=lambda p: Vector((0.3, 0, 1)))
        self.awning.grid(lambda s, t: pt(s, t, -0.045), 10, 8, 1, uvc, ref=lambda p: Vector((-0.3, 0, -1)))
        for s_edge, ny in ((0.0, -1), (1.0, 1)):
            self.awning.grid(lambda u, v, se=s_edge: pt(se, u, -0.045 + 0.09 * v), 8, 1, 1,
                             lambda u, v, p: (u * 0.8, 0.05), smooth=False, ref=lambda p, ny=ny: Vector((0, ny, 0)))
        self.awning.grid(lambda u, v: pt(u, 1.0, -0.045 + 0.09 * v), 10, 1, 1,
                         lambda u, v, p: (u * 0.9, 0.05), smooth=False, ref=lambda p: Vector((1, 0, 0)))

    def build_porch(self):
        """Option « perron » (P03 #5) : palier en planches, 2 marches et 2 montants devant la porte (-Y)."""
        yf = -self.D / 2
        W_p, D_land = 6.0, 2.3
        z_land = 1.05
        y0, y1 = yf + 0.1, yf - D_land
        # Lambourdes sous le palier + 4 plots d'appui.
        for sx in (-1, 1):
            x = sx * (W_p / 2 - 0.45)
            self.beam((x, y0, z_land - 0.34), (x, y1 + 0.1, z_land - 0.34), (1, 0, 0), 0.5, 0.42, bevel=0.08, mb=self.porch)
            self.beam((x, y1 + 0.45, -0.15), (x, y1 + 0.45, z_land - 0.25), (1, 0, 0), 0.58, 0.58, bevel=0.1, mb=self.porch)
        self.beam((-W_p / 2 + 0.2, y1 + 0.35, z_land - 0.42), (W_p / 2 - 0.2, y1 + 0.35, z_land - 0.42), (0, 1, 0), 0.45, 0.45, bevel=0.08, mb=self.porch)
        # Planches du palier orientées d'avant en arrière.
        n_pl = 6
        pw = W_p / n_pl - 0.04
        for k in range(n_pl):
            x = -W_p / 2 + W_p * (k + 0.5) / n_pl
            self.beam((x, y0, z_land - 0.12), (x, y1 - 0.08 + self.jit(0.05), z_land - 0.12), (1, 0, 0), pw, 0.26,
                      band=1 + (k % 2), bevel=0.06, wobble=0.3, mb=self.porch)
        # 2 marches descendantes devant le palier (total 3 niveaux avec le palier).
        for step_idx, (ys, zs, ws) in enumerate(((y1 - 0.55, 0.70, 5.75), (y1 - 1.35, 0.35, 5.5))):
            self.beam((-ws / 2, ys, zs - 0.13), (ws / 2, ys, zs - 0.13), (0, 1, 0), 0.85, 0.26,
                      band=1 + (step_idx % 2), bevel=0.06, wobble=0.4, mb=self.porch)
            for sx in (-1, 1):
                self.beam((sx * (ws / 2 - 0.45), ys, -0.15), (sx * (ws / 2 - 0.45), ys, zs - 0.15), (1, 0, 0), 0.48, 0.48, bevel=0.08, mb=self.porch)
        # 2 montants verticaux encadrant la porte (comme sur Planche P03 #5).
        for sx in (-1, 1):
            x = sx * 2.85
            self.beam((x, yf - 0.35, z_land - 0.1), (x, yf - 0.35, 4.7), (1, 0, 0), 0.62, 0.62, bevel=0.12, mb=self.porch)
        # Collider en rampe douce pour monter à pied sur le perron.
        self.opt_colliders.setdefault("Porch", []).append([
            Vector((-W_p / 2, y0, 0.0)), Vector((W_p / 2, y0, 0.0)),
            Vector((-W_p / 2, y0, z_land)), Vector((W_p / 2, y0, z_land)),
            Vector((-W_p / 2, y1, z_land)), Vector((W_p / 2, y1, z_land)),
            Vector((-W_p / 2, y1 - 1.8, 0.0)), Vector((W_p / 2, y1 - 1.8, 0.0)),
        ])

    def build_cross_gable(self):
        """Option « pignon_croise » (P03 #6) : pignon perpendiculaire à colombage sur le pan -X."""
        hy = 3.85
        x_out = -self.W / 2 - 2.4
        z_eave = self.H + 0.3
        z_ridge = self.Ra - 0.8
        x_in = -0.25
        # Volume d'enduit du pignon (au-dessus de la poutre horizontale, jusqu'au toit principal).
        pw = hy - 0.25
        prof = [(-pw, self.H - 0.3), (pw, self.H - 0.3), (pw, z_eave - 0.15), (0.0, z_ridge - 0.35), (-pw, z_eave - 0.15)]
        self._extrude_x_profile(self.cross_gable, prof, x_out + 0.25, x_in, 1, world_uv("plaster"))
        # 2 poteaux avant descendant au sol, entrait horizontal, jambes de force et sablières latérales.
        xp = x_out + 0.35
        top = self.H - 0.4
        self.beam((xp, -hy - 0.35, top), (xp, hy + 0.35, top), (1, 0, 0), 0.78, 0.8, bevel=0.14, mb=self.cross_gable)
        for sy in (-1, 1):
            y = sy * pw
            self.beam((xp, y, -0.15), (xp, y, top - 0.25), (1, 0, 0), 0.78, 0.78, bevel=0.14, mb=self.cross_gable)
            self.beam((xp, y, top - 1.8), (xp, sy * (pw - 1.4), top - 0.15), (1, 0, 0), 0.46, 0.46, bevel=0.08, mb=self.cross_gable)
            self.beam((-self.W / 2 + 0.2, y, top), (xp, y, top), (0, 0, 1), 0.72, 0.72, bevel=0.12, mb=self.cross_gable)
            # Montants verticaux et chevrons obliques dans le triangle du pignon.
            s_stud = sy * 1.45
            z_top_stud = z_ridge - (z_ridge - z_eave) / hy * abs(s_stud) - 0.35
            self.beam((xp, s_stud, top), (xp, s_stud, z_top_stud), (1, 0, 0), 0.52, 0.52, bevel=0.1, mb=self.cross_gable)
        # Poinçon central.
        self.beam((xp, 0, top), (xp, 0, z_ridge - 0.35), (1, 0, 0), 0.56, 0.56, bevel=0.1, mb=self.cross_gable)
        # Petit toit à deux pans + tuiles.
        self._add_cross_roof(self.cross_gable, self.cross_gable_roof, x_in, x_out - GOV, hy, z_eave, z_ridge, 0.95, r_scale=0.9)
        # Colliders optionnels (calque Wall) : 2 poteaux + prisme du haut.
        for sy in (-1, 1):
            self.opt_collider_box("CrossGable", (xp, sy * pw, top / 2), (0.85, 0.85, top))
        xe_y = hy + 1.0
        self.opt_colliders.setdefault("CrossGable", []).append(
            [(xx, sy * xe_y, z_eave - 0.5) for xx in (x_out - GOV, -self.W / 2) for sy in (-1, 1)] +
            [(xx, 0, z_ridge + TILE_BASE + 0.8) for xx in (x_out - GOV, -self.W / 2)])

    def opt_collider_objects(self, house_prefix, coll):
        """Crée les colliders optionnels (<House>_<Opt>Col<n>)."""
        obs = []
        for opt_key, list_pts in self.opt_colliders.items():
            for i, pts in enumerate(list_pts):
                bm = bmesh.new()
                for p in pts:
                    bm.verts.new(p)
                bmesh.ops.convex_hull(bm, input=list(bm.verts))
                for v in [v for v in bm.verts if not v.link_faces]:
                    bm.verts.remove(v)
                me = bpy.data.meshes.new(f"{house_prefix}_{opt_key}Col{i}")
                bm.to_mesh(me)
                bm.free()
                ob = bpy.data.objects.new(me.name, me)
                coll.objects.link(ob)
                ob.display_type = "WIRE"
                ob.hide_render = True
                obs.append(ob)
        return obs

    def build(self, coll):
        self.build_plaster()
        if self.plinth:
            self.build_plinth()
        self.build_frame()
        self.build_door()
        self.build_gables()
        self.build_side_windows()
        self.build_roof_structure()
        self.build_tiles()
        self.build_chimney()
        self.build_dormer()
        self.build_leanto()
        self.build_awning()
        self.build_porch()
        self.build_cross_gable()
        self.build_colliders()
        k = self.key
        body = self.body.to_object(f"House_{k}_Body", coll)
        roof = self.roof.to_object(f"House_{k}_Roof", coll, weighted=False)
        chim = self.chim.to_object(f"House_{k}_Chimney", coll)
        dormer = self.dormer.to_object(f"House_{k}_Dormer", coll)
        dormer_roof = self.dormer_roof.to_object(f"House_{k}_DormerRoof", coll, weighted=False)
        leanto = self.leanto.to_object(f"House_{k}_LeanTo", coll)
        awning = self.awning.to_object(f"House_{k}_Awning", coll)
        porch = self.porch.to_object(f"House_{k}_Porch", coll)
        cg = self.cross_gable.to_object(f"House_{k}_CrossGable", coll)
        cg_roof = self.cross_gable_roof.to_object(f"House_{k}_CrossGableRoof", coll, weighted=False)
        cols = self.collider_objects(f"House_{k}", coll)
        opt_cols = self.opt_collider_objects(f"House_{k}", coll)
        return (body, roof, chim, dormer, dormer_roof, leanto, awning, porch, cg, cg_roof), cols + opt_cols


# ------------------------------------------------------------------ construction et export
def main(do_export=False, preview_roofs=("Red", "Blue", "Yellow")):
    coll = vl.reset_collection(COLL)
    report = {}
    offsets = {"S": -22.0, "M": 0.0, "L": 24.0}
    for i, (key, p) in enumerate(MODULES.items()):
        h = House(key, **p)
        meshes, cols = h.build(coll)
        body, roof, chim, dormer, dormer_roof, leanto, awning, porch, cg, cg_roof = meshes
        roof_key = "roof_" + preview_roofs[i % len(preview_roofs)]
        vl.assign(body, ["wood", "plaster", "stone"])
        vl.assign(roof, [roof_key])
        vl.assign(chim, ["stone", "wood"])
        vl.assign(dormer, ["wood", "plaster"])
        vl.assign(dormer_roof, [roof_key])
        vl.assign(leanto, ["wood"])
        vl.assign(awning, ["wood", "canvas"])
        vl.assign(porch, ["wood"])
        vl.assign(cg, ["wood", "plaster"])
        vl.assign(cg_roof, [roof_key])
        objs = list(meshes) + cols
        if do_export:
            vl.export_fbx(objs, os.path.join(OUT_DIR, f"SM_House_{key}.fbx"))
        for o in objs:
            o.location.x = offsets[key]
        bpy.context.view_layer.update()
        report[key] = {"tris": {o.name: vl.tri_count(o) for o in meshes},
                       "colliders": len(cols),
                       "top_z": round(max((o.matrix_world @ Vector(c)).z for o in meshes for c in o.bound_box), 2)}
    return report


if __name__ == "__main__" or "DO_EXPORT" in globals():
    result = main(globals().get("DO_EXPORT", False))
