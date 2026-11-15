# Construit le mur à arche des 4 portes du Lobby (TeamTimo) dans Blender.
# Exécuter dans Blender, avec ArtSource/Murs/Mur_Stylized.blend ouvert :
#   exec(open(r"<dépôt>\ArtSource\Murs\build_gate.py", encoding="utf-8").read(), {"DO_EXPORT": True})
# Réutilise build_walls.py (chargé en bibliothèque) : constructeur MB, pierres de la texture, matériaux.
# Textures : T_WallStone_* (murs) + T_GateCarving_* (gen_gate_textures.py : frise de grecques et panneaux sculptés).
# Repère identique au mur (Wall.fbx) : le long de X (-25..25), épaisseur en Y, hauteur en Z, pivot en bas au centre.
# Sous-meshes dans Unity : 0 = lierre, 1 = pierre sculptée, 2 = pierre (le matériau Outline en surplus s'applique au dernier).
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix

_here = os.path.dirname(bpy.data.filepath)
globals()["LIB_ONLY"] = True
exec(open(os.path.join(_here, "build_walls.py"), encoding="utf-8").read(), globals())
COLL = "Arches_Stylized"
NAME = "Gate_Arch"

TG = 4.6             # demi-épaisseur (plus épais que le mur : 3,3)
R = 13.0             # rayon intérieur de l'arche (passage de 26 m)
SPRING = 21.0        # hauteur de la naissance de l'arche (clé de voûte à 34 m)
BAND = 4.2           # largeur de l'archivolte sculptée
BAND_OUT = 0.6       # saillie de l'archivolte
PLINTH = 3.4         # haut des socles de l'archivolte
BODY = 44.0          # haut du corps (frise au-dessus)
FRIEZE = (44.0, 48.2)
CORNICE = 50.2
PED_W, PED_TOP = 19.5, 60.5   # fronton : demi-base et sommet
ARC_SEG = 14
CARVE = 3            # code matériau interne de la pierre sculptée (0 lierre, 1 pierre, 2 corps)
BAND_LEN = 16.8      # longueur (m) d'une répétition de la frise (64 unités de 0,2625 m)
STONES = [(fx, fy, fw, fh) for (fx, fy, fw, fh) in layout["stones"]]
# texture T_GateCarving (2048 x 1536) : frise en haut (v 2/3..1), panneaux décoratifs au milieu, roses des vents en bas
V_BAND = 2.0 / 3.0
def panel_uv(i):   # 0 spirale, 1 rosace, 2 losange, 3 quadrilobe
    return (i * 0.25, 1.0 / 3.0, i * 0.25 + 0.25, 2.0 / 3.0)
def compass_uv(letter):   # rose des vents marquée N, E, S ou W
    i = "NESW".index(letter)
    return (i * 0.25, 0.0, i * 0.25 + 0.25, 1.0 / 3.0)
LETTERS = "NESW"   # une variante de l'arche par point cardinal (seule la rose du fronton change)

def oface(mb, pts, want, mat=1, uvs=None):
    """Face orientée : retourne l'ordre des sommets si sa normale (Newell) ne va pas vers `want`."""
    n = Vector((0, 0, 0))
    for a, b in zip(pts, pts[1:] + pts[:1]):
        n.x += (a[1] - b[1]) * (a[2] + b[2]); n.y += (a[2] - b[2]) * (a[0] + b[0]); n.z += (a[0] - b[0]) * (a[1] + b[1])
    if n.dot(Vector(want)) < 0:
        pts = pts[::-1]
        if uvs: uvs = uvs[::-1]
    mb.face([mb.vert(p) for p in pts], mat, uvs)

def stone_uv(wf, hf, rnd):
    target = math.log(wf / hf)
    cands = sorted(STONES, key=lambda s: abs(math.log(s[2] / s[3]) - target))[:6]
    fx, fy, fw, fh = rnd.choice(cands)
    g = GAPF * 0.4
    return (fx + g, 1.0 - fy - fh + g, fx + fw - g, 1.0 - fy - g)

def rect_uv(r):
    u0, v0, u1, v1 = r
    return [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]

def front_quad(mb, side, x0, x1, z0, z1, y, mat, uv=None):
    """Quad vertical face à -Y (side -1) ou +Y (side +1) ; uv vue de face (gauche-bas -> droite-haut)."""
    if side < 0: pts = [(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)]
    else:        pts = [(x1, y, z0), (x0, y, z0), (x0, y, z1), (x1, y, z1)]
    oface(mb, pts, (0, side, 0), mat, rect_uv(uv) if uv else None)

def slab(mb, side, x0, x1, z0, z1, y0, y1, mat_front=1, uv=None, bottom=True):
    """Pièce en saillie sur la face `side`, de y0 (contre le mur) à y1 (devant) : face avant + 4 côtés."""
    front_quad(mb, side, x0, x1, z0, z1, y1, mat_front, uv)
    oface(mb, [(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], (0, 0, 1))
    if bottom: oface(mb, [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], (0, 0, -1))
    oface(mb, [(x0, y0, z0), (x0, y1, z0), (x0, y1, z1), (x0, y0, z1)], (-1, 0, 0))
    oface(mb, [(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)], (1, 0, 0))

def sblock(mb, rnd, cx, cy, cz, sx, sy, sz, jit=0.06, rot_y=0.0, skip=()):
    """Bloc de pierre dont les faces avant et arrière (±Y) montrent chacune UNE pierre peinte
    (sinon la projection boîte le fond dans le mur qui est derrière)."""
    R = Matrix.Rotation(rot_y, 3, 'Y')
    c = []
    for (ix, iy, iz) in [(-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1), (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)]:
        p = Vector((ix * sx / 2 + rnd.uniform(-jit, jit), iy * sy / 2 + rnd.uniform(-jit, jit), iz * sz / 2 + rnd.uniform(-jit, jit)))
        c.append(tuple(R @ p + Vector((cx, cy, cz))))
    F = {'-z': (0, 3, 2, 1), '+z': (4, 5, 6, 7), '-y': (0, 1, 5, 4), '+y': (2, 3, 7, 6), '-x': (3, 0, 4, 7), '+x': (1, 2, 6, 5)}
    for k, q in F.items():
        if k in skip: continue
        uvs = rect_uv(stone_uv(sx, sz, rnd)) if k in ('-y', '+y') else None
        mb.face([mb.vert(c[i]) for i in q], 1, uvs)

# ------------------------------------------------------------------ corps
def arch_pts(r, n=ARC_SEG):
    return [(-r * math.cos(math.pi * k / n), SPRING + r * math.sin(math.pi * k / n)) for k in range(n + 1)]

def body(mb, rnd):
    arc = arch_pts(R)
    outline = [(-HALF, 0.0), (-R, 0.0)] + arc + [(R, 0.0), (HALF, 0.0), (HALF, BODY), (-HALF, BODY)]
    fa = [(x, -TG, z) for (x, z) in outline]; fb = [(x, TG, z) for (x, z) in outline]
    oface(mb, fa, (0, -1, 0), mat=2); oface(mb, fb, (0, 1, 0), mat=2)
    # piédroits, dessus, extrémités : projection boîte (corps)
    n = len(outline)
    for k in range(n):
        a, b = outline[k], outline[(k + 1) % n]
        if a[1] == 0.0 and b[1] == 0.0: continue                  # dessous : jamais vu
        if 2 <= k < 2 + ARC_SEG: continue                          # intrados : traité à part
        mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        want = (b[1] - a[1], 0, -(b[0] - a[0]))                     # normale extérieure (contour CCW)
        oface(mb, [(a[0], -TG, a[1]), (b[0], -TG, b[1]), (b[0], TG, b[1]), (a[0], TG, a[1])], want, mat=2)
    # intrados : chaque segment coupé en 3 pierres dans l'épaisseur (joints décalés d'un rang à l'autre)
    for k in range(ARC_SEG):
        a, b = arc[k], arc[k + 1]
        want = (b[1] - a[1], 0, -(b[0] - a[0]))                     # vers le centre de l'arche (côté passage)
        cuts = [-TG, -TG + 2 * TG / 3 + (0.9 if k % 2 else -0.9), TG - 2 * TG / 3 + (0.9 if k % 2 else -0.9), TG]
        cuts = sorted(cuts)
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        for y0, y1 in zip(cuts, cuts[1:]):
            uv = stone_uv(y1 - y0, L, rnd)
            oface(mb, [(a[0], y0, a[1]), (a[0], y1, a[1]), (b[0], y1, b[1]), (b[0], y0, b[1])], want, mat=1,
                  uvs=[(uv[0], uv[1]), (uv[2], uv[1]), (uv[2], uv[3]), (uv[0], uv[3])])

# ------------------------------------------------------------------ archivolte sculptée (socle -> arc -> socle)
def band_path():
    rc = R + BAND / 2
    pts = [((-rc, PLINTH), (-1, 0))]
    for k in range(ARC_SEG + 1):
        t = math.pi * k / ARC_SEG
        d = (-math.cos(t), math.sin(t))
        pts.append(((rc * d[0], SPRING + rc * d[1]), d))
    pts.append(((rc, PLINTH), (1, 0)))
    return pts

def archivolt(mb, side):
    path = band_path()
    y0, y1 = side * TG, side * (TG + BAND_OUT)
    s = 0.0; prev = None
    rows = []
    for (c, d) in path:
        if prev: s += math.hypot(c[0] - prev[0], c[1] - prev[1])
        prev = c
        inn = (c[0] - d[0] * BAND / 2, c[1] - d[1] * BAND / 2); out = (c[0] + d[0] * BAND / 2, c[1] + d[1] * BAND / 2)
        rows.append((inn, out, s / BAND_LEN))
    for (i0, o0, u0), (i1, o1, u1) in zip(rows, rows[1:]):
        if abs(u1 - u0) < 1e-6: continue
        # face sculptée : bas de la frise (v 2/3) côté passage, haut (v 1) côté extérieur
        oface(mb, [(i0[0], y1, i0[1]), (i1[0], y1, i1[1]), (o1[0], y1, o1[1]), (o0[0], y1, o0[1])], (0, side, 0), CARVE,
              uvs=[(u0, V_BAND), (u1, V_BAND), (u1, 1.0), (u0, 1.0)])
        # tranches intérieure et extérieure (pierre)
        for (p, q, sgn) in ((i0, i1, -1), (o0, o1, 1)):
            mx, mz = (p[0] + q[0]) / 2, (p[1] + q[1]) / 2
            cx, cz = 0.0, (SPRING if mz > SPRING else mz)
            want = (sgn * (mx - cx), 0, sgn * (mz - cz)) if mz > SPRING + 0.01 else (sgn * (1 if mx > 0 else -1), 0, 0)
            oface(mb, [(p[0], y0, p[1]), (q[0], y0, q[1]), (q[0], y1, q[1]), (p[0], y1, p[1])], want, 1)

# ------------------------------------------------------------------ éléments
def plinths(mb, rnd, side):
    rc = R + BAND / 2
    for sx in (-1, 1):
        x0, x1 = sx * rc - 2.7, sx * rc + 2.7
        slab(mb, side, x0, x1, 0.0, PLINTH, side * TG, side * (TG + 0.95), 1, stone_uv(5.4, PLINTH, rnd), bottom=False)

def keystone(mb, rnd, side):
    zc = SPRING + R + BAND / 2
    slab(mb, side, -2.0, 2.0, zc - 3.4, zc + 3.6, side * TG, side * (TG + BAND_OUT + 0.8), 1, stone_uv(4.0, 7.0, rnd))

def panels(mb, side):
    # écoinçons (de part et d'autre de l'arche) : spirale / losange, quadrilobe selon la face
    picks = (0, 2) if side < 0 else (3, 0)
    for sx, pi in zip((-1, 1), picks):
        cx, cz, h = sx * 14.2, 38.4, 2.6
        slab(mb, side, cx - h, cx + h, cz - h, cz + h, side * TG, side * (TG + 0.3), CARVE, panel_uv(pi))

def frieze(mb, side):
    z0, z1 = FRIEZE
    y = side * (TG + 0.35)
    # face sculptée découpée en morceaux d'une répétition (u continu)
    xs = [-HALF + k * (2 * HALF) / 3 for k in range(4)]
    for a, b in zip(xs, xs[1:]):
        ua, ub = (a + HALF) / BAND_LEN, (b + HALF) / BAND_LEN
        if side > 0: ua, ub = (HALF - a) / BAND_LEN, (HALF - b) / BAND_LEN
        pts = [(a, y, z0), (b, y, z0), (b, y, z1), (a, y, z1)]
        oface(mb, pts, (0, side, 0), CARVE, uvs=[(ua, V_BAND), (ub, V_BAND), (ub, 1.0), (ua, 1.0)])
    oface(mb, [(-HALF, side * TG, z0), (HALF, side * TG, z0), (HALF, y, z0), (-HALF, y, z0)], (0, 0, -1))

def cornice(mb, rnd):
    xe = 19.7
    x = -xe
    while x < xe - 0.5:
        w = min(rnd.uniform(4.2, 6.4), xe - x)
        if xe - (x + w) < 2.0: w = xe - x
        for (za, zb, yd) in ((FRIEZE[1], 49.0, 0.8), (49.0, CORNICE, 1.5)):
            sblock(mb, rnd, x + w / 2, rnd.uniform(-0.05, 0.05), (za + zb) / 2, w - 0.14, 2 * (TG + yd), zb - za, jit=0.06)
        x += w
    # le corps continue derrière la corniche jusqu'aux piliers
    for sx in (-1, 1):
        mb.block(sx * (xe + HALF) / 2, 0, (FRIEZE[1] + CORNICE) / 2, HALF - xe, 2 * TG, CORNICE - FRIEZE[1], rnd, jit=0.0, skip=('-z',))

def pediment(mb, rnd, letter):
    t = TG - 0.3
    tri = [(-PED_W, CORNICE), (PED_W, CORNICE), (0.0, PED_TOP)]
    oface(mb, [(x, -t, z) for (x, z) in tri], (0, -1, 0), mat=2)
    oface(mb, [(x, t, z) for (x, z) in tri], (0, 1, 0), mat=2)
    for a, b in ((tri[1], tri[2]), (tri[2], tri[0])):
        oface(mb, [(a[0], -t, a[1]), (b[0], -t, b[1]), (b[0], t, b[1]), (a[0], t, a[1])], (b[1] - a[1], 0, -(b[0] - a[0])), mat=2)
    # rampants : blocs le long des deux pentes
    ang = math.atan2(PED_TOP - CORNICE, PED_W)
    for sx in (-1, 1):
        L = math.hypot(PED_W + 1.2, PED_TOP - CORNICE + 0.6)
        n = 5
        for k in range(n):
            f = (k + 0.5) / n
            px = sx * (PED_W + 1.2) * (1 - f); pz = CORNICE - 0.6 + (PED_TOP - CORNICE + 0.6) * f
            nx, nz = sx * math.sin(ang), math.cos(ang)            # normale de la pente (vers l'extérieur-haut)
            sblock(mb, rnd, px + nx * 0.85, rnd.uniform(-0.05, 0.05), pz + nz * 0.85, L / n - 0.12, 2 * (TG + 0.7), 2.1,
                   jit=0.05, rot_y=sx * ang)
    # bloc de faîte
    sblock(mb, rnd, 0, 0, PED_TOP + 1.0, 3.0, 2 * (TG + 0.5), 2.6, jit=0.08)
    # rose des vents dans le tympan : lettre du point cardinal de la porte (même lettre sur les deux faces)
    for side in (-1, 1):
        slab(mb, side, -3.6, 3.6, 50.5, 57.7, side * t, side * (t + 0.35), CARVE, compass_uv(letter))

# ------------------------------------------------------------------ lierre
def ivy(mb, rnd, side):
    front = TG + 1.5
    for x, length in ((-17.0, rnd.uniform(22, 30)), (-6.5, rnd.uniform(9, 13)), (8.5, rnd.uniform(12, 18)), (16.0, rnd.uniform(26, 36))):
        x += rnd.uniform(-0.8, 0.8)
        w = rnd.uniform(4.0, 5.2)
        yb = side * (TG + BAND_OUT + 0.25)
        pts = [(x, side * (TG - 0.5), CORNICE + 0.06), (x, side * (front + 0.25), CORNICE + 0.04), (x, side * (front + 0.25), FRIEZE[1] - 0.2),
               (x, yb, FRIEZE[0] - 0.6), (x, yb, CORNICE - length)]
        mb.card(pts, w, ATLAS[rnd.choice("AB")], side)
    # brins qui pendent de l'intrados, de chaque côté de la clé
    for x in (rnd.uniform(-7.5, -5.0), rnd.uniform(4.0, 6.5)):
        ztop = SPRING + math.sqrt(R * R - x * x) - 0.15
        y = side * (TG - 0.4)
        mb.card([(x, y, ztop), (x, y, ztop - rnd.uniform(6.5, 9.0))], 2.6, ATLAS[rnd.choice("AB")], side)
    # touffes au pied, devant les socles, et sur la corniche
    for x in (rnd.uniform(-19.0, -17.0), rnd.uniform(17.0, 19.0)):
        s = rnd.uniform(5.0, 6.5)
        mb.card([(x, side * (TG + 1.2), s * 0.74), (x, side * (TG + 1.2), -0.26 * s)], s, ATLAS[rnd.choice("CD")], side)
    x = rnd.uniform(-12, 12)
    mb.card([(x, side * (TG + 1.2), CORNICE + 2.2), (x, side * (TG + 1.2), CORNICE - 0.8)], 3.4, ATLAS["C"], side)

def gate(seed, letter):
    rnd = random.Random(seed); mb = MB()
    body(mb, rnd)
    for side in (-1, 1):
        archivolt(mb, side); plinths(mb, rnd, side); keystone(mb, rnd, side); panels(mb, side); frieze(mb, side)
    cornice(mb, rnd); pediment(mb, rnd, letter)
    for side in (-1, 1): ivy(mb, rnd, side)
    return mb, []

# ------------------------------------------------------------------ matériaux et assemblage
def get_carving_mat():
    def img(name, data=False):
        im = bpy.data.images.get(name) or bpy.data.images.load(os.path.join(TEXDIR, name), check_existing=True)
        im.reload()
        if data: im.colorspace_settings.name = "Non-Color"
        return im
    m = bpy.data.materials.get("M_GateCarving") or bpy.data.materials.new("M_GateCarving")
    m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); bs = nt.nodes.new("ShaderNodeBsdfPrincipled")
    ta = nt.nodes.new("ShaderNodeTexImage"); ta.image = img("T_GateCarving_Albedo.png")
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = img("T_GateCarving_Normal.png", True)
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(ta.outputs[0], bs.inputs["Base Color"])
    nt.links.new(tn.outputs[0], nm.inputs["Color"]); nt.links.new(nm.outputs[0], bs.inputs["Normal"])
    bs.inputs["Roughness"].default_value = 0.88
    nt.links.new(bs.outputs[0], out.inputs[0])
    return m

def build_gate(letter, xoff, uoff=4.4):
    name = NAME + "_" + letter
    mb, _ = gate(909, letter)          # même graine : les 4 variantes ne diffèrent que par la rose du fronton
    old = bpy.data.objects.get(name)
    if old: bpy.data.objects.remove(old)
    me = bpy.data.meshes.get(name)
    if me: bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, me)
    coll = bpy.data.collections.get(COLL) or bpy.data.collections.new(COLL)
    if coll.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(coll)
    coll.objects.link(obj)
    ivy_m, stone_m = get_mats()
    me.materials.append(ivy_m); me.materials.append(get_carving_mat()); me.materials.append(stone_m)
    remap = {0: 0, CARVE: 1, 1: 2, 2: 2}
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    fixed = set()
    for i, (fi, m) in enumerate(zip(mb.f, mb.mat)):
        vs = [bm.verts.new(mb.v[vi]) for vi in fi]
        f = bm.faces.new(vs); f.material_index = remap[m]
        if i in mb.uv:
            for loop, uv in zip(f.loops, mb.uv[i]): loop[uvl].uv = uv
            fixed.add(f)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bm.normal_update()
    for f in bm.faces:                       # pierre : projection boîte en coordonnées monde / TILE (comme les murs)
        if f.material_index != 2 or f in fixed: continue
        n = f.normal; ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop in f.loops:
            p = loop.vert.co
            if ay >= ax and ay >= az: u, v = (p.x + uoff) / TILE, p.z / TILE
            elif ax >= az:            u, v = (p.y + uoff * 0.37) / TILE, p.z / TILE
            else:                     u, v = (p.x + uoff) / TILE, (p.y + 0.5 * TILE) / TILE
            loop[uvl].uv = (u, v)
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='EAR_CLIP')
    bm.faces.sort(key=lambda f: f.material_index)
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = False
    obj.location.x = xoff
    return len(me.polygons)

GATE_EXPORT = os.path.normpath(os.path.join(DIR, "..", "..", "Assets", "Art", "Models", "Environment", "Gate_Stylized.fbx"))
GATE_NAMES = [NAME + "_" + c for c in LETTERS]

def build_gates():
    for n in (NAME,):                  # ancienne arche unique (avant les roses des vents)
        if bpy.data.objects.get(n): bpy.data.objects.remove(bpy.data.objects[n])
        if bpy.data.meshes.get(n): bpy.data.meshes.remove(bpy.data.meshes[n])
    return {NAME + "_" + c: build_gate(c, -120 - 60 * i) for i, c in enumerate(LETTERS)}

def export_gate():
    objs = [bpy.data.objects[n] for n in GATE_NAMES]
    saved = [o.location.x for o in objs]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.location.x = 0; o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=GATE_EXPORT, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_NONE', mesh_smooth_type='FACE', add_leaf_bones=False,
                             bake_anim=False, axis_forward='-Z', axis_up='Y')
    for o, x in zip(objs, saved): o.location.x = x

BUILD_RESULT = build_gates()
if globals().get("DO_EXPORT"): export_gate()
