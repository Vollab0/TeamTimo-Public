# Construction de la souris (mob de base) dans Blender : maillage, palettes jour / nuit, squelette.
# A lancer dans Blender :  exec(open(r"...\mouse_build.py", encoding="utf-8").read())
# La souris regarde vers -Y (convention Blender), Z vers le haut, unites en metres.
import bpy
import bmesh
import math
import os
from mathutils import Vector, Matrix, Euler

SRC_DIR = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo\ArtSource\Souris"
TEX_DIR = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo\Assets\Art\Textures\Characters\Souris"
os.makedirs(TEX_DIR, exist_ok=True)

# --------------------------------------------------------------------------------------
# Nettoyage : on ne supprime que ce que ce script cree (et le cube par defaut).
# --------------------------------------------------------------------------------------
if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')

for name in ("Cube", "Mouse_Body", "Mouse_Eyes_Day", "Mouse_Eyes_Night", "Mouse_Rig"):
    ob = bpy.data.objects.get(name)
    if ob:
        bpy.data.objects.remove(ob, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.armatures):
    for block in list(coll):
        if block.users == 0:
            coll.remove(block)

col = bpy.data.collections.get("Mouse")
if col is None:
    col = bpy.data.collections.new("Mouse")
    bpy.context.scene.collection.children.link(col)

# --------------------------------------------------------------------------------------
# Palette : une texture 4x4 cases par version (jour / nuit). Chaque partie du modele a ses UV
# ecrases au centre d'une case -> 1 seul materiau par version, aucun debordement de couleur.
# --------------------------------------------------------------------------------------
PAL = ["fur", "belly", "skin", "ear_in", "nose", "eye_white", "iris", "pupil",
       "whisker", "teeth", "brow", "shine", "fur_dark", "spare1", "spare2", "spare3"]

def srgb(hexstr):
    hexstr = hexstr.lstrip('#')
    return tuple(int(hexstr[i:i + 2], 16) / 255.0 for i in (0, 2, 4))

DAY = {
    "fur": srgb("9c9da7"), "belly": srgb("ecebee"), "skin": srgb("f2a9ae"), "ear_in": srgb("f29aab"),
    "nose": srgb("16161a"), "eye_white": srgb("f7f7f7"), "iris": srgb("3f9a4c"), "pupil": srgb("0c0c0e"),
    "whisker": srgb("f4f4f4"), "teeth": srgb("fbfbf5"), "brow": srgb("6c6d76"), "shine": srgb("ffffff"),
    "fur_dark": srgb("7a7b85"), "spare1": srgb("ff00ff"), "spare2": srgb("ff00ff"), "spare3": srgb("ff00ff"),
}
NIGHT = {
    "fur": srgb("1d1c21"), "belly": srgb("2a292f"), "skin": srgb("9a6b72"), "ear_in": srgb("a8747d"),
    "nose": srgb("060607"), "eye_white": srgb("ff2a1a"), "iris": srgb("ff2a1a"), "pupil": srgb("ff5a3a"),
    "whisker": srgb("8c8a92"), "teeth": srgb("f3f1e6"), "brow": srgb("0c0b0e"), "shine": srgb("ffd0c0"),
    "fur_dark": srgb("131216"), "spare1": srgb("ff00ff"), "spare2": srgb("ff00ff"), "spare3": srgb("ff00ff"),
}

def pal_uv(key):
    i = PAL.index(key)
    return ((i % 4 + 0.5) / 4.0, 1.0 - (i // 4 + 0.5) / 4.0)

def make_palette(name, colors, filename):
    cell = 16
    size = cell * 4
    img = bpy.data.images.get(name)
    if img:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(name, size, size, alpha=False)
    px = [0.0] * (size * size * 4)
    for y in range(size):
        row = 3 - (y // cell)          # la ligne 0 de la palette est en haut de l'image
        for x in range(size):
            c = colors[PAL[row * 4 + x // cell]]
            o = (y * size + x) * 4
            px[o:o + 4] = (c[0], c[1], c[2], 1.0)
    # image 8 bits : les pixels sont deja dans l'espace sRGB de l'image, pas de conversion
    img.pixels = px
    img.filepath_raw = os.path.join(TEX_DIR, filename)
    img.file_format = 'PNG'
    img.save()
    return img

img_day = make_palette("Mouse_Palette_Day", DAY, "Mouse_Palette_Day.png")
img_night = make_palette("Mouse_Palette_Night", NIGHT, "Mouse_Palette_Night.png")

def principled(mat):
    # Blender en francais traduit le nom des noeuds : on cherche par type
    return next(n for n in mat.node_tree.nodes if n.bl_idname == "ShaderNodeBsdfPrincipled")

def make_material(name, image, roughness=0.75):
    mat = bpy.data.materials.get(name)
    if mat:
        bpy.data.materials.remove(mat)
    mat = bpy.data.materials.new(name)
    nt = mat.node_tree
    bsdf = principled(mat)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = 'Closest'
    tex.location = (-320, 300)
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = roughness
    mat.use_fake_user = True
    return mat

mat_day = make_material("Mouse_Day", img_day)
mat_night = make_material("Mouse_Night", img_night)

# Yeux rouges de nuit : materiau emissif a part
mat_glow = bpy.data.materials.get("Mouse_EyeGlow_Night")
if mat_glow:
    bpy.data.materials.remove(mat_glow)
mat_glow = bpy.data.materials.new("Mouse_EyeGlow_Night")
_b = principled(mat_glow)
_b.inputs["Base Color"].default_value = (1.0, 0.02, 0.01, 1.0)
_b.inputs["Emission Color"].default_value = (1.0, 0.0, 0.0, 1.0)
_b.inputs["Emission Strength"].default_value = 2.0
mat_glow.use_fake_user = True

# --------------------------------------------------------------------------------------
# Squelette : positions (espace armature = monde)
# --------------------------------------------------------------------------------------
TAIL_PTS = [Vector((0, 0.265, 0.165)), Vector((0, 0.36, 0.125)), Vector((0, 0.455, 0.125)),
            Vector((0, 0.53, 0.185)), Vector((0, 0.545, 0.285)), Vector((0, 0.49, 0.365))]

BONES = [  # nom, tete, queue, parent
    ("Root",   (0, 0, 0),             (0, 0, 0.08),          None),
    ("Main",   (0, 0.08, 0.17),       (0, 0.08, 0.27),       "Root"),
    ("Body",   (0, 0.20, 0.17),       (0, -0.04, 0.19),      "Main"),
    ("Head",   (0, -0.07, 0.235),     (0, -0.30, 0.265),     "Body"),
    ("Ear_L",  (0.095, -0.15, 0.335), (0.185, -0.15, 0.505), "Head"),
    ("Ear_R",  (-0.095, -0.15, 0.335), (-0.185, -0.15, 0.505), "Head"),
    ("Leg_FL", (0.072, -0.055, 0.15), (0.072, -0.065, 0.02), "Main"),
    ("Leg_FR", (-0.072, -0.055, 0.15), (-0.072, -0.065, 0.02), "Main"),
    ("Leg_BL", (0.10, 0.175, 0.155),  (0.105, 0.155, 0.02),  "Main"),
    ("Leg_BR", (-0.10, 0.175, 0.155), (-0.105, 0.155, 0.02), "Main"),
]
for i in range(5):
    BONES.append((f"Tail_{i + 1:02d}", tuple(TAIL_PTS[i]), tuple(TAIL_PTS[i + 1]),
                  "Body" if i == 0 else f"Tail_{i:02d}"))
GROUPS = [b[0] for b in BONES]

# --------------------------------------------------------------------------------------
# Outils de modelisation (bmesh)
# --------------------------------------------------------------------------------------
class Builder:
    def __init__(self, groups):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.dl = self.bm.verts.layers.deform.verify()
        self.groups = groups
        self.mat_index = 0

    def _finish(self, verts, color, group):
        u, v = pal_uv(color)
        faces = set()
        gi = self.groups.index(group) if group else None
        for vert in verts:
            if gi is not None:
                vert[self.dl][gi] = 1.0
            faces.update(vert.link_faces)
        for f in faces:
            f.smooth = True
            f.material_index = self.mat_index
            for loop in f.loops:
                loop[self.uv].uv = (u, v)
        return verts

    def sphere(self, center, radii, color, group, segs=14, rings=9, rot=None):
        m = Matrix.Translation(Vector(center))
        if rot is not None:
            m = m @ rot.to_matrix().to_4x4()
        m = m @ Matrix.Diagonal((radii[0], radii[1], radii[2], 1.0))
        ret = bmesh.ops.create_uvsphere(self.bm, u_segments=segs, v_segments=rings, radius=1.0, matrix=m)
        return self._finish(ret["verts"], color, group)

    def sphere_m(self, matrix, color, group, segs=14, rings=9):
        ret = bmesh.ops.create_uvsphere(self.bm, u_segments=segs, v_segments=rings, radius=1.0, matrix=matrix)
        return self._finish(ret["verts"], color, group)

    def cone(self, base, tip, r_base, r_tip, color, group, segs=6):
        base = Vector(base)
        tip = Vector(tip)
        axis = tip - base
        depth = axis.length
        q = axis.normalized().to_track_quat('Z', 'Y')
        m = Matrix.Translation((base + tip) / 2) @ q.to_matrix().to_4x4()
        ret = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=True, segments=segs,
                                    radius1=r_base, radius2=r_tip, depth=depth, matrix=m)
        return self._finish(ret["verts"], color, group)

    def tube(self, points, radii, color, weight_fn, segs=8):
        """Tube le long d'une liste de points ; weight_fn(i) -> {groupe: poids} pour l'anneau i."""
        u, v = pal_uv(color)
        rings = []
        n = len(points)
        for i, p in enumerate(points):
            if i == 0:
                t = points[1] - points[0]
            elif i == n - 1:
                t = points[-1] - points[-2]
            else:
                t = points[i + 1] - points[i - 1]
            t.normalize()
            side = Vector((1, 0, 0))
            up = t.cross(side).normalized()
            ring = []
            for s in range(segs):
                a = 2 * math.pi * s / segs
                pos = p + (side * math.cos(a) + up * math.sin(a)) * radii[i]
                vert = self.bm.verts.new(pos)
                for g, w in weight_fn(i).items():
                    vert[self.dl][self.groups.index(g)] = w
                ring.append(vert)
            rings.append(ring)
        faces = []
        for i in range(n - 1):
            for s in range(segs):
                a, b = rings[i][s], rings[i][(s + 1) % segs]
                c, d = rings[i + 1][(s + 1) % segs], rings[i + 1][s]
                faces.append(self.bm.faces.new((a, b, c, d)))
        faces.append(self.bm.faces.new(list(reversed(rings[0]))))
        faces.append(self.bm.faces.new(rings[-1]))
        for f in faces:
            f.smooth = True
            f.material_index = self.mat_index
            for loop in f.loops:
                loop[self.uv].uv = (u, v)

    def to_object(self, name, materials):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        ob = bpy.data.objects.new(name, me)
        col.objects.link(ob)
        for g in self.groups:
            ob.vertex_groups.new(name=g)
        for m in materials:
            me.materials.append(m)
        return ob

def rot_xyz(x=0.0, y=0.0, z=0.0):
    return Euler((math.radians(x), math.radians(y), math.radians(z)), 'XYZ')

# --------------------------------------------------------------------------------------
# CORPS (un seul maillage skinne)
# --------------------------------------------------------------------------------------
B = Builder(GROUPS)

# Tronc + ventre clair
B.sphere((0, 0.10, 0.175), (0.132, 0.195, 0.128), "fur", "Body", segs=18, rings=12)
B.sphere((0, 0.065, 0.150), (0.112, 0.165, 0.110), "belly", "Body", segs=16, rings=10)

# Tete + bas du visage clair (joues / museau)
B.sphere((0, -0.165, 0.265), (0.152, 0.142, 0.132), "fur", "Head", segs=18, rings=12)
B.sphere((0, -0.222, 0.216), (0.104, 0.090, 0.068), "belly", "Head", segs=16, rings=10)
B.sphere((0, -0.287, 0.228), (0.062, 0.050, 0.046), "belly", "Head", segs=12, rings=8)   # museau
B.sphere((0, -0.336, 0.246), (0.023, 0.018, 0.017), "nose", "Head", segs=10, rings=6)   # truffe

# Oreilles (disque gris + interieur rose)
for side, grp in ((1, "Ear_L"), (-1, "Ear_R")):
    m_ear = Matrix.Translation((side * 0.142, -0.148, 0.418)) @ rot_xyz(8, side * 26, side * -14).to_matrix().to_4x4()
    B.sphere_m(m_ear @ Matrix.Diagonal((0.108, 0.022, 0.108, 1)), "fur", grp, segs=18, rings=8)
    B.sphere_m(m_ear @ Matrix.Translation((0, -0.012, -0.004)) @ Matrix.Diagonal((0.083, 0.015, 0.083, 1)),
               "ear_in", grp, segs=18, rings=8)

# Pattes avant
for side, grp in ((1, "Leg_FL"), (-1, "Leg_FR")):
    B.sphere((side * 0.072, -0.058, 0.090), (0.038, 0.040, 0.075), "fur", grp, segs=10, rings=7)
    B.sphere((side * 0.074, -0.090, 0.022), (0.034, 0.052, 0.022), "skin", grp, segs=10, rings=6)
    for k in (-1, 0, 1):   # doigts
        B.sphere((side * 0.074 + k * 0.021, -0.136, 0.015), (0.012, 0.018, 0.012), "skin", grp, segs=6, rings=4)

# Pattes arriere (cuisse + pied)
for side, grp in ((1, "Leg_BL"), (-1, "Leg_BR")):
    B.sphere((side * 0.098, 0.165, 0.120), (0.058, 0.082, 0.088), "fur", grp, segs=12, rings=8)
    B.sphere((side * 0.108, 0.118, 0.022), (0.036, 0.066, 0.022), "skin", grp, segs=10, rings=6)
    for k in (-1, 0, 1):
        B.sphere((side * 0.108 + k * 0.022, 0.058, 0.015), (0.012, 0.019, 0.012), "skin", grp, segs=6, rings=4)

# Moustaches (fines pointes blanches)
for side in (1, -1):
    for dz, dy, lz in ((0.018, 0.02, 0.035), (0.0, 0.0, 0.0), (-0.016, 0.02, -0.03)):
        base = Vector((side * 0.045, -0.285, 0.222 + dz))
        tip = Vector((side * 0.205, -0.270 + dy, 0.222 + dz + lz))
        B.cone(base, tip, 0.0035, 0.0008, "whisker", "Head", segs=4)

# Queue : tube lisse le long de 5 os
def catmull(p0, p1, p2, p3, t):
    return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                  + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t)

SUB = 4
tail_points, tail_w = [], []
ext = [TAIL_PTS[0] + (TAIL_PTS[0] - TAIL_PTS[1])] + TAIL_PTS + [TAIL_PTS[-1] + (TAIL_PTS[-1] - TAIL_PTS[-2])]
for i in range(5):
    for s in range(SUB):
        f = s / SUB
        tail_points.append(catmull(ext[i], ext[i + 1], ext[i + 2], ext[i + 3], f))
        w = {}
        if f < 0.5:
            w[f"Tail_{i + 1:02d}"] = 0.5 + f
            w["Body" if i == 0 else f"Tail_{i:02d}"] = 0.5 - f
        else:
            w[f"Tail_{i + 1:02d}"] = 1.5 - f
            if i < 4:
                w[f"Tail_{i + 2:02d}"] = f - 0.5
            else:
                w[f"Tail_{i + 1:02d}"] = 1.0
        tail_w.append(w)
tail_points.append(TAIL_PTS[-1].copy())
tail_w.append({"Tail_05": 1.0})
n_t = len(tail_points)
tail_r = [0.021 - 0.015 * (i / (n_t - 1)) for i in range(n_t)]
B.tube(tail_points, tail_r, "skin", lambda i: tail_w[i], segs=8)

body = B.to_object("Mouse_Body", [mat_day])

# --------------------------------------------------------------------------------------
# YEUX DE JOUR (objet a part, parente a l'os Head)
# --------------------------------------------------------------------------------------
def eye_frame(side):
    d = Vector((side * 0.34, -1.0, 0.20)).normalized()
    c = Vector((side * 0.058, -0.293, 0.297)) - d * 0.010
    q = d.to_track_quat('Z', 'Y')
    return c, d, q

E = Builder([])
for side in (1, -1):
    c, d, q = eye_frame(side)
    R = q.to_matrix().to_4x4()
    E.sphere_m(Matrix.Translation(c) @ R @ Matrix.Diagonal((0.037, 0.042, 0.024, 1)), "eye_white", None, segs=14, rings=9)
    E.sphere_m(Matrix.Translation(c + d * 0.013) @ R @ Matrix.Diagonal((0.030, 0.034, 0.015, 1)), "iris", None, segs=14, rings=8)
    E.sphere_m(Matrix.Translation(c + d * 0.021) @ R @ Matrix.Diagonal((0.020, 0.024, 0.010, 1)), "pupil", None, segs=12, rings=6)
    up = R.to_3x3() @ Vector((0, 1, 0))
    rt = R.to_3x3() @ Vector((1, 0, 0))
    E.sphere_m(Matrix.Translation(c + d * 0.029 + up * 0.011 + rt * 0.008) @ R @ Matrix.Diagonal((0.007, 0.008, 0.005, 1)),
               "shine", None, segs=8, rings=5)
eyes_day = E.to_object("Mouse_Eyes_Day", [mat_day])

# --------------------------------------------------------------------------------------
# VISAGE DE NUIT (objet a part) : yeux rouges emissifs en colere, sourcils, dents
# --------------------------------------------------------------------------------------
N = Builder([])
for side in (1, -1):
    c, d, q = eye_frame(side)
    R = q.to_matrix().to_4x4()
    roll = Matrix.Rotation(math.radians(side * 26), 4, 'Z')   # coin interieur plus bas = regard mauvais
    N.mat_index = 0
    N.sphere_m(Matrix.Translation(c + d * 0.004) @ R @ roll @ Matrix.Diagonal((0.044, 0.026, 0.026, 1)),
               "eye_white", None, segs=14, rings=8)
    N.sphere_m(Matrix.Translation(c + d * 0.024) @ R @ roll @ Matrix.Diagonal((0.015, 0.011, 0.008, 1)),
               "pupil", None, segs=10, rings=6)
    # Sourcil sombre incline qui coupe le haut de l'oeil
    N.mat_index = 1
    up = (R @ roll).to_3x3() @ Vector((0, 1, 0))
    N.sphere_m(Matrix.Translation(c + d * 0.010 + up * 0.034) @ R @ roll @ Matrix.Diagonal((0.056, 0.022, 0.028, 1)),
               "brow", None, segs=12, rings=7)

# Gueule sombre + dents pointues sous le museau
N.mat_index = 1
N.sphere((0, -0.292, 0.196), (0.060, 0.036, 0.020), "brow", None, segs=12, rings=6)
for i in range(7):
    a = (i - 3) / 3.0                       # -1 .. 1
    x = a * 0.050
    y = -0.325 + (a * a) * 0.026
    N.cone((x, y, 0.207), (x, y - 0.002, 0.186), 0.0085, 0.0008, "teeth", None, segs=5)
for i in range(6):
    a = (i - 2.5) / 2.5
    x = a * 0.042
    y = -0.320 + (a * a) * 0.024
    N.cone((x, y, 0.183), (x, y - 0.002, 0.201), 0.0075, 0.0008, "teeth", None, segs=5)
eyes_night = N.to_object("Mouse_Eyes_Night", [mat_glow, mat_night])

# --------------------------------------------------------------------------------------
# SQUELETTE
# --------------------------------------------------------------------------------------
arm_data = bpy.data.armatures.new("Mouse_Rig")
rig = bpy.data.objects.new("Mouse_Rig", arm_data)
col.objects.link(rig)
bpy.context.view_layer.objects.active = rig
for o in bpy.context.selected_objects:
    o.select_set(False)
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name, head, tail, parent in BONES:
    eb = arm_data.edit_bones.new(name)
    eb.head = head
    eb.tail = tail
    eb.roll = 0.0
    if parent:
        eb.parent = arm_data.edit_bones[parent]
        eb.use_connect = name.startswith("Tail_") and name != "Tail_01"
bpy.ops.object.mode_set(mode='OBJECT')
arm_data.display_type = 'STICK'
rig.show_in_front = True
for pb in rig.pose.bones:
    pb.rotation_mode = 'QUATERNION'

# Corps : skinning par l'armature
body.parent = rig
mod = body.modifiers.new("Armature", 'ARMATURE')
mod.object = rig

# Yeux : parentes a l'os Head (sans les deplacer)
bpy.context.view_layer.update()
head_bone = arm_data.bones["Head"]
bone_tail_world = rig.matrix_world @ head_bone.matrix_local @ Matrix.Translation((0, head_bone.length, 0))
for ob in (eyes_day, eyes_night):
    ob.parent = rig
    ob.parent_type = 'BONE'
    ob.parent_bone = "Head"
    ob.matrix_parent_inverse = bone_tail_world.inverted()

# Par defaut dans Blender : version de jour visible, visage de nuit masque
eyes_night.hide_viewport = False
eyes_night.hide_set(True)
eyes_night.hide_render = True

bpy.context.scene.render.fps = 30

result = {
    "body_verts": len(body.data.vertices),
    "body_tris": sum(len(p.vertices) - 2 for p in body.data.polygons),
    "eyes_day_tris": sum(len(p.vertices) - 2 for p in eyes_day.data.polygons),
    "eyes_night_tris": sum(len(p.vertices) - 2 for p in eyes_night.data.polygons),
    "bones": [b.name for b in arm_data.bones],
}
