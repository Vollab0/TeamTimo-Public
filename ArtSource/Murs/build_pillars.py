# Construit les 3 variantes de piliers stylisés de TeamTimo dans Blender (mêmes textures que les murs).
# Exécuter dans Blender, avec ArtSource/Murs/Mur_Stylized.blend ouvert :
#   exec(open(r"<dépôt>\ArtSource\Murs\build_pillars.py", encoding="utf-8").read(), {"DO_EXPORT": True})
# Réutilise build_walls.py (chargé en bibliothèque) : constructeur MB, matériaux, UV boîte, export.
# Repère : pivot en bas au centre, hauteur en Z (0..57,2), section carrée (rotation par quarts de tour dans Unity).
import bpy, bmesh, json, math, os, random
from mathutils import Vector, Matrix, Euler

_here = os.path.dirname(bpy.data.filepath)
globals()["LIB_ONLY"] = True
exec(open(os.path.join(_here, "build_walls.py"), encoding="utf-8").read(), globals())
COLL = "Piliers_Stylized"

S = 5.0              # demi-largeur du fût (pierres d'angle comprises : S + saillie)
Z_SHAFT = (6.2, 44.6)  # fût : du haut du socle au bas de la corniche
# profil (z, demi-largeur, chanfrein) : socle, fût légèrement conique, corniche, dé du chapiteau, tablette, sommet
PROFILE = [(0.0, 6.4, 0.8), (4.6, 6.4, 0.8), (6.2, 5.15, 0.6), (44.6, 5.0, 0.6),
           (44.6, 5.9, 0.75), (46.6, 5.9, 0.75), (47.1, 5.35, 0.55), (53.6, 5.35, 0.55),
           (53.6, 6.1, 0.8), (55.4, 6.1, 0.8), (57.2, 4.7, 0.5)]
FACES = [(1, 0), (0, 1), (-1, 0), (0, -1)]   # normales des 4 faces
STONES = [(fx, fy, fw, fh) for (fx, fy, fw, fh) in layout["stones"]]

def oct_ring(h, c, z, rnd, jit):
    pts = [(-h + c, -h), (h - c, -h), (h, -h + c), (h, h - c), (h - c, h), (-h + c, h), (-h, h - c), (-h, -h + c)]
    return [(x + rnd.uniform(-jit, jit), y + rnd.uniform(-jit, jit), z) for (x, y) in pts]

def body(mb, rnd):
    rings = []
    for (z, h, c) in PROFILE:
        jit = 0.0 if Z_SHAFT[0] < z < Z_SHAFT[1] else 0.07
        rings.append([mb.vert(p) for p in oct_ring(h, c, z, rnd, jit)])
    mb.face(rings[0][::-1], mat=2)                    # fond (supprimé après les booléens)
    for a, b in zip(rings, rings[1:]):
        for k in range(8):
            k2 = (k + 1) % 8
            mb.face([a[k], a[k2], b[k2], b[k]], mat=2)
    mb.face(rings[-1], mat=2)

def stone_uv(wf, hf, rnd):
    """Rectangle UV d'une pierre peinte de proportions proches de la face (wf x hf, m)."""
    target = math.log(wf / hf)
    cands = sorted(STONES, key=lambda s: abs(math.log((s[2] * TILE) / (s[3] * TILE)) - target))[:6]
    fx, fy, fw, fh = rnd.choice(cands)
    g = GAPF * 0.4
    return (fx + g, 1.0 - fy - fh + g, fx + fw - g, 1.0 - fy - g)

def courses(rnd):
    z0, z1 = Z_SHAFT
    n = 12
    hs = [rnd.uniform(0.85, 1.15) for _ in range(n)]
    k = (z1 - z0) / sum(hs)
    zs = [z0]
    for h in hs: zs.append(zs[-1] + h * k)
    return zs

def quoin(mb, rnd, sx, sy, za, zb, long_x, top=False):
    """Pierre d'angle saillante au coin (sx, sy) ; faces extérieures calées sur une pierre de la texture."""
    p = rnd.uniform(0.24, 0.36)
    lx = rnd.uniform(3.0, 3.5) if long_x else rnd.uniform(1.8, 2.2)
    ly = rnd.uniform(1.8, 2.2) if long_x else rnd.uniform(3.0, 3.5)
    xo, yo = S + p, S + p
    X = sorted((sx * xo, sx * (xo - lx))); Y = sorted((sy * yo, sy * (yo - ly)))
    j = lambda: rnd.uniform(-0.05, 0.05)
    c = []
    for (ix, iy, iz) in [(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)]:
        c.append(mb.vert((X[ix] + j(), Y[iy] + j(), (za + 0.02 if iz == 0 else zb) + j() * 0.5)))
    F = {'-z': (0, 3, 2, 1), '+z': (4, 5, 6, 7), '-y': (0, 1, 5, 4), '+y': (2, 3, 7, 6), '-x': (3, 0, 4, 7), '+x': (1, 2, 6, 5)}
    outer_x = '+x' if sx > 0 else '-x'; outer_y = '+y' if sy > 0 else '-y'
    for key, q in F.items():
        if key == '+z' and not top: continue
        if key == outer_x: uv = stone_uv(ly, zb - za, rnd)
        elif key == outer_y: uv = stone_uv(lx, zb - za, rnd)
        else: uv = None
        idx = [c[i] for i in q]
        if uv: mb.face(idx, mat=1, uvs=[(uv[0], uv[1]), (uv[2], uv[1]), (uv[2], uv[3]), (uv[0], uv[3])])
        else: mb.face(idx, mat=1)

def quoins(mb, rnd, zs, skip=lambda sx, sy, k: False):
    for sx in (-1, 1):
        for sy in (-1, 1):
            par = 0 if sx * sy > 0 else 1
            n = len(zs) - 1
            for k in range(n):
                if skip(sx, sy, k): continue
                top = k == n - 1 or skip(sx, sy, k + 1)
                quoin(mb, rnd, sx, sy, zs[k], zs[k + 1], (k + par) % 2 == 0, top=top)

# ------------------------------------------------------------------ lierre
def face_pt(nf, d, t, z):
    nx, ny = nf
    tx, ty = -ny, nx                     # Z x n : axe horizontal de la face, vers la droite vue de face
    return (nx * d + tx * t, ny * d + ty * t, z)

def card_face(mb, pts, width, cell, nf):
    """Carte de lierre devant la face de normale nf ; pts du haut vers le bas."""
    u0, v0, u1, v1 = cell
    tx, ty = -nf[1], nf[0]
    L = [0.0]
    for a, b in zip(pts, pts[1:]): L.append(L[-1] + (Vector(b) - Vector(a)).length)
    rows = []
    for pnt, l in zip(pts, L):
        vv = v1 - (v1 - v0) * (l / L[-1])
        rows.append((mb.vert((pnt[0] - tx * width / 2, pnt[1] - ty * width / 2, pnt[2])),
                     mb.vert((pnt[0] + tx * width / 2, pnt[1] + ty * width / 2, pnt[2])), vv))
    for (a0, a1, va), (b0, b1, vb) in zip(rows, rows[1:]):
        mb.face([b0, b1, a1, a0], mat=0, uvs=[(u0, vb), (u1, vb), (u1, va), (u0, va)])

def ivy_from_top(mb, rnd, nf, t, length, width=None):
    """Brin qui part du sommet, passe par-dessus la tablette et la corniche, et pend le long des pierres d'angle."""
    e = 0.18
    prof = [(4.9, 56.9), (6.1 + e, 55.45), (6.1 + e, 53.5), (5.35 + e, 52.6), (5.35 + e, 47.3),
            (5.9 + e, 46.7), (5.9 + e, 44.5), (S + 0.36 + e, 43.4)]
    zend = 55.4 - length
    pts = [face_pt(nf, d, t, z) for (d, z) in prof if z > zend + 1.0]
    pts.append(face_pt(nf, (S + 0.36 + e) if zend < 44 else 5.9 + e, t, zend))
    card_face(mb, pts, width or rnd.uniform(2.4, 3.0), ATLAS[rnd.choice("AB")], nf)

def ivy_from(mb, rnd, nf, t, d, ztop, length, width=None):
    pts = [face_pt(nf, d, t, ztop + 0.3), face_pt(nf, d + 0.1, t, ztop - length)]
    card_face(mb, pts, width or rnd.uniform(2.2, 2.8), ATLAS[rnd.choice("AB")], nf)

def ivy_clump_face(mb, rnd, nf, t, size, d=6.4 + 0.15, zbase=0.0):
    z0 = zbase - 0.26 * size
    card_face(mb, [face_pt(nf, d, t, z0 + size), face_pt(nf, d, t, z0)], size, ATLAS[rnd.choice("CD")], nf)

def rubble(mb, rnd, x, y, z, s):
    mb.block(x, y, z + s * 0.36, s * rnd.uniform(1.1, 1.5), s, s * 0.8, rnd, jit=0.22,
             rot=(rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), rnd.uniform(-0.8, 0.8)), skip=('-z',))

# ------------------------------------------------------------------ variantes
def pillar_intact(name, seed, uoff):
    rnd = random.Random(seed); mb = MB()
    body(mb, rnd); zs = courses(rnd)
    quoins(mb, rnd, zs)
    faces = rnd.sample(FACES, 2)
    for nf in faces:
        ivy_from_top(mb, rnd, nf, rnd.choice((-1, 1)) * rnd.uniform(3.6, 4.2), rnd.uniform(16, 24))
    for nf in rnd.sample(FACES, 2):
        ivy_clump_face(mb, rnd, nf, rnd.choice((-1, 1)) * rnd.uniform(3.8, 4.6), rnd.uniform(3.6, 4.6))
    return mb, []

def pillar_broken(name, seed, uoff):
    rnd = random.Random(seed); mb = MB()
    body(mb, rnd); zs = courses(rnd)
    sx, sy = rnd.choice((-1, 1)), rnd.choice((-1, 1))
    k1 = len(zs) - 7                                  # l'effondrement descend jusqu'à 6 assises sous la corniche
    zlow = zs[k1]
    quoins(mb, rnd, zs, skip=lambda a, b, k: (a, b) == (sx, sy) and k >= k1)
    # cassure en escalier dans le coin (sx, sy) : une marche par assise, de plus en plus profonde vers le haut
    boxes = []
    steps = [(1.8, 2.2, zs[k1]), (2.7, 2.6, zs[k1 + 1]), (3.4, 3.6, zs[k1 + 3]), (4.6, 4.2, zs[k1 + 5]), (6.4, 5.6, 47.6)]
    for (dx, dy, z0) in steps:
        dx += rnd.uniform(-0.3, 0.3); dy += rnd.uniform(-0.3, 0.3)
        x0, y0 = S - dx, S - dy
        boxes.append(((sx * (x0 + 12) / 2, sy * (y0 + 12) / 2, (z0 + 80) / 2), (12 - x0, 12 - y0, 80 - z0),
                      (rnd.uniform(-0.03, 0.03), rnd.uniform(-0.03, 0.03), rnd.uniform(-0.05, 0.05))))
    for (dx, dy, z0) in steps[1:3]:                   # blocs tombés sur les marches
        rubble(mb, rnd, sx * (S - dx + 0.7), sy * (S - dy + 0.5), z0 - 0.15, rnd.uniform(0.8, 1.1))
    # éboulis : au pied du coin cassé (dans le quart du coin, hors des murs) et sur la première marche
    placed = 0
    while placed < 4:
        x, y = rnd.uniform(4.6, 10.0), rnd.uniform(4.6, 10.0)
        if max(x, y) < 7.4: continue
        rubble(mb, rnd, sx * x, sy * y, 0.0, rnd.uniform(1.1, 2.0)); placed += 1
    # lierre : depuis les marches de la cassure, et depuis le sommet sur une face opposée
    # axe t d'une face de normale (nx, ny) = (-ny, nx) : sur la face (sx, 0), y = sx * t ; sur (0, sy), x = -sy * t
    nfx, nfy = (sx, 0), (0, sy)
    ivy_from(mb, rnd, nfx, sx * sy * (S - 1.0), S + 0.5, zlow, rnd.uniform(12, 18))
    ivy_from(mb, rnd, nfy, -sy * sx * (S - 2.2), S + 0.5, zs[k1 + 2], rnd.uniform(14, 20))
    other = [f for f in FACES if f not in (nfx, nfy)]
    ivy_from_top(mb, rnd, rnd.choice(other), rnd.choice((-1, 1)) * rnd.uniform(3.6, 4.2), rnd.uniform(16, 22))
    ivy_clump_face(mb, rnd, nfx, sx * sy * 4.2, rnd.uniform(3.6, 4.4))
    return mb, boxes

def pillar_overgrown(name, seed, uoff):
    rnd = random.Random(seed); mb = MB()
    body(mb, rnd); zs = courses(rnd)
    n = len(zs) - 1
    missing = set()
    for _ in range(4):
        missing.add(((rnd.choice((-1, 1)), rnd.choice((-1, 1))), rnd.randint(1, n - 2)))
    quoins(mb, rnd, zs, skip=lambda a, b, k: ((a, b), k) in missing)
    boxes = []
    for ((sx, sy), k) in missing:                     # renfoncement là où la pierre d'angle est tombée
        d = rnd.uniform(0.8, 1.1)
        boxes.append(((sx * (S - d + 6) / 2 + sx * 0.0, sy * (S - d + 6) / 2, (zs[k] + zs[k + 1]) / 2),
                      (6 - (S - d), 6 - (S - d), zs[k + 1] - zs[k] - 0.04), (0, 0, rnd.uniform(-0.04, 0.04))))
        rubble(mb, rnd, sx * rnd.uniform(7.0, 8.5), sy * rnd.uniform(5.5, 7.5), 0.0, rnd.uniform(1.2, 1.7))
    # beaucoup de lierre : deux brins larges par face depuis le sommet (un de chaque côté), plus des touffes au pied
    for nf in FACES:
        for side in (-1, 1):
            ivy_from_top(mb, rnd, nf, side * rnd.uniform(3.4, 3.9), rnd.uniform(22, 40), width=rnd.uniform(3.2, 3.8))
    for nf in rnd.sample(FACES, 3):
        ivy_clump_face(mb, rnd, nf, rnd.choice((-1, 1)) * rnd.uniform(3.8, 4.6), rnd.uniform(4.0, 5.2))
    nf = rnd.choice(FACES)
    ivy_clump_face(mb, rnd, nf, rnd.choice((-1, 1)) * 3.9, 3.2, d=5.9 + 0.2, zbase=46.6)   # touffe sur la corniche
    return mb, boxes

# ------------------------------------------------------------------ booléens et nettoyage
def apply_box_cutters(obj, boxes):
    """Booléens successifs (un cube par boîte), solveur Manifold : le solveur Exact vide le corps
    dès que les boîtes sont légèrement tournées."""
    for (c, size, rot) in boxes:
        me = bpy.data.meshes.new("cut"); bm = bmesh.new()
        geom = bmesh.ops.create_cube(bm, size=1.0)["verts"]
        bmesh.ops.scale(bm, vec=size, verts=geom)
        bmesh.ops.rotate(bm, verts=geom, cent=(0, 0, 0), matrix=Euler(rot).to_matrix())
        bmesh.ops.translate(bm, vec=c, verts=geom)
        bm.to_mesh(me); bm.free()
        cut = bpy.data.objects.new("cut", me)
        bpy.context.scene.collection.objects.link(cut)
        mod = obj.modifiers.new("cut", 'BOOLEAN'); mod.operation = 'DIFFERENCE'; mod.solver = 'MANIFOLD'; mod.object = cut
        with bpy.context.temp_override(object=obj, active_object=obj):
            bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.data.objects.remove(cut); bpy.data.meshes.remove(me)
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(0.5), verts=bm.verts[:], edges=bm.edges[:])
    bm.to_mesh(obj.data); bm.free()
    # le booléen peut modifier la liste des matériaux : on la remet à [lierre, pierre]
    ivy, stone = get_mats()
    obj.data.materials.clear(); obj.data.materials.append(ivy); obj.data.materials.append(stone)

def drop_bottom(obj):
    bm = bmesh.new(); bm.from_mesh(obj.data); bm.normal_update()
    bot = [f for f in bm.faces if f.normal.z < -0.99 and all(v.co.z < 0.01 for v in f.verts)]
    bmesh.ops.delete(bm, geom=bot, context='FACES_ONLY')
    bm.to_mesh(obj.data); bm.free(); obj.data.update()

PILLARS = (("Pillar_A_Intact", pillar_intact, 101, 3.1, 200),
           ("Pillar_B_Broken", pillar_broken, 202, 9.7, 225),
           ("Pillar_C_Overgrown", pillar_overgrown, 303, 14.2, 250))

def build_pillars():
    res = {}
    for name, fn, seed, uoff, xoff in PILLARS:
        o, _ = build(name, fn, seed, uoff, xoff, cut_fn=apply_box_cutters)
        drop_bottom(o)
        res[name] = sum(len(p.vertices) - 2 for p in o.data.polygons)
    return res

PILLAR_EXPORT = os.path.normpath(os.path.join(DIR, "..", "..", "Assets", "Art", "Models", "Environment", "Pillars_Stylized.fbx"))

def export_pillars():
    names = [p[0] for p in PILLARS]
    saved = {n: bpy.data.objects[n].location.x for n in names}
    for n in names: bpy.data.objects[n].location.x = 0
    bpy.ops.object.select_all(action='DESELECT')
    for n in names: bpy.data.objects[n].select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects[names[0]]
    bpy.ops.export_scene.fbx(filepath=PILLAR_EXPORT, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_NONE', mesh_smooth_type='FACE', add_leaf_bones=False,
                             bake_anim=False, axis_forward='-Z', axis_up='Y')
    for n in names: bpy.data.objects[n].location.x = saved[n]

BUILD_RESULT = build_pillars()
if globals().get("DO_EXPORT"): export_pillars()
