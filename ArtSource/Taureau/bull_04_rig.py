# Etape 4 : squelette du taureau, skinning du corps, yeux et paupieres attaches a leurs os.
# - Os de deformation (exportes vers Unity) + os de controle (IK des pieds, cibles des genoux, regard).
# - Corps : poids automatiques de Blender, puis retouches (tete rigide, museau, oreilles, queue, sabots, cornes).
# - Yeux / paupieres : objets separes parentes aux os Eye_L/R et Lid_L/R (comme le renard) :
#     regard = deplacer Ctrl_Eyes (ou Ctrl_Eye_L / Ctrl_Eye_R) ; clignement = tourner Lid_L / Lid_R.
import bpy
import bmesh
import math
import numpy as np
from mathutils import Vector, Matrix, kdtree

COMMON = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo\ArtSource\Taureau\bull_common.py"
exec(open(COMMON, encoding="utf-8").read())

col = get_collection("Bull")
body = bpy.data.objects["Bull_Body"]
if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')

for name in ("Bull_Rig", "Bull_Tmp_Skin"):
    ob = bpy.data.objects.get(name)
    if ob:
        bpy.data.objects.remove(ob, do_unlink=True)
for arm_ in list(bpy.data.armatures):
    if arm_.users == 0:
        bpy.data.armatures.remove(arm_)

# --------------------------------------------------------------------------------------
# Os : (nom, tete, queue, parent, deforme)
# --------------------------------------------------------------------------------------
SH, EL, WR = (0.55, 0.0, 1.26), (0.80, 0.01, 1.125), (0.985, -0.01, 1.01)
PALM, TIP = (1.10, -0.012, 0.94), (1.25, -0.015, 0.855)
HIP, KN, AN = (0.205, 0.01, 0.56), (0.21, -0.03, 0.36), (0.205, 0.01, 0.165)
TOE = (0.205, -0.15, 0.04)
ex, ey, ez = float(EYE_C[0]), float(EYE_C[1]), float(EYE_C[2])

BONES = [
    ("Root", (0, 0, 0), (0, 0, 0.15), None, True),
    ("Hips", (0, 0.02, 0.60), (0, 0.02, 0.78), "Root", True),
    ("Spine", (0, 0.02, 0.78), (0, 0.01, 1.02), "Hips", True),
    ("Chest", (0, 0.01, 1.02), (0, 0.0, 1.40), "Spine", True),
    ("Head", (0, -0.02, 1.50), (0, -0.06, 1.86), "Chest", True),
    ("Muzzle", (0, -0.30, 1.48), (0, -0.50, 1.48), "Head", True),
]
SIDE = [
    ("Ear", tuple(EAR_ROOT), tuple(EAR_TIP), "Head", True),
    ("Brow", (0.16, -0.34, 1.84), (0.16, -0.42, 1.84), "Head", True),
    ("Eye", (ex, ey, ez), (ex, ey - 0.12, ez), "Head", True),
    ("Lid", (ex, ey, ez), (ex, ey - 0.11, ez + 0.035), "Head", True),
    ("Shoulder", (0.20, 0.02, 1.40), (0.50, 0.0, 1.30), "Chest", True),
    ("UpperArm", SH, EL, "Shoulder", True),
    ("Forearm", EL, WR, "UpperArm", True),
    ("Hand", WR, PALM, "Forearm", True),
    ("Fingers", PALM, TIP, "Hand", True),
    ("Thumb", (1.0, -0.05, 0.95), (0.992, -0.075, 0.83), "Hand", True),
    ("Thigh", HIP, KN, "Hips", True),
    ("Shin", KN, AN, "Thigh", True),
    ("Foot", AN, TOE, "Shin", True),
    # controles (non exportes)
    ("IK_Foot", AN, TOE, "Root", False),
    ("Ctrl_Eye", (ex, ey - 1.2, ez), (ex, ey - 1.2, ez + 0.08), "Ctrl_Eyes", False),
]
BONES.append(("Ctrl_Eyes", (0, ey - 1.2, ez), (0, ey - 1.2, ez + 0.14), "Head", False))
knee_dir = (Vector(KN) - (Vector(HIP) + Vector(AN)) / 2).normalized()
pole = Vector(KN) + knee_dir * 0.7
SIDE.append(("Pole_Knee", tuple(pole), tuple(pole + Vector((0, 0, 0.1))), "Root", False))


def mx(p):
    return (-p[0], p[1], p[2])

SIDED_PARENTS = {b[0] for b in SIDE} | {"Ctrl_Eye"}
for name, head, tail, parent, deform in SIDE:
    for suffix, f in (("_L", lambda p: p), ("_R", mx)):
        par = parent + suffix if parent in SIDED_PARENTS and parent != "Ctrl_Eyes" else parent
        BONES.append((name + suffix, f(head), f(tail), par, deform))
tail_n = len(TAIL_PTS) - 1
for i in range(tail_n):
    tail_end = TAIL_PTS[i + 1]
    if i == tail_n - 1:      # le dernier os porte aussi le toupet
        d_ = (Vector(TAIL_PTS[-1]) - Vector(TAIL_PTS[-2])).normalized()
        tail_end = tuple(Vector(TAIL_PTS[-1]) + d_ * 0.12)
    BONES.append((f"Tail_{i + 1:02d}", TAIL_PTS[i], tail_end, "Hips" if i == 0 else f"Tail_{i:02d}", True))

# ordre : un parent doit exister avant ses enfants
order, done = [], set()
pending = list(BONES)
while pending:
    for b in list(pending):
        if b[3] is None or b[3] in done:
            order.append(b)
            done.add(b[0])
            pending.remove(b)
BONES = order

arm_data = bpy.data.armatures.new("Bull_Rig")
rig = bpy.data.objects.new("Bull_Rig", arm_data)
col.objects.link(rig)
for o in bpy.context.view_layer.objects:
    o.select_set(False)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name, head, tail, parent, deform in BONES:
    eb = arm_data.edit_bones.new(name)
    eb.head = head
    eb.tail = tail
    eb.roll = 0.0
    eb.use_deform = deform
    if parent:
        eb.parent = arm_data.edit_bones[parent]
        eb.use_connect = False
bpy.ops.object.mode_set(mode='OBJECT')
arm_data.display_type = 'OCTAHEDRAL'
rig.show_in_front = True
for pb in rig.pose.bones:
    pb.rotation_mode = 'QUATERNION'

# Collections d'os : deformation / controles
c_def = arm_data.collections.new("Deformation")
c_ctl = arm_data.collections.new("Controles")
for b in arm_data.bones:
    (c_def if b.use_deform else c_ctl).assign(b)

DEFORM = [b[0] for b in BONES if b[4]]
bi = {n: i for i, n in enumerate(DEFORM)}

# --------------------------------------------------------------------------------------
# Poids automatiques sur une copie du corps seul (sans cornes, sabots, oreilles... qui sont des ilots)
# --------------------------------------------------------------------------------------
tmp_me = body.data.copy()
tmp = bpy.data.objects.new("Bull_Tmp_Skin", tmp_me)
col.objects.link(tmp)
bm = bmesh.new()
bm.from_mesh(tmp_me)
layer = bm.faces.layers.int["part"]
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f[layer] != 0], context='FACES')
bm.to_mesh(tmp_me)
bm.free()
tmp.vertex_groups.clear()

HEAT = {"Hips", "Spine", "Chest", "Head"}
for base in ("Shoulder", "UpperArm", "Forearm", "Hand", "Fingers", "Thumb", "Thigh", "Shin", "Foot"):
    HEAT |= {base + "_L", base + "_R"}
for b in arm_data.bones:
    b.use_deform = b.name in HEAT
for o in bpy.context.view_layer.objects:
    o.select_set(False)
tmp.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
for b in arm_data.bones:
    b.use_deform = b.name in DEFORM

nv_tmp = len(tmp_me.vertices)
W_tmp = np.zeros((nv_tmp, len(DEFORM)), dtype=np.float64)
gname = {g.index: g.name for g in tmp.vertex_groups}
for v in tmp_me.vertices:
    for g in v.groups:
        n = gname[g.group]
        if n in bi:
            W_tmp[v.index, bi[n]] = g.weight
unweighted = int((W_tmp.sum(axis=1) < 1e-4).sum())

# --------------------------------------------------------------------------------------
# Report sur Bull_Body (par position) et retouches
# --------------------------------------------------------------------------------------
me = body.data
nv = len(me.vertices)
co = np.zeros(nv * 3, dtype=np.float64)
me.vertices.foreach_get("co", co)
co = co.reshape(-1, 3)
face_part = np.zeros(len(me.polygons), dtype=np.int32)
me.attributes["part"].data.foreach_get("value", face_part)
vpart = np.zeros(nv, dtype=np.int32)
for p in me.polygons:
    for vi in p.vertices:
        vpart[vi] = face_part[p.index]

kd = kdtree.KDTree(nv_tmp)
for v in tmp_me.vertices:
    kd.insert(v.co, v.index)
kd.balance()
W = np.zeros((nv, len(DEFORM)), dtype=np.float64)
for i in np.nonzero(vpart == 0)[0]:
    _, j, _ = kd.find(co[i])
    W[i] = W_tmp[j]
bpy.data.objects.remove(tmp, do_unlink=True)

x, y, z = co[:, 0], co[:, 1], co[:, 2]
ax = np.abs(x)
is_body = vpart == 0


def sstep(e0, e1, v):
    t = np.clip((v - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)

# Les poids automatiques melangent trop d'os sur le tronc (cuisses et epaules jusqu'au ventre -> plis).
# On ne garde d'eux que les membres, limites a leur zone ; le tronc est reparti par hauteur (Hips / Spine / Chest).
def limit(names, mask):
    for n_ in names:
        W[:, bi[n_]] *= mask

for s_, sx in (("_L", 1.0), ("_R", -1.0)):
    xs = x * sx
    limit(["Thigh" + s_, "Shin" + s_, "Foot" + s_], sstep(0.82, 0.62, z) * sstep(-0.05, 0.04, xs))
    limit(["Shoulder" + s_], sstep(0.98, 1.14, z) * sstep(-0.02, 0.14, xs))
    limit(["UpperArm" + s_, "Forearm" + s_, "Hand" + s_, "Fingers" + s_, "Thumb" + s_], sstep(0.30, 0.44, xs))

d_muz = rbox(MUZZLE_C, MUZZLE_HALF, MUZZLE_RAD)(x, y, z)
d_head = np.minimum(rbox((0, -0.10, 1.73), (0.30, 0.22, 0.25), 0.12)(x, y, z), d_muz)
m_head = sstep(0.07, 0.0, d_head) * is_body
h = bi["Head"]
trunk = [bi["Hips"], bi["Spine"], bi["Chest"]]
share = np.stack([sstep(0.82, 0.62, z), 1.0 - sstep(0.82, 0.62, z) - sstep(0.95, 1.20, z), sstep(0.95, 1.20, z)], axis=1)


def rebuild():
    """Tete imposee par son masque, membres conserves, le reste au tronc ; somme = 1."""
    global W
    W[:, h] = np.maximum(W[:, h] * sstep(1.30, 1.45, z), m_head)
    W[:, trunk] = 0
    limbs = W.copy()
    limbs[:, h] = 0
    s_l = limbs.sum(axis=1)
    room = 1.0 - W[:, h]
    f_ = np.where(s_l > room, room / np.maximum(s_l, 1e-9), 1.0)
    limbs *= f_[:, None]
    rest = np.clip(room - limbs.sum(axis=1), 0.0, 1.0)
    head_w = W[:, h].copy()
    W = limbs
    W[:, h] = head_w
    W[:, trunk] = share * rest[:, None]

rebuild()

# Lissage des poids le long du maillage (supprime le bruit d'un sommet a l'autre)
ne = len(me.edges)
ev = np.zeros(ne * 2, dtype=np.int32)
me.edges.foreach_get("vertices", ev)
ev = ev.reshape(-1, 2)
ev = ev[is_body[ev[:, 0]] & is_body[ev[:, 1]]]
deg = np.zeros(nv)
np.add.at(deg, ev[:, 0], 1)
np.add.at(deg, ev[:, 1], 1)
for _ in range(4):
    acc = np.zeros_like(W)
    np.add.at(acc, ev[:, 0], W[ev[:, 1]])
    np.add.at(acc, ev[:, 1], W[ev[:, 0]])
    sm_ = np.where(deg[:, None] > 0, acc / np.maximum(deg, 1)[:, None], W)
    W = np.where(is_body[:, None], 0.5 * W + 0.5 * sm_, W)
# la tete reste rigide apres lissage
w_head = np.maximum(W[:, h], m_head)
others = W.copy()
others[:, h] = 0
s_o = others.sum(axis=1)
W = others * np.where(s_o > 1e-6, (1.0 - w_head) / np.maximum(s_o, 1e-6), 0.0)[:, None]
W[:, h] = np.where(s_o > 1e-6, w_head, 1.0)
# Museau : os a part (souffle, reniflement)
m_muz = sstep(0.025, -0.005, d_muz) * is_body * 0.9
W[:, bi["Muzzle"]] = W[:, h] * m_muz
W[:, h] *= (1.0 - m_muz)


def one_hot(mask, bone):
    W[mask] = 0
    W[mask, bi[bone]] = 1.0

PART = {"hoof": 1, "horn": 2, "brow_L": 7, "brow_R": 8, "tail": 9, "tuft": 10, "ear_L": 11, "ear_R": 12}
one_hot((vpart == PART["hoof"]) & (x > 0), "Foot_L")
one_hot((vpart == PART["hoof"]) & (x <= 0), "Foot_R")
one_hot(vpart == PART["horn"], "Head")
one_hot(vpart == PART["brow_L"], "Brow_L")
one_hot(vpart == PART["brow_R"], "Brow_R")
one_hot(vpart == PART["tuft"], f"Tail_{tail_n:02d}")

# Oreilles : la base reste sur la tete, le reste suit l'os de l'oreille
axis = EAR_TIP - EAR_ROOT
for key, sx, bone in (("ear_L", 1.0, "Ear_L"), ("ear_R", -1.0, "Ear_R")):
    sel = vpart == PART[key]
    rel = co[sel] * np.array((sx, 1, 1)) - EAR_ROOT
    te = (rel @ axis) / float(axis @ axis)
    w = sstep(0.02, 0.32, te)
    W[sel] = 0
    W[sel, bi[bone]] = w
    W[sel, h] = 1.0 - w

# Queue : fondu d'un os au suivant le long de la courbe
sp, _ = spline(TAIL_PTS, [0] * len(TAIL_PTS), 20)
sp = np.array([np.array(p) for p in sp])
for i in np.nonzero(vpart == PART["tail"])[0]:
    k = int(np.argmin(((sp - co[i]) ** 2).sum(axis=1)))
    s_ = k / 20.0
    b = min(int(s_), tail_n - 1)
    f = s_ - b
    W[i] = 0
    cur = bi[f"Tail_{b + 1:02d}"]
    if f < 0.5:
        prev = bi["Hips"] if b == 0 else bi[f"Tail_{b:02d}"]
        W[i, cur] = 0.5 + f
        W[i, prev] = 0.5 - f
    elif b < tail_n - 1:
        W[i, cur] = 1.5 - f
        W[i, bi[f"Tail_{b + 2:02d}"]] = f - 0.5
    else:
        W[i, cur] = 1.0

# 4 influences au plus par sommet (limite par defaut d'Unity), somme = 1
idx_sorted = np.argsort(-W, axis=1)
keep = np.zeros_like(W, dtype=bool)
np.put_along_axis(keep, idx_sorted[:, :4], True, axis=1)
W = np.where(keep & (W > 0.015), W, 0.0)
tot = W.sum(axis=1)
no_weight = int((tot < 1e-6).sum())
W[tot < 1e-6, bi["Hips"]] = 1.0
W /= W.sum(axis=1)[:, None]

body.vertex_groups.clear()
groups = [body.vertex_groups.new(name=n) for n in DEFORM]
for b_i, g in enumerate(groups):
    for i in np.nonzero(W[:, b_i] > 0)[0]:
        g.add([int(i)], float(W[i, b_i]), 'REPLACE')

body.parent = rig
body.modifiers.clear()
mod = body.modifiers.new("Armature", 'ARMATURE')
mod.object = rig

# --------------------------------------------------------------------------------------
# Yeux et paupieres : objets parentes a leur os (sans les deplacer)
# --------------------------------------------------------------------------------------
bpy.context.view_layer.update()
for obj_name, bone_name in (("Bull_Eye_L", "Eye_L"), ("Bull_Eye_R", "Eye_R"), ("Bull_Lid_L", "Lid_L"), ("Bull_Lid_R", "Lid_R")):
    ob = bpy.data.objects[obj_name]
    bone = arm_data.bones[bone_name]
    ob.parent = rig
    ob.parent_type = 'BONE'
    ob.parent_bone = bone_name
    ob.matrix_parent_inverse = (rig.matrix_world @ bone.matrix_local @ Matrix.Translation((0, bone.length, 0))).inverted()

# --------------------------------------------------------------------------------------
# Contraintes : regard, IK des jambes
# --------------------------------------------------------------------------------------
for s in ("_L", "_R"):
    c = rig.pose.bones["Eye" + s].constraints.new('DAMPED_TRACK')
    c.name = "Regard"
    c.target = rig
    c.subtarget = "Ctrl_Eye" + s
    c.track_axis = 'TRACK_Y'

    c = rig.pose.bones["Foot" + s].constraints.new('COPY_ROTATION')
    c.name = "Suit le pied IK"
    c.target = rig
    c.subtarget = "IK_Foot" + s
    c.target_space = 'WORLD'
    c.owner_space = 'WORLD'

    ik = rig.pose.bones["Shin" + s].constraints.new('IK')
    ik.name = "IK jambe"
    ik.target = rig
    ik.subtarget = "IK_Foot" + s
    ik.pole_target = rig
    ik.pole_subtarget = "Pole_Knee" + s
    ik.chain_count = 2
    ik.use_stretch = False

# Angle de pole : on garde celui qui laisse la pose de repos intacte
def rest_error():
    bpy.context.view_layer.update()
    err = 0.0
    for n in ("Thigh_L", "Shin_L", "Foot_L", "Thigh_R", "Shin_R", "Foot_R"):
        pb = rig.pose.bones[n]
        rb = arm_data.bones[n]
        err = max(err, (pb.head - rb.head_local).length, (pb.tail - rb.tail_local).length)
    return err

pole_report = {}
for s in ("_L", "_R"):
    ik = rig.pose.bones["Shin" + s].constraints["IK jambe"]
    best = None
    for ang in (-90, 90, 0, 180):
        ik.pole_angle = math.radians(ang)
        e = rest_error()
        if best is None or e < best[1]:
            best = (ang, e)
    ik.pole_angle = math.radians(best[0])
    pole_report[s] = [best[0], round(best[1], 5)]
rest_err = rest_error()

result = {
    "bones_deform": len(DEFORM), "bones_total": len(arm_data.bones),
    "auto_unweighted_verts": unweighted, "verts_without_weight_fixed": no_weight,
    "pole": pole_report, "rest_error_m": round(rest_err, 5),
    "max_influences": int((W > 0).sum(axis=1).max()),
}
