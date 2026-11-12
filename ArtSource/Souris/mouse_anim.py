# Animations de la souris : Idle, Walk, Run, Attack, Hit, Die, Sniff (30 images/s, sur place).
# A lancer apres mouse_build.py. Chaque animation est une Action Blender ; l'export FBX les prend toutes.
#
# Conventions (espace de l'armature, la souris regarde vers -Y, sa gauche est +X) :
#   rot = (rx, ry, rz) en degres autour des axes de l'armature, appliquee au pivot de l'os
#     - tete / corps : rx > 0 = museau vers le bas ; rz > 0 = tourne a gauche ; ry > 0 = penche a gauche
#     - pattes       : rx > 0 = pied vers l'arriere ; rx < 0 = pied vers l'avant
#     - queue        : rx > 0 = se releve ; rz > 0 = part vers la droite de la souris
#     - oreilles     : rx > 0 = vers l'avant ; rx < 0 = couchees en arriere
#   loc = (dx, dy, dz) en metres (dy < 0 = vers l'avant, dz > 0 = vers le haut)
import bpy
import math
from mathutils import Vector, Euler, Quaternion

FPS = 30
rig = bpy.data.objects["Mouse_Rig"]
arm = rig.data
scene = bpy.context.scene
scene.render.fps = FPS

REST = {b.name: b.matrix_local.to_3x3() for b in arm.bones}
REST_INV = {k: v.inverted() for k, v in REST.items()}
ALL_BONES = [b.name for b in arm.bones]
TAIL = [f"Tail_{i:02d}" for i in range(1, 6)]
LEGS_F = ["Leg_FL", "Leg_FR"]
LEGS_B = ["Leg_BL", "Leg_BR"]

def local_quat(bone, rot_deg):
    rw = Euler([math.radians(a) for a in rot_deg], 'XYZ').to_matrix()
    return (REST_INV[bone] @ rw @ REST[bone]).to_quaternion()

def local_loc(bone, d):
    return REST_INV[bone] @ Vector(d)

class Clip:
    """Pistes par os : {os: {image: (rot, loc, scale)}} ; rot / loc / scale peuvent etre None (= repos)."""
    def __init__(self, name, length, loop):
        self.name, self.length, self.loop = name, length, loop
        self.tracks = {}

    def key(self, bone, frame, rot=None, loc=None, scale=None):
        self.tracks.setdefault(bone, {})[frame] = (rot or (0, 0, 0), loc or (0, 0, 0), scale or (1, 1, 1))

    def sample(self, bone, fn, step=1):
        """fn(t) -> dict(rot=, loc=, scale=) avec t dans [0, 1] ; echantillonne toutes les `step` images."""
        frames = list(range(0, self.length, step)) + [self.length]
        for f in frames:
            t = f / self.length
            if self.loop and f == self.length:
                t = 0.0          # derniere image = premiere image (boucle parfaite)
            self.key(bone, f, **fn(t))

    def bake(self):
        old = bpy.data.actions.get(self.name)
        if old:
            bpy.data.actions.remove(old)
        act = bpy.data.actions.new(self.name)
        act.use_fake_user = True
        ad = rig.animation_data or rig.animation_data_create()
        ad.action = act
        slot = act.slots.new(id_type='OBJECT', name=rig.name)
        ad.action_slot = slot
        for bone in ALL_BONES:
            pb = rig.pose.bones[bone]
            pb.rotation_mode = 'QUATERNION'
            keys = self.tracks.get(bone) or {0: ((0, 0, 0), (0, 0, 0), (1, 1, 1))}
            if 0 not in keys:
                raise ValueError(f"{self.name}: l'os {bone} n'a pas de cle a l'image 0")
            prev = None
            for frame in sorted(keys):
                rot, loc, scale = keys[frame]
                q = local_quat(bone, rot)
                if prev is not None:
                    q.make_compatible(prev)
                prev = q.copy()
                pb.rotation_quaternion = q
                pb.location = local_loc(bone, loc)
                pb.scale = scale
                for path in ("rotation_quaternion", "location", "scale"):
                    pb.keyframe_insert(path, frame=frame, group=bone)
        act.use_frame_range = True
        act.frame_start = 0
        act.frame_end = self.length
        act.use_cyclic = self.loop
        return act

TWO_PI = 2 * math.pi
def s(t, freq=1.0, phase=0.0):
    return math.sin(TWO_PI * (t * freq + phase))

clips = []

# ------------------------------------------------------------------------------------ Idle
c = Clip("Idle", 60, True)
c.sample("Body", lambda t: dict(loc=(0, 0, 0.005 * (0.5 - 0.5 * math.cos(TWO_PI * t))),
                                scale=(1 + 0.02 * (0.5 - 0.5 * math.cos(TWO_PI * t)), 1,
                                       1 + 0.02 * (0.5 - 0.5 * math.cos(TWO_PI * t)))), step=3)
c.sample("Head", lambda t: dict(rot=(-2.5 * (0.5 - 0.5 * math.cos(TWO_PI * t)), 0, 4 * s(t))), step=3)
for i, b in enumerate(TAIL):
    c.sample(b, lambda t, i=i: dict(rot=(1.5 * s(t, 1, -0.1 * i), 0, (5 + 2.5 * i) * s(t, 1, -0.12 * i))), step=3)
# petits tressaillements d'oreilles
for b, start, sign in (("Ear_L", 34, 1), ("Ear_R", 10, -1)):
    c.key(b, 0)
    c.key(b, start)
    c.key(b, start + 3, rot=(-6, sign * -12, 0))
    c.key(b, start + 6, rot=(2, sign * 4, 0))
    c.key(b, start + 9)
    c.key(b, 60)
clips.append(c)

# ------------------------------------------------------------------------------------ Walk (trot : diagonales ensemble)
c = Clip("Walk", 16, True)
def leg_cycle(amp, lift, phase):
    def fn(t):
        th = TWO_PI * (t + phase)
        return dict(rot=(amp * math.sin(th), 0, 0), loc=(0, 0, lift * max(0.0, -math.cos(th))))
    return fn
c.sample("Leg_FL", leg_cycle(30, 0.022, 0.0))
c.sample("Leg_BR", leg_cycle(26, 0.020, 0.0))
c.sample("Leg_FR", leg_cycle(30, 0.022, 0.5))
c.sample("Leg_BL", leg_cycle(26, 0.020, 0.5))
c.sample("Body", lambda t: dict(rot=(0, 2.5 * s(t), 3 * s(t, 1, 0.25)), loc=(0, 0, 0.006 * s(t, 2))))
c.sample("Head", lambda t: dict(rot=(2 * s(t, 2, 0.15), -1.5 * s(t), -2.5 * s(t, 1, 0.25))))
for b, sign in (("Ear_L", 1), ("Ear_R", -1)):
    c.sample(b, lambda t, sign=sign: dict(rot=(-3 + 3 * s(t, 2, 0.3), sign * 3 * s(t, 2, 0.3), 0)))
for i, b in enumerate(TAIL):
    c.sample(b, lambda t, i=i: dict(rot=(2 * s(t, 2, -0.1 * i), 0, (7 + 3 * i) * s(t, 1, 0.5 - 0.13 * i))))
clips.append(c)

# ------------------------------------------------------------------------------------ Run (bonds : avant ensemble, arriere ensemble)
c = Clip("Run", 12, True)
def run_leg(amp, lift, phase, side_phase):
    def fn(t):
        th = TWO_PI * (t + phase + side_phase)
        return dict(rot=(amp * math.sin(th), 0, 0), loc=(0, 0, lift * max(0.0, -math.cos(th))))
    return fn
c.sample("Leg_FL", run_leg(44, 0.03, 0.0, 0.0))
c.sample("Leg_FR", run_leg(44, 0.03, 0.0, 0.06))
c.sample("Leg_BL", run_leg(40, 0.03, 0.45, 0.0))
c.sample("Leg_BR", run_leg(40, 0.03, 0.45, 0.06))
c.sample("Main", lambda t: dict(loc=(0, 0, 0.022 * max(0.0, s(t, 1, 0.15)))))
c.sample("Body", lambda t: dict(rot=(4 + 7 * s(t, 1, 0.05), 0, 0), loc=(0, 0, 0.004 * s(t, 1, 0.3)),
                                scale=(1, 1 + 0.04 * s(t, 1, 0.25), 1)))
c.sample("Head", lambda t: dict(rot=(-8 - 6 * s(t, 1, 0.1), 0, 0)))
for b, sign in (("Ear_L", 1), ("Ear_R", -1)):
    c.sample(b, lambda t, sign=sign: dict(rot=(-26 + 7 * s(t, 1, 0.35), sign * (6 + 4 * s(t, 1, 0.35)), 0)))
for i, b in enumerate(TAIL):
    base = (-6, -20, -24, -22, -16)[i]          # la queue se tend vers l'arriere
    c.sample(b, lambda t, i=i, base=base: dict(rot=(base + (3 + 2 * i) * s(t, 1, 0.2 - 0.12 * i), 0,
                                                    (2 + i) * s(t, 1, -0.1 * i))))
clips.append(c)

# ------------------------------------------------------------------------------------ Attack (morsure ; impact a l'image 9 = 0,30 s)
c = Clip("Attack", 20, False)
for f, loc in ((0, None), (5, (0, 0.045, -0.012)), (9, (0, -0.13, 0.018)), (12, (0, -0.11, 0.008)), (20, None)):
    c.key("Main", f, loc=loc)
for f, rot, sc in ((0, None, None), (5, (-9, 0, 0), (1.04, 0.94, 1.04)), (9, (7, 0, 0), (0.96, 1.08, 0.96)),
                   (12, (5, 0, 0), None), (20, None, None)):
    c.key("Body", f, rot=rot, scale=sc)
for f, rot in ((0, None), (5, (-20, 0, 0)), (8, (-6, 0, 0)), (10, (22, 0, 0)), (13, (16, 0, 0)), (20, None)):
    c.key("Head", f, rot=rot)
for b in LEGS_F:
    for f, rot, loc in ((0, None, None), (5, (14, 0, 0), None), (9, (-42, 0, 0), (0, 0, 0.03)),
                        (12, (-30, 0, 0), None), (20, None, None)):
        c.key(b, f, rot=rot, loc=loc)
for b in LEGS_B:
    for f, rot in ((0, None), (5, (-16, 0, 0)), (9, (34, 0, 0)), (12, (24, 0, 0)), (20, None)):
        c.key(b, f, rot=rot)
for b, sign in (("Ear_L", 1), ("Ear_R", -1)):
    for f, rot in ((0, None), (5, (-28, sign * 8, 0)), (9, (-34, sign * 10, 0)), (13, (10, 0, 0)), (20, None)):
        c.key(b, f, rot=rot)
for i, b in enumerate(TAIL):
    for f, rot in ((0, None), (5, (10, 0, 0)), (9, (-14, 0, 0)), (14, (6, 0, 0)), (20, None)):
        c.key(b, f, rot=rot)
clips.append(c)

# ------------------------------------------------------------------------------------ Hit (recul)
c = Clip("Hit", 12, False)
for f, loc in ((0, None), (3, (0, 0.07, 0.035)), (7, (0, 0.04, 0.0)), (12, None)):
    c.key("Main", f, loc=loc)
for f, rot, sc in ((0, None, None), (3, (-14, 0, 6), (1.06, 0.92, 1.06)), (7, (4, 0, -2), (0.97, 1.04, 0.97)),
                   (12, None, None)):
    c.key("Body", f, rot=rot, scale=sc)
for f, rot in ((0, None), (3, (-24, 6, 10)), (7, (8, 0, -4)), (12, None)):
    c.key("Head", f, rot=rot)
for b in LEGS_F:
    for f, rot, loc in ((0, None, None), (3, (-30, 0, 0), (0, 0, 0.02)), (7, (8, 0, 0), None), (12, None, None)):
        c.key(b, f, rot=rot, loc=loc)
for b in LEGS_B:
    for f, rot in ((0, None), (3, (-12, 0, 0)), (7, (6, 0, 0)), (12, None)):
        c.key(b, f, rot=rot)
for b, sign in (("Ear_L", 1), ("Ear_R", -1)):
    for f, rot in ((0, None), (3, (-36, sign * 14, 0)), (7, (10, sign * -4, 0)), (12, None)):
        c.key(b, f, rot=rot)
for i, b in enumerate(TAIL):
    for f, rot in ((0, None), (3, (14, 0, 0)), (8, (-8, 0, 0)), (12, None)):
        c.key(b, f, rot=rot)
clips.append(c)

# ------------------------------------------------------------------------------------ Die (sursaut puis tombe sur le flanc ; garde la derniere image)
c = Clip("Die", 26, False)
for f, rot, loc in ((0, None, None), (4, (-10, 0, 0), (0, 0.03, 0.09)), (9, (0, 55, 0), (0, 0.04, 0.05)),
                    (13, (0, 92, 0), (0, 0.04, -0.045)), (16, (0, 84, 0), (0, 0.04, -0.03)),
                    (20, (0, 90, 0), (0, 0.04, -0.045)), (26, (0, 90, 0), (0, 0.04, -0.045))):
    c.key("Main", f, rot=rot, loc=loc)
for f, rot, sc in ((0, None, None), (4, (-8, 0, 0), (0.95, 1.08, 0.95)), (13, (0, 0, 0), (1.06, 0.95, 1.0)),
                   (20, None, None), (26, None, None)):
    c.key("Body", f, rot=rot, scale=sc)
for f, rot in ((0, None), (4, (-26, 0, 0)), (13, (6, 0, -10)), (17, (-4, 0, -18)), (22, (0, 0, -14)), (26, (0, 0, -14))):
    c.key("Head", f, rot=rot)
for b in LEGS_F:
    for f, rot in ((0, None), (4, (-36, 0, 0)), (13, (-22, 0, 0)), (17, (-30, 0, 0)), (21, (-16, 0, 0)), (26, (-18, 0, 0))):
        c.key(b, f, rot=rot)
for b in LEGS_B:
    for f, rot in ((0, None), (4, (30, 0, 0)), (13, (20, 0, 0)), (17, (30, 0, 0)), (21, (14, 0, 0)), (26, (16, 0, 0))):
        c.key(b, f, rot=rot)
for b, sign in (("Ear_L", 1), ("Ear_R", -1)):
    for f, rot in ((0, None), (4, (-30, sign * 10, 0)), (13, (14, sign * 18, 0)), (20, (-8, sign * 22, 0)), (26, (-8, sign * 22, 0))):
        c.key(b, f, rot=rot)
for i, b in enumerate(TAIL):
    flat = (-4, -22, -30, -34, -30)[i]
    for f, rot in ((0, None), (4, (16, 0, 0)), (13, (flat * 0.6, 0, 0)), (20, (flat, 0, 4)), (26, (flat, 0, 4))):
        c.key(b, f, rot=rot)
clips.append(c)

# ------------------------------------------------------------------------------------ Sniff (reniflement pendant la recherche)
c = Clip("Sniff", 40, True)
c.sample("Body", lambda t: dict(rot=(-7 + 1.5 * s(t, 4), 0, 5 * s(t)), loc=(0, 0, 0.004)), step=1)
c.sample("Head", lambda t: dict(rot=(-18 + 5 * s(t, 5), 0, 16 * s(t))), step=1)
for b in LEGS_F:
    c.sample(b, lambda t: dict(rot=(-5, 0, 0)), step=8)
for b, sign in (("Ear_L", 1), ("Ear_R", -1)):
    c.sample(b, lambda t, sign=sign: dict(rot=(12 + 5 * s(t, 3, 0.1 * sign), sign * -6, 0)), step=1)
for i, b in enumerate(TAIL):
    c.sample(b, lambda t, i=i: dict(rot=(4, 0, (4 + 2 * i) * s(t, 1, -0.12 * i))), step=2)
clips.append(c)

out = {}
for c in clips:
    act = c.bake()
    out[c.name] = {"frames": c.length, "loop": c.loop, "seconds": round(c.length / FPS, 3)}

# Laisse l'Idle affichee
rig.animation_data.action = bpy.data.actions["Idle"]
rig.animation_data.action_slot = bpy.data.actions["Idle"].slots[0]
scene.frame_start = 0
scene.frame_end = 60
scene.frame_set(0)
result = out
