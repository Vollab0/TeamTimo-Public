# Construit les 3 variantes de murs stylisés de TeamTimo dans Blender.
# Exécuter dans Blender, avec ArtSource/Murs/Mur_Stylized.blend ouvert :
#   exec(open(r"<dépôt>\ArtSource\Murs\build_walls.py", encoding="utf-8").read(), {"DO_EXPORT": True})
# Les textures viennent de gen_stone_textures.py et gen_ivy_atlas.py (Python de Blender, numpy) :
# les relancer dans ce dossier, puis copier les PNG dans Assets/Art/Textures/Environment/Walls/.
# Repère : mur le long de X (-25..25), épaisseur en Y, hauteur en Z (0..50), pivot en bas au centre
# (identique à l'ancien mur « Mur » pour que les meshes soient interchangeables dans Unity).
import bpy, bmesh, json, math, os, random
from mathutils import Vector, Matrix

DIR = os.path.dirname(bpy.data.filepath)  # dossier du .blend (= ArtSource/Murs) : textures + stone_layout.json
TEXDIR = os.path.normpath(os.path.join(DIR, "..", "..", "Assets", "Art", "Textures", "Environment", "Walls"))
TILE = 18.0          # taille monde (m) d'une répétition de la texture de pierre
T = 3.3              # demi-épaisseur du corps du mur
HALF = 25.0          # demi-longueur
BODY_TOP = 46.8      # haut du corps (sous le couronnement)
VIS = 19.0           # |x| visible (au-delà : caché par les piliers)
COLL = "Murs_Stylized"

layout = json.load(open(os.path.join(DIR, "stone_layout.json")))
GAPF = layout["gap"]

# ------------------------------------------------------------------ pierres de la texture
def stones_world(uoff, x0=-HALF, x1=HALF, z0=0.0, z1=50.0):
    """Rectangles (xa, xb, za, zb) des pierres peintes (joint compris) qui touchent la zone."""
    out = []
    for (fx, fy, fw, fh) in layout["stones"]:
        for i in range(-3, 5):
            for j in range(0, 4):
                u0 = fx + i; v0 = 1.0 - fy - fh + j
                xa = u0 * TILE - uoff; xb = xa + fw * TILE
                za = v0 * TILE; zb = za + fh * TILE
                if xb > x0 and xa < x1 and zb > z0 and za < z1:
                    out.append((xa, xb, za, zb))
    return out

def inner(r, m):
    xa, xb, za, zb = r
    g = GAPF * TILE + m
    return (xa + g, xb - g, za + g, zb - g)

# ------------------------------------------------------------------ constructeur de mesh
class MB:
    def __init__(self):
        self.v = []; self.f = []; self.mat = []; self.uv = {}  # uv: index face -> liste uv (ivy seulement)
    def vert(self, p):
        self.v.append(tuple(p)); return len(self.v) - 1
    def face(self, idx, mat=1, uvs=None):
        self.f.append(list(idx)); self.mat.append(mat)
        if uvs is not None: self.uv[len(self.f) - 1] = uvs
    def quad(self, a, b, c, d, mat=1):
        self.face([self.vert(a), self.vert(b), self.vert(c), self.vert(d)], mat)

    def prism_xz(self, outline, ya, yb):
        """Polygone (x,z) CCW vu depuis -Y, extrudé de ya (face avant, -Y) à yb (arrière)."""
        n = len(outline)
        fa = [self.vert((x, ya, z)) for (x, z) in outline]
        fb = [self.vert((x, yb, z)) for (x, z) in outline]
        self.face(fa, mat=2)          # avant : normale -Y si CCW vu de -Y (2 = corps, reçoit les booléens)
        self.face(fb[::-1], mat=2)
        for k in range(n):
            k2 = (k + 1) % n
            self.face([fa[k2], fa[k], fb[k], fb[k2]], mat=2)

    def block(self, cx, cy, cz, sx, sy, sz, rnd, jit=0.12, rot=(0, 0, 0), skip=()):
        """Bloc de pierre (boîte aux sommets légèrement déformés). skip: faces à omettre ('-y','+y',...)."""
        R = Matrix.Rotation(rot[0], 3, 'X') @ Matrix.Rotation(rot[1], 3, 'Y') @ Matrix.Rotation(rot[2], 3, 'Z')
        c = []
        for (ix, iy, iz) in [(-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1), (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)]:
            p = Vector((ix * sx / 2 + rnd.uniform(-jit, jit), iy * sy / 2 + rnd.uniform(-jit, jit), iz * sz / 2 + rnd.uniform(-jit, jit)))
            c.append(R @ p + Vector((cx, cy, cz)))
        F = {'-z': (0, 3, 2, 1), '+z': (4, 5, 6, 7), '-y': (0, 1, 5, 4), '+y': (2, 3, 7, 6), '-x': (3, 0, 4, 7), '+x': (1, 2, 6, 5)}
        for k, q in F.items():
            if k in skip: continue
            self.quad(*(c[i] for i in q))

    def tapered(self, xa, xb, za, zb, side, depth, bev, rnd):
        """Pierre en saillie sur la face (side=-1 avant, +1 arrière) : tronc de pyramide, 5 faces."""
        y0 = side * (T - 0.05); y1 = side * (T + depth)
        j = lambda: rnd.uniform(-0.08, 0.08)
        b = [(xa, y0, za), (xb, y0, za), (xb, y0, zb), (xa, y0, zb)]
        t = [(xa + bev + j(), y1 + j() * 0.5, za + bev + j()), (xb - bev + j(), y1 + j() * 0.5, za + bev + j()),
             (xb - bev + j(), y1 + j() * 0.5, zb - bev + j()), (xa + bev + j(), y1 + j() * 0.5, zb - bev + j())]
        B = [self.vert(p) for p in b]; Tt = [self.vert(p) for p in t]
        if side < 0:
            self.face([Tt[0], Tt[1], Tt[2], Tt[3]])
            for k in range(4):
                k2 = (k + 1) % 4
                self.face([B[k], B[k2], Tt[k2], Tt[k]])
        else:
            self.face([Tt[3], Tt[2], Tt[1], Tt[0]])
            for k in range(4):
                k2 = (k + 1) % 4
                self.face([B[k2], B[k], Tt[k], Tt[k2]])

    def card(self, pts, width, uv_rect, normal_side):
        """Carte de lierre le long d'une polyligne 3D (pts du haut vers le bas), largeur en X.
        uv_rect = (u0, v0, u1, v1) de la cellule de l'atlas ; v1 = haut de la carte."""
        u0, v0, u1, v1 = uv_rect
        L = [0.0]
        for a, b in zip(pts, pts[1:]): L.append(L[-1] + (Vector(b) - Vector(a)).length)
        tot = L[-1]
        rows = []
        for p, l in zip(pts, L):
            vv = v1 - (v1 - v0) * (l / tot)
            rows.append((self.vert((p[0] - width / 2, p[1], p[2])), self.vert((p[0] + width / 2, p[1], p[2])), vv))
        for (a0, a1, va), (b0, b1, vb) in zip(rows, rows[1:]):
            if normal_side < 0:   # visible depuis -Y
                self.face([b0, b1, a1, a0], mat=0, uvs=[(u0, vb), (u1, vb), (u1, va), (u0, va)])
            else:                 # visible depuis +Y (miroir en X)
                self.face([b1, b0, a0, a1], mat=0, uvs=[(u0, vb), (u1, vb), (u1, va), (u0, va)])

ATLAS = {"A": (0.0, 0.0, 0.25, 1.0), "B": (0.25, 0.0, 0.5, 1.0), "C": (0.5, 0.5, 1.0, 1.0), "D": (0.5, 0.0, 1.0, 0.5)}

# ------------------------------------------------------------------ éléments communs
def coping(mb, rnd, segments, z0=BODY_TOP - 0.15):
    """Couronnement : blocs de pierre sur les intervalles x donnés."""
    tops = []
    for (a, b) in segments:
        x = a
        while x < b - 0.6:
            w = min(rnd.uniform(4.0, 6.8), b - x)
            if b - (x + w) < 2.0: w = b - x
            h = rnd.uniform(2.3, 3.2)
            mb.block((x + x + w) / 2, rnd.uniform(-0.1, 0.1), z0 + h / 2, w - 0.18, 2 * T + 1.2 + rnd.uniform(-0.15, 0.2), h, rnd,
                     jit=0.15, rot=(rnd.uniform(-0.02, 0.02), rnd.uniform(-0.03, 0.03), rnd.uniform(-0.02, 0.02)))
            tops.append(((x + x + w) / 2, z0 + h, w))
            x += w
    return tops

def protrusions(mb, rnd, uoff, count, avoid, zmin=1.0, zmax=44.0):
    placed = []
    st = [r for r in stones_world(uoff) if r[0] > -VIS and r[1] < VIS and r[2] > zmin and r[3] < zmax
          and r[1] - r[0] > 2.0 and r[3] - r[2] > 1.0]
    rnd.shuffle(st)
    for side in (-1, 1):
        n = 0
        for r in st:
            if n >= count: break
            if any(not (r[1] < a[0] or r[0] > a[1] or r[3] < a[2] or r[2] > a[3]) and a[4] in (0, side) for a in avoid + placed):
                continue
            xa, xb, za, zb = inner(r, 0.02)
            mb.tapered(xa, xb, za, zb, side, rnd.uniform(0.3, 0.7), rnd.uniform(0.16, 0.26), rnd)
            placed.append((r[0], r[1], r[2], r[3], side)); n += 1
    return placed

def ivy_hanging(mb, rnd, x, ztop, length, side, over_top=True, coping_front=None):
    cell = ATLAS[rnd.choice("AB")]
    w = length / 3.2 * rnd.uniform(0.9, 1.1)
    yb = side * (T + 0.12)
    pts = []
    if over_top: pts.append((x, side * (T - 1.6), ztop + 0.08))
    if coping_front is not None:
        yc = side * (coping_front + 0.25)
        pts += [(x, yc, ztop + 0.05), (x, yc, BODY_TOP - 0.3), (x, yb, BODY_TOP - 1.2)]
        pts.append((x, yb, ztop - length))
    else:
        pts += [(x, side * (T + 0.08), ztop + 0.02), (x, yb, ztop - length)]
    mb.card(pts, w, cell, side)
    return (x - w / 2, x + w / 2, ztop - length, 60, side)

def ivy_clump(mb, rnd, x, zbase, size, side):
    cell = ATLAS[rnd.choice("CD")]
    y = side * (T + 0.15)
    z0 = zbase - 0.26 * size
    mb.card([(x, y, z0 + size), (x, y, z0)], size, cell, side)
    return (x - size / 2, x + size / 2, z0, z0 + size, side)

# ------------------------------------------------------------------ variantes
def variant_intact(name, seed, uoff):
    rnd = random.Random(seed); mb = MB()
    mb.prism_xz([(-HALF, 0), (HALF, 0), (HALF, BODY_TOP), (-HALF, BODY_TOP)], -T, T)
    tops = coping(mb, rnd, [(-HALF, HALF)])
    avoid = []
    # lierre : brins pendants depuis le couronnement + touffes au pied, sur les 2 faces
    for side in (-1, 1):
        for _ in range(rnd.randint(2, 3)):
            cx, ztop, w = rnd.choice([t for t in tops if abs(t[0]) < VIS - 2])
            avoid.append(ivy_hanging(mb, rnd, cx + rnd.uniform(-1.5, 1.5), ztop, rnd.uniform(18, 30), side, coping_front=T + 0.6))
        for _ in range(rnd.randint(1, 2)):
            avoid.append(ivy_clump(mb, rnd, rnd.uniform(-VIS + 4, VIS - 4), 0.0, rnd.uniform(6, 9), side))
    cutters = []
    # pierres manquantes (renfoncements) ponctuelles
    st = [r for r in stones_world(uoff) if -VIS < r[0] and r[1] < VIS and 6 < r[2] and r[3] < 42]
    rnd.shuffle(st)
    for side in (-1, 1):
        for r in st[:4] if side < 0 else st[4:8]:
            cutters.append(("box", r, side, 1.2))
            avoid.append((r[0], r[1], r[2], r[3], 0))
    protrusions(mb, rnd, uoff, 14, avoid)
    return mb, cutters

def variant_broken(name, seed, uoff):
    rnd = random.Random(seed); mb = MB()
    xb = rnd.uniform(-6, 6); wb = rnd.uniform(11, 15); depth = rnd.uniform(15, 19)
    def zcut(x):
        t = max(0.0, 1 - abs(x - xb) / wb)
        return BODY_TOP - depth * (t ** 0.8) + rnd.uniform(-1.2, 1.2) * t
    st = stones_world(uoff, z1=BODY_TOP + 2)
    removed = [r for r in st if (r[2] + r[3]) / 2 > zcut((r[0] + r[1]) / 2) and r[3] > BODY_TOP - depth - 3]
    xs = sorted(set([-HALF, HALF] + [min(max(r[0], -HALF), HALF) for r in st] + [min(max(r[1], -HALF), HALF) for r in st]))
    cols = []
    for a, b in zip(xs, xs[1:]):
        if b - a < 1e-4: continue
        m = (a + b) / 2
        bottoms = [r[2] for r in removed if r[0] <= m <= r[1]]
        top = min(bottoms) if bottoms else BODY_TOP
        top = min(top, BODY_TOP)
        if cols and abs(cols[-1][2] - top) < 1e-4: cols[-1] = (cols[-1][0], b, top)
        else: cols.append((a, b, top))
    outline = [(-HALF, 0.0), (HALF, 0.0)]
    for (a, b, top) in reversed(cols):
        outline += [(b, top), (a, top)]
    # nettoyage des points consécutifs identiques
    clean = []
    for p in outline:
        if not clean or (abs(clean[-1][0] - p[0]) > 1e-4 or abs(clean[-1][1] - p[1]) > 1e-4): clean.append(p)
    mb.prism_xz(clean, -T, T)
    intact = [(a, b) for (a, b, top) in cols if top >= BODY_TOP - 1e-3]
    # fusion des intervalles intacts contigus
    merged = []
    for a, b in intact:
        if merged and abs(merged[-1][1] - a) < 1e-3: merged[-1] = (merged[-1][0], b)
        else: merged.append((a, b))
    tops = coping(mb, rnd, [m for m in merged if m[1] - m[0] > 1.5])
    # éboulis sur les paliers cassés
    ledges = [(a, b, top) for (a, b, top) in cols if top < BODY_TOP - 1e-3 and b - a > 1.2]
    for (a, b, top) in rnd.sample(ledges, min(len(ledges), 5)):
        s = rnd.uniform(1.0, 2.2)
        mb.block(rnd.uniform(a + s / 2, max(a + s / 2, b - s / 2)), rnd.uniform(-T + s / 2, T - s / 2), top + s * 0.38, s * rnd.uniform(1, 1.5), s, s * 0.8, rnd,
                 jit=0.25, rot=(rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), rnd.uniform(-0.6, 0.6)), skip=('-z',))
    avoid = []
    for side in (-1, 1):
        lowest = sorted(ledges, key=lambda c: c[2])[:4]
        for (a, b, top) in rnd.sample(lowest, min(2, len(lowest))):
            avoid.append(ivy_hanging(mb, rnd, (a + b) / 2, top, rnd.uniform(14, 22), side))
        if tops:
            cx, ztop, w = rnd.choice([t for t in tops if abs(t[0]) < VIS - 1] or tops)
            avoid.append(ivy_hanging(mb, rnd, cx, ztop, rnd.uniform(18, 28), side, coping_front=T + 0.6))
        (a, b, top) = rnd.choice(ledges)
        avoid.append(ivy_clump(mb, rnd, (a + b) / 2, top, rnd.uniform(4.5, 6.5), side))
        avoid.append(ivy_clump(mb, rnd, rnd.uniform(-VIS + 4, VIS - 4), 0.0, rnd.uniform(6, 9), side))
    cutters = []
    st2 = [r for r in stones_world(uoff) if -VIS < r[0] and r[1] < VIS and 5 < r[2] and r[3] < BODY_TOP - depth - 2]
    rnd.shuffle(st2)
    for side in (-1, 1):
        for r in (st2[:2] if side < 0 else st2[2:4]):
            cutters.append(("box", r, side, 0.9)); avoid.append((r[0], r[1], r[2], r[3], 0))
    protrusions(mb, rnd, uoff, 12, avoid, zmax=BODY_TOP - depth - 1)
    return mb, cutters

def variant_holes(name, seed, uoff):
    rnd = random.Random(seed); mb = MB()
    mb.prism_xz([(-HALF, 0), (HALF, 0), (HALF, BODY_TOP), (-HALF, BODY_TOP)], -T, T)
    tops = coping(mb, rnd, [(-HALF, HALF)])
    st = stones_world(uoff)
    cutters = []; avoid = []
    centers = [(-9.0 + rnd.uniform(-3, 3), rnd.uniform(28, 37)), (8.0 + rnd.uniform(-3, 3), rnd.uniform(14, 24))]
    if rnd.random() < 0.7: centers.append((rnd.uniform(-4, 4), rnd.uniform(38, 42)))
    for (hx, hz) in centers:
        rad = rnd.uniform(3.0, 4.3)
        cl = [r for r in st if math.hypot(((r[0] + r[1]) / 2 - hx) * 0.8, (r[2] + r[3]) / 2 - hz) < rad and abs(r[0]) < VIS and abs(r[1]) < VIS]
        for r in cl:
            cutters.append(("through", r, 0, 0, len(centers) and centers.index((hx, hz))))
            avoid.append((r[0] - 1.5, r[1] + 1.5, r[2] - 1.5, r[3] + 1.5, 0))
    # éboulis en appui dans le bas de chaque trou
    for (hx, hz) in centers:
        cl = [c for c in cutters if c[0] == "through" and c[4] == centers.index((hx, hz)) and math.hypot(((c[1][0] + c[1][1]) / 2 - hx) * 0.8, (c[1][2] + c[1][3]) / 2 - hz) < 4.4]
        if not cl: continue
        r = min(cl, key=lambda c: c[1][2])[1]
        s = rnd.uniform(0.9, 1.5)
        mb.block((r[0] + r[1]) / 2, rnd.uniform(-1.5, 1.5), r[2] + s * 0.35, s * 1.3, s, s * 0.8, rnd, jit=0.2,
                 rot=(rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), rnd.uniform(-0.6, 0.6)), skip=('-z',))
    for side in (-1, 1):
        for _ in range(2):
            cx, ztop, w = rnd.choice([t for t in tops if abs(t[0]) < VIS - 2])
            a = ivy_hanging(mb, rnd, cx + rnd.uniform(-1.0, 1.0), ztop, rnd.uniform(16, 26), side, coping_front=T + 0.6)
            avoid.append(a)
        for _ in range(rnd.randint(1, 2)):
            avoid.append(ivy_clump(mb, rnd, rnd.uniform(-VIS + 4, VIS - 4), 0.0, rnd.uniform(6, 9), side))
    protrusions(mb, rnd, uoff, 12, avoid)
    return mb, cutters

# ------------------------------------------------------------------ assemblage Blender
def get_mats():
    def img(name):
        p = os.path.join(TEXDIR, name)
        im = bpy.data.images.get(name) or bpy.data.images.load(p, check_existing=True)
        im.reload(); return im
    stone = bpy.data.materials.get("M_WallStone") or bpy.data.materials.new("M_WallStone")
    stone.use_nodes = True
    nt = stone.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); bs = nt.nodes.new("ShaderNodeBsdfPrincipled")
    ta = nt.nodes.new("ShaderNodeTexImage"); ta.image = img("T_WallStone_Albedo.png")
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = img("T_WallStone_Normal.png"); tn.image.colorspace_settings.name = "Non-Color"
    nm = nt.nodes.new("ShaderNodeNormalMap")
    tao = nt.nodes.new("ShaderNodeTexImage"); tao.image = img("T_WallStone_AO.png"); tao.image.colorspace_settings.name = "Non-Color"
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'; mix.inputs[0].default_value = 1.0
    nt.links.new(ta.outputs[0], mix.inputs[6]); nt.links.new(tao.outputs[0], mix.inputs[7])
    nt.links.new(mix.outputs[2], bs.inputs["Base Color"])
    nt.links.new(tn.outputs[0], nm.inputs["Color"]); nt.links.new(nm.outputs[0], bs.inputs["Normal"])
    bs.inputs["Roughness"].default_value = 0.88
    nt.links.new(bs.outputs[0], out.inputs[0])
    ivy = bpy.data.materials.get("M_WallIvy") or bpy.data.materials.new("M_WallIvy")
    ivy.use_nodes = True
    nt = ivy.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); bs = nt.nodes.new("ShaderNodeBsdfPrincipled")
    ti = nt.nodes.new("ShaderNodeTexImage"); ti.image = img("T_WallIvy_Atlas.png")
    ti.image.alpha_mode = 'STRAIGHT'
    nt.links.new(ti.outputs[0], bs.inputs["Base Color"]); nt.links.new(ti.outputs[1], bs.inputs["Alpha"])
    bs.inputs["Roughness"].default_value = 0.6
    nt.links.new(bs.outputs[0], out.inputs[0])
    try: ivy.surface_render_method = 'DITHERED'
    except Exception: pass
    ivy.use_backface_culling = False
    return ivy, stone

def apply_cutters(obj, cutters):
    """Booléens : un cutter par pierre manquante, et un seul cutter (boîtes jointes) par trou traversant."""
    groups = {}
    for c in cutters:
        key = ("through", c[4]) if c[0] == "through" else ("single", len(groups))
        groups.setdefault(key, []).append(c)
    for key, cs in groups.items():
        me = bpy.data.meshes.new("cut"); bm = bmesh.new()
        for c in cs:
            kind, r, side, depth = c[:4]
            xa, xb, za, zb = r
            if kind == "through":
                e = 0.03  # léger recouvrement : pas de cloison de joint entre deux pierres retirées
                xa -= e; xb += e; za -= e; zb += e
                ya, yb = -T - 2, T + 2
            else:
                g = GAPF * TILE * 0.5
                xa += g; xb -= g; za += g; zb -= g
                ya, yb = (-T - 2, -T + depth) if side < 0 else (T - depth, T + 2)
            geom = bmesh.ops.create_cube(bm, size=1.0)["verts"]
            bmesh.ops.scale(bm, vec=(xb - xa, yb - ya, zb - za), verts=geom)
            bmesh.ops.translate(bm, vec=((xa + xb) / 2, (ya + yb) / 2, (za + zb) / 2), verts=geom)
        bm.to_mesh(me); bm.free()
        cut = bpy.data.objects.new("cut", me)
        bpy.context.scene.collection.objects.link(cut)
        mod = obj.modifiers.new("cut", 'BOOLEAN'); mod.operation = 'DIFFERENCE'; mod.solver = 'EXACT'; mod.object = cut
        mod.use_self = True
        with bpy.context.temp_override(object=obj, active_object=obj):
            bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.data.objects.remove(cut); bpy.data.meshes.remove(me)
    # fusion des faces coplanaires créées par les booléens
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(0.5), verts=bm.verts[:], edges=bm.edges[:])
    bm.to_mesh(obj.data); bm.free()

def build(name, fn, seed, uoff, xoff, cut_fn=None):
    """cut_fn(obj, cutters) : booléens sur le corps (par défaut apply_cutters, propre aux murs)."""
    mb, cutters = fn(name, seed, uoff)
    old = bpy.data.objects.get(name)
    if old: bpy.data.objects.remove(old)
    me = bpy.data.meshes.get(name)
    if me: bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new(name)
    # les cartes de lierre sont séparées pour ne pas être touchées par les booléens
    body_f = [i for i, m in enumerate(mb.mat) if m == 2]
    add_f = [i for i, m in enumerate(mb.mat) if m != 2]
    me.from_pydata(mb.v, [], [mb.f[i] for i in body_f])
    me.validate(); me.update()
    obj = bpy.data.objects.new(name, me)
    coll = bpy.data.collections.get(COLL) or bpy.data.collections.new(COLL)
    if coll.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(coll)
    coll.objects.link(obj)
    ivy, stone = get_mats()
    me.materials.append(ivy); me.materials.append(stone)
    for p in me.polygons: p.material_index = 1
    # fusion des sommets doublons
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(me); bm.free()
    if cutters: (cut_fn or apply_cutters)(obj, cutters)
    for p in me.polygons: p.material_index = 1
    # ajout des cartes de lierre et des blocs
    bm = bmesh.new(); bm.from_mesh(me)
    uvl = bm.loops.layers.uv.get("UVMap") or bm.loops.layers.uv.new("UVMap")
    vmap = {}
    fixed = set()   # faces aux UV imposées (lierre, pierres d'angle) : pas de projection boîte
    for i in add_f:
        vs = []
        for vi in mb.f[i]:
            if vi not in vmap: vmap[vi] = bm.verts.new(mb.v[vi])
            vs.append(vmap[vi])
        f = bm.faces.new(vs); f.material_index = mb.mat[i]
        if i in mb.uv:
            for loop, uv in zip(f.loops, mb.uv[i]): loop[uvl].uv = uv
            fixed.add(f)
    bm.normal_update()
    # UV par projection boîte (pierre) en coordonnées monde / TILE
    for f in bm.faces:
        if f.material_index != 1 or f in fixed: continue
        n = f.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop in f.loops:
            p = loop.vert.co
            if ay >= ax and ay >= az:
                u, v = (p.x + uoff) / TILE, p.z / TILE
            elif ax >= az:
                u, v = (p.y + uoff * 0.37) / TILE, p.z / TILE
            else:
                u, v = (p.x + uoff) / TILE, (p.y + 0.5 * TILE) / TILE
            loop[uvl].uv = (u, v)
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='EAR_CLIP')
    # lierre d'abord : dans Unity sous-mesh 0 = lierre, 1 = pierre (le matériau Outline en surplus s'applique au dernier)
    bm.faces.sort(key=lambda f: f.material_index)
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = False
    obj.location.x = xoff
    return obj, len(me.polygons)

def build_all():
    res = {}
    for name, fn, seed, uoff, xoff in (("Wall_A_Intact", variant_intact, 11, 0.0, 0),
                                       ("Wall_B_Broken", variant_broken, 23, 7.3, 60),
                                       ("Wall_C_Holes", variant_holes, 37, 12.9, 120)):
        o, tris = build(name, fn, seed, uoff, xoff)
        res[name] = tris
    return res

EXPORT = os.path.normpath(os.path.join(DIR, "..", "..", "Assets", "Art", "Models", "Environment", "Walls_Stylized.fbx"))

def export_fbx():
    names = ("Wall_A_Intact", "Wall_B_Broken", "Wall_C_Holes")
    saved = {n: bpy.data.objects[n].location.x for n in names}
    for n in names: bpy.data.objects[n].location.x = 0
    bpy.ops.object.select_all(action='DESELECT')
    for n in names: bpy.data.objects[n].select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects[names[0]]
    # mêmes réglages que l'ancien Wall.fbx (dans Unity : rotation -90 X, échelle 100)
    bpy.ops.export_scene.fbx(filepath=EXPORT, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_NONE', mesh_smooth_type='FACE', add_leaf_bones=False,
                             bake_anim=False, axis_forward='-Z', axis_up='Y')
    for n in names: bpy.data.objects[n].location.x = saved[n]

if not globals().get("LIB_ONLY"):   # build_pillars.py charge ce fichier comme bibliothèque
    BUILD_RESULT = build_all()
    if globals().get("DO_EXPORT"): export_fbx()
