# Etape 2 : maillage de jeu. Allege les maillages bruts (bull_01_shape.py), ajoute yeux, paupieres, sourcils et queue,
# assemble le tout dans "Bull_Body", deplie les UV, puis separe les yeux et les paupieres en objets a part
# (Bull_Eye_L/R, Bull_Lid_L/R) pour pouvoir les animer independamment, comme sur le renard.
# Chaque face porte un attribut entier "part" (voir PARTS) : il sert a peindre la texture (etape 3) et a skinner (etape 4).
import bpy
import bmesh
import math
import numpy as np
from mathutils import Vector

COMMON = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo\ArtSource\Taureau\bull_common.py"
exec(open(COMMON, encoding="utf-8").read())

BODY_TRIS = globals().get("BODY_TRIS", 17000)   # 15000 -> 17000 : mains moins anguleuses
col = get_collection("Bull")
raw_col = get_collection("Bull_Raw")

if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')

SEPARATE = {"eye_L": "Bull_Eye_L", "eye_R": "Bull_Eye_R", "lid_L": "Bull_Lid_L", "lid_R": "Bull_Lid_R"}
for name in ["Bull_Body"] + list(SEPARATE.values()) + [o.name for o in bpy.data.objects if o.name.startswith("Bull_Part_")]:
    ob = bpy.data.objects.get(name)
    if ob:
        bpy.data.objects.remove(ob, do_unlink=True)

# Les maillages bruts vont dans une collection a part, masquee au rendu
for o in list(bpy.data.objects):
    if o.name.startswith("Bull_Raw_"):
        for c in list(o.users_collection):
            c.objects.unlink(o)
        raw_col.objects.link(o)
        o.hide_render = True
        o.hide_set(False)


def new_object(name, verts, faces, smooth=True):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], [tuple(f) for f in faces])
    me.polygons.foreach_set("use_smooth", [smooth] * len(me.polygons))
    me.update()
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)
    return ob


def dup_raw(src_name, new_name, mirror=False):
    src = bpy.data.objects[src_name]
    ob = bpy.data.objects.new(new_name, src.data.copy())
    col.objects.link(ob)
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    if mirror:
        for v in bm.verts:
            v.co.x = -v.co.x
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(ob.data)
    bm.free()
    return ob


def apply_modifiers(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    me_new = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    me_old = ob.data
    ob.modifiers.clear()
    ob.data = me_new
    me_new.name = ob.name
    bpy.data.meshes.remove(me_old)


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def decimate(ob, tris, symmetry=False):
    mod = ob.modifiers.new("Decimate", 'DECIMATE')
    mod.ratio = min(1.0, tris / max(1, tri_count(ob)))
    mod.use_symmetry = symmetry
    mod.symmetry_axis = 'X'
    mod.use_collapse_triangulate = True
    bpy.context.view_layer.update()
    apply_modifiers(ob)


def make_outward(ob):
    """Retourne les faces si la piece a ses normales vers l'interieur (arrivait aux pieces symetrisees :
    corne et oreille droites eclairees a l'envers dans Unity)."""
    me_ = ob.data
    n = len(me_.polygons)
    cen = np.zeros(n * 3)
    me_.polygons.foreach_get("center", cen)
    nor = np.zeros(n * 3)
    me_.polygons.foreach_get("normal", nor)
    cen, nor = cen.reshape(-1, 3), nor.reshape(-1, 3)
    if np.mean(np.sum((cen - cen.mean(0)) * nor, axis=1) > 0) >= 0.5:
        return
    bm = bmesh.new()
    bm.from_mesh(me_)
    bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    bm.to_mesh(me_)
    bm.free()


def set_part(ob, part):
    attr = ob.data.attributes.get("part") or ob.data.attributes.new("part", 'INT', 'FACE')
    attr.data.foreach_set("value", [part] * len(ob.data.polygons))


def tube(points, radii, segs=8, up_hint=(0, 0, 1)):
    """Tube ferme le long d'une liste de points (anneaux de `segs` sommets)."""
    pts = [Vector(p) for p in points]
    verts, faces = [], []
    n = len(pts)
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        side = t.cross(Vector(up_hint))
        if side.length < 1e-4:
            side = t.cross(Vector((1, 0, 0)))
        side.normalize()
        up = side.cross(t).normalized()
        for s in range(segs):
            a = 2 * math.pi * s / segs
            verts.append(p + (side * math.cos(a) + up * math.sin(a)) * radii[i])
    for i in range(n - 1):
        for s in range(segs):
            a, b = i * segs + s, i * segs + (s + 1) % segs
            faces.append((a, b, b + segs, a + segs))
    faces.append(tuple(reversed(range(segs))))
    faces.append(tuple(range((n - 1) * segs, n * segs)))
    return verts, faces


PARTS = {"body": 0, "hoof": 1, "horn": 2, "eye_L": 3, "eye_R": 4, "lid_L": 5, "lid_R": 6,
         "brow_L": 7, "brow_R": 8, "tail": 9, "tuft": 10, "ear_L": 11, "ear_R": 12}
parts = []

# --------------------------------------------------------------------------------------
# Corps : decimation symetrique, puis normales reprises du maillage dense (ombrage lisse)
# --------------------------------------------------------------------------------------
raw_body = bpy.data.objects["Bull_Raw_Body"]
bm = bmesh.new()
bm.from_mesh(raw_body.data)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
bm.to_mesh(raw_body.data)
bm.free()

body = dup_raw("Bull_Raw_Body", "Bull_Body")
decimate(body, BODY_TRIS, symmetry=True)
dt = body.modifiers.new("Normals", 'DATA_TRANSFER')
dt.object = raw_body
dt.use_loop_data = True
dt.data_types_loops = {'CUSTOM_NORMAL'}
dt.loop_mapping = 'POLYINTERP_NEAREST'
bpy.context.view_layer.update()
apply_modifiers(body)
set_part(body, PARTS["body"])

# --------------------------------------------------------------------------------------
# Sabots et cornes (bruts = cote gauche ; le droit par symetrie)
# --------------------------------------------------------------------------------------
for side, mirror in (("L", False), ("R", True)):
    hoof = dup_raw("Bull_Raw_Hoof", f"Bull_Part_Hoof_{side}", mirror)
    decimate(hoof, 420)
    make_outward(hoof)
    hoof.data.set_sharp_from_angle(angle=math.radians(38))
    set_part(hoof, PARTS["hoof"])
    horn = dup_raw("Bull_Raw_Horn", f"Bull_Part_Horn_{side}", mirror)
    decimate(horn, 380)
    make_outward(horn)
    set_part(horn, PARTS["horn"])
    ear = dup_raw("Bull_Raw_Ear", f"Bull_Part_Ear_{side}", mirror)
    decimate(ear, 800)          # 420 -> 800 : oreilles moins anguleuses de pres
    make_outward(ear)
    set_part(ear, PARTS[f"ear_{side}"])
    parts += [hoof, horn, ear]

# --------------------------------------------------------------------------------------
# Yeux : globe (l'iris est peint dans la texture) + paupiere superieure (demi-boule qui pivote autour du centre)
# --------------------------------------------------------------------------------------
LID_ELEV = 17.0      # la paupiere couvre le globe au-dessus de cette elevation (degres)
LID_ROLL = 5.0       # coin exterieur un peu plus bas, comme sur la reference

def uv_sphere(center, radius, segs=18, rings=10):
    bm_ = bmesh.new()
    bmesh.ops.create_uvsphere(bm_, u_segments=segs, v_segments=rings, radius=radius)
    for v in bm_.verts:      # pole vers l'avant : les meridiens convergent derriere l'iris
        v.co = Vector((v.co.x, -v.co.z, v.co.y)) + Vector(center)
    verts = [v.co.copy() for v in bm_.verts]
    faces = [[v.index for v in f.verts] for f in bm_.faces]
    bm_.free()
    return verts, faces

def half_ball(center, radius, normal, segs=18, rings=6):
    """Demi-boule : le dome est du cote de `normal`, fermee par un disque plat passant par le centre."""
    n = Vector(normal).normalized()
    a = n.orthogonal().normalized()
    b = n.cross(a)
    c = Vector(center)
    verts = [c + n * radius]
    for k in range(1, rings + 1):
        th = (math.pi / 2) * k / rings
        for s in range(segs):
            ph = 2 * math.pi * s / segs
            verts.append(c + (n * math.cos(th) + (a * math.cos(ph) + b * math.sin(ph)) * math.sin(th)) * radius)
    faces = []
    for s in range(segs):
        faces.append((0, 1 + s, 1 + (s + 1) % segs))
    for k in range(rings - 1):
        for s in range(segs):
            r0, r1 = 1 + k * segs, 1 + (k + 1) * segs
            faces.append((r0 + s, r1 + s, r1 + (s + 1) % segs, r0 + (s + 1) % segs))
    rim = 1 + (rings - 1) * segs
    verts.append(c.copy())
    ci = len(verts) - 1
    for s in range(segs):
        faces.append((rim + (s + 1) % segs, rim + s, ci))
    return verts, faces

for side, sx in (("L", 1), ("R", -1)):
    c = Vector((EYE_C[0] * sx, EYE_C[1], EYE_C[2]))
    v, f = uv_sphere(c, EYE_R)
    eye = new_object(f"Bull_Part_Eye_{side}", v, f)
    set_part(eye, PARTS[f"eye_{side}"])
    fwd, up = Vector((0, -1, 0)), Vector((0, 0, 1))
    e = math.radians(LID_ELEV)
    n = up * math.cos(e) - fwd * math.sin(e)
    roll = math.radians(LID_ROLL) * sx            # autour de l'axe avant
    n = n + Vector((1, 0, 0)) * math.sin(roll)
    v, f = half_ball(c, EYE_R + 0.008, n)
    lid = new_object(f"Bull_Part_Lid_{side}", v, f)
    lid.data.set_sharp_from_angle(angle=math.radians(50))
    set_part(lid, PARTS[f"lid_{side}"])
    parts += [eye, lid]

# --------------------------------------------------------------------------------------
# Sourcils : arcs clairs en relief, poses sur le front (projection sur le maillage dense)
# --------------------------------------------------------------------------------------
BROW_PTS = [(0.080, 1.825), (0.112, 1.845), (0.155, 1.855), (0.200, 1.843), (0.238, 1.821), (0.262, 1.795)]
BROW_RAD = [0.007, 0.014, 0.017, 0.017, 0.014, 0.007]

def on_surface(x, z, y_start=-0.8):
    hit, loc, nor, idx = raw_body.ray_cast(Vector((x, y_start, z)), Vector((0, 1, 0)))
    return loc if hit else Vector((x, -0.3, z))

for side, sx in (("L", 1), ("R", -1)):
    ctrl = [on_surface(x * sx, z) + Vector((0, 0.004, 0)) for x, z in BROW_PTS]
    bp, br = spline(ctrl, BROW_RAD, 3)
    v, f = tube(bp, br, segs=6)
    brow = new_object(f"Bull_Part_Brow_{side}", v, f)
    bm = bmesh.new()
    bm.from_mesh(brow.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(brow.data)
    bm.free()
    set_part(brow, PARTS[f"brow_{side}"])
    parts.append(brow)

# --------------------------------------------------------------------------------------
# Queue : tube fin + toupet sombre au bout
# --------------------------------------------------------------------------------------
tp, tr = spline(TAIL_PTS, [0.026, 0.023, 0.021, 0.019, 0.018], 4)
v, f = tube(tp, tr, segs=8, up_hint=(1, 0, 0))
tail = new_object("Bull_Part_Tail", v, f)
set_part(tail, PARTS["tail"])
end = Vector(TAIL_PTS[-1])
d = (Vector(TAIL_PTS[-1]) - Vector(TAIL_PTS[-2])).normalized()
tuft_pts = [end - d * 0.02, end + d * 0.03, end + d * 0.09, end + d * 0.15, end + d * 0.19]
v, f = tube(tuft_pts, [0.020, 0.050, 0.056, 0.036, 0.004], segs=8, up_hint=(1, 0, 0))
tuft = new_object("Bull_Part_Tuft", v, f)
set_part(tuft, PARTS["tuft"])
for o in (tail, tuft):
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(o.data)
    bm.free()
parts += [tail, tuft]

# --------------------------------------------------------------------------------------
# Assemblage en un seul objet
# --------------------------------------------------------------------------------------
objs = [body] + parts
for o in bpy.context.view_layer.objects:
    o.select_set(False)
for o in objs:
    o.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = "Bull_Body"
body.data.name = "Bull_Body"

# --------------------------------------------------------------------------------------
# UV : la tete est depliee a part et agrandie (plus de detail pour le visage)
# --------------------------------------------------------------------------------------
me = body.data
part = np.zeros(len(me.polygons), dtype=np.int32)
me.attributes["part"].data.foreach_get("value", part)
centers = np.zeros(len(me.polygons) * 3, dtype=np.float32)
me.polygons.foreach_get("center", centers)
centers = centers.reshape(-1, 3)
face_parts = ((part >= 3) & (part <= 8)) | (part >= 11)
is_head = face_parts | ((part == 0) & (centers[:, 2] > 1.36) & (np.abs(centers[:, 0]) < 0.34) & (centers[:, 1] < -0.02))

if not me.uv_layers:
    me.uv_layers.new(name="UVMap")

def unwrap(mask, angle):
    me.polygons.foreach_set("select", mask.tolist())
    me.update()
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_mode(type='FACE')
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=0.002, area_weight=0.0,
                             correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')

unwrap(is_head, 66)
unwrap(~is_head, 66)

me.polygons.foreach_set("select", [True] * len(me.polygons))
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.select_all(action='SELECT')
bpy.ops.uv.average_islands_scale()
bpy.ops.object.mode_set(mode='OBJECT')

HEAD_UV_SCALE = 1.7
EYE_UV_SCALE = 3.0
uv = np.zeros(len(me.loops) * 2, dtype=np.float32)
me.uv_layers.active.data.foreach_get("uv", uv)
uv = uv.reshape(-1, 2)
loop_face = np.zeros(len(me.loops), dtype=np.int32)
for p in me.polygons:
    loop_face[p.loop_start:p.loop_start + p.loop_total] = p.index
scale = np.ones(len(me.loops), dtype=np.float32)
scale[is_head[loop_face]] = HEAD_UV_SCALE
scale[((part == 3) | (part == 4))[loop_face]] = EYE_UV_SCALE
uv *= scale[:, None]
me.uv_layers.active.data.foreach_set("uv", uv.ravel())

bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.select_all(action='SELECT')
bpy.ops.uv.pack_islands(rotate=True, margin=0.0025)
bpy.ops.object.mode_set(mode='OBJECT')

uv_all = uv.copy()
me.uv_layers.active.data.foreach_get("uv", uv_all.ravel())
tris_by_part = {k: int(sum(len(p.vertices) - 2 for p in me.polygons if part[p.index] == v)) for k, v in PARTS.items()}

# --------------------------------------------------------------------------------------
# Yeux et paupieres : objets separes (meme texture, memes UV)
# --------------------------------------------------------------------------------------
for key, name in SEPARATE.items():
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.mode_set(mode='EDIT')
    bm = bmesh.from_edit_mesh(body.data)
    layer = bm.faces.layers.int["part"]
    for f_ in bm.faces:
        f_.select_set(False)
    for f_ in bm.faces:
        if f_[layer] == PARTS[key]:
            f_.select_set(True)
    bm.select_flush(True)
    bmesh.update_edit_mesh(body.data)
    bpy.ops.mesh.separate(type='SELECTED')
    bpy.ops.object.mode_set(mode='OBJECT')
    new = [o for o in bpy.context.selected_objects if o != body][0]
    new.name = name
    new.data.name = name

me = body.data
for o in raw_col.objects:
    o.hide_set(True)
result = {
    "verts": len(me.vertices),
    "tris_body_object": tri_count(body),
    "tris_total": int(sum(tris_by_part.values())),
    "tris_by_part": tris_by_part,
    "objects": {n: tri_count(bpy.data.objects[n]) for n in SEPARATE.values()},
    "uv_min": [float(uv_all[:, 0].min()), float(uv_all[:, 1].min())],
    "uv_max": [float(uv_all[:, 0].max()), float(uv_all[:, 1].max())],
}
