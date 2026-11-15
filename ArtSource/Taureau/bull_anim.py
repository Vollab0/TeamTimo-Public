# Animations du taureau (30 images/s, sur place) : Idle, Walk, Turn, Prepare, Charge, Impact, Brake, Stun, WakeUp, Hit, Die
# (+ Base : pose de repos sur une image, premiere action par ordre alphabetique = pose par defaut du FBX).
# A lancer apres bull_04_rig.py. Chaque animation est une Action Blender ; l'export FBX les prend toutes.
#
# Conventions (espace de l'armature : le taureau regarde vers -Y, sa gauche est +X, Z vers le haut) :
#   rot = (rx, ry, rz) en degres autour des axes de l'armature, au pivot de l'os (axes portes par le parent)
#     - rx > 0 : bascule vers l'avant (museau / torse vers le bas ; pied : pointe vers le bas ; queue : se releve)
#     - ry > 0 : penche vers sa gauche (+X) ; bras gauche : s'abaisse
#     - rz > 0 : tourne vers sa gauche
#     - paupieres (Lid) : rx > 0 = se ferme (~65 = fermee)
#   Bras (UpperArm) : ordre Y puis X (d'abord abaisser le bras, puis le balancer d'avant en arriere ; rx < 0 = vers l'avant)
#   loc = (dx, dy, dz) en metres (dy < 0 = vers l'avant) ; scale = (largeur X, profondeur Y, hauteur Z)
#   Un nom sans suffixe (ex. "UpperArm") s'applique aux deux cotes, en miroir. "_L" = gauche, "_R" = droite.
#   Jambes : on anime IK_Foot_L / IK_Foot_R (position et inclinaison du sabot) et Hips ; les genoux suivent.
#   Regard : Ctrl_Eyes (les deux yeux) ou Ctrl_Eye_L / Ctrl_Eye_R (un seul).
import bpy
import math
from mathutils import Vector, Euler

FPS = 30
rig = bpy.data.objects["Bull_Rig"]
arm = rig.data
scene = bpy.context.scene
scene.render.fps = FPS

REST = {b.name: b.matrix_local.to_3x3() for b in arm.bones}
REST_INV = {k: v.inverted() for k, v in REST.items()}
ALL_BONES = [b.name for b in arm.bones]
SIDED = {b.name[:-2] for b in arm.bones if b.name.endswith("_L")}
ORDER = {"UpperArm_L": 'YXZ', "UpperArm_R": 'YXZ'}
TAIL = [n for n in ALL_BONES if n.startswith("Tail_")]
TWO_PI = 2 * math.pi


def local_quat(bone, rot_deg):
    rw = Euler([math.radians(a) for a in rot_deg], ORDER.get(bone, 'XYZ')).to_matrix()
    return (REST_INV[bone] @ rw @ REST[bone]).to_quaternion()


def local_loc(bone, d):
    return REST_INV[bone] @ Vector(d)


def local_scale(bone, s_arm):
    """Echelle donnee selon les axes de l'armature -> axes locaux de l'os (axe dominant)."""
    m = REST[bone]
    out = []
    for i in range(3):
        colv = [abs(m[j][i]) for j in range(3)]
        out.append(s_arm[colv.index(max(colv))])
    return out


def mirror(d):
    out = {}
    if d.get("rot") is not None:
        r = d["rot"]
        out["rot"] = (r[0], -r[1], -r[2])
    if d.get("loc") is not None:
        l = d["loc"]
        out["loc"] = (-l[0], l[1], l[2])
    if d.get("scale") is not None:
        out["scale"] = d["scale"]
    return out


def expand(pose):
    """Developpe les noms sans suffixe en _L (tel quel) et _R (miroir)."""
    out = {}
    for name, d in pose.items():
        if name in SIDED:
            out.setdefault(name + "_L", d)
            out.setdefault(name + "_R", mirror(d))
        else:
            out[name] = d
    return out


def add(a, b):
    return tuple(x + y for x, y in zip(a, b))


class Clip:
    def __init__(self, name, length, loop):
        self.name, self.length, self.loop = name, length, loop
        self.tracks = {}

    def key(self, bone, frame, rot=None, loc=None, scale=None):
        self.tracks.setdefault(bone, {})[frame] = (rot or (0, 0, 0), loc or (0, 0, 0), scale or (1, 1, 1))

    def pose(self, frame, pose):
        """Pose complete a l'image `frame` : les os absents du dictionnaire sont au repos."""
        full = expand(pose)
        for bone in ALL_BONES:
            self.key(bone, frame, **full.get(bone, {}))

    def sample(self, fn, step=1):
        """fn(t) -> pose (dictionnaire) avec t dans [0, 1[ ; une cle toutes les `step` images."""
        frames = list(range(0, self.length, step)) + [self.length]
        for f in frames:
            t = f / self.length
            if self.loop and f == self.length:
                t = 0.0                      # derniere image = premiere (boucle parfaite)
            self.pose(f, fn(t))

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
        frames_all = sorted({f for tr in self.tracks.values() for f in tr})
        for bone in ALL_BONES:
            pb = rig.pose.bones[bone]
            pb.rotation_mode = 'QUATERNION'
            keys = dict(self.tracks.get(bone) or {})
            if 0 not in keys:
                keys[0] = ((0, 0, 0), (0, 0, 0), (1, 1, 1))
            prev = None
            for frame in sorted(keys):
                rot, loc, scale = keys[frame]
                q = local_quat(bone, rot)
                if prev is not None:
                    q.make_compatible(prev)
                prev = q.copy()
                pb.rotation_quaternion = q
                pb.location = local_loc(bone, loc)
                pb.scale = local_scale(bone, scale)
                for path in ("rotation_quaternion", "location", "scale"):
                    pb.keyframe_insert(path, frame=frame, group=bone)
        act.use_frame_range = True
        act.frame_start = 0
        act.frame_end = self.length
        act.use_cyclic = self.loop
        return act


def s(t, freq=1.0, phase=0.0):
    return math.sin(TWO_PI * (t * freq + phase))


def c01(t):
    return 0.5 - 0.5 * math.cos(TWO_PI * t)


def bump(t, center, width):
    """Bosse lisse 0 -> 1 -> 0 autour de `center` (cyclique), de demi-largeur `width`."""
    d = ((t - center + 0.5) % 1.0) - 0.5
    x = max(0.0, 1.0 - abs(d) / width)
    return x * x * (3 - 2 * x)


def smooth(u):
    u = min(1.0, max(0.0, u))
    return u * u * (3 - 2 * u)


def tail_pose(pose, t, lift, swing, freq=1.0, lag=0.12, flutter=0.0):
    for i, b in enumerate(TAIL):
        lf = lift[i] if isinstance(lift, (list, tuple)) else lift
        pose[b] = dict(rot=(lf + flutter * s(t, freq * 2, -lag * i),
                            0, (swing * (1 + 0.5 * i)) * s(t, freq, -lag * i)))


# Posture de base : bras abaisses le long du corps (la pose de repos du squelette est en A)
ARM = (-6, 40, 0)
FORE = (0, 0, -24)
FING = (0, 30, 0)
ANGRY_BROW = dict(rot=(0, -17, 0), loc=(-0.008, 0, -0.016))

clips = []

# ------------------------------------------------------------------------------------ Idle (respire, cligne, souffle)
def idle_pose(t):
    b = c01(t)
    snort = bump(t, 0.74, 0.07)
    blink = bump(t, 0.36, 0.045)
    glance = smooth((t - 0.50) / 0.06) - smooth((t - 0.86) / 0.06)
    flick_l = bump(t, 0.60, 0.05)
    flick_r = bump(t, 0.18, 0.05)
    p = {
        "Hips": dict(loc=(0.012 * s(t, 1, 0.1), 0, -0.015 - 0.006 * b), rot=(0, 1.2 * s(t, 1, 0.1), 0)),
        "Spine": dict(rot=(2 - 1.0 * b, 0, 0)),
        "Chest": dict(rot=(3 - 2.5 * b, 0, 0), scale=(1 + 0.03 * b, 1 + 0.045 * b, 1 + 0.012 * b)),
        "Head": dict(rot=(-2 + 2.0 * c01(t + 0.08) + 4 * snort, 0, 2.5 * s(t, 1, 0.3) + 5 * glance)),
        "Muzzle": dict(scale=(1 + 0.08 * snort, 1 + 0.05 * snort, 1 + 0.08 * snort)),
        "Shoulder": dict(rot=(0, -2.5 * b, 0)),
        "UpperArm": dict(rot=add(ARM, (1.5 * s(t, 1, 0.2), -1.5 * b, 0))),
        "Forearm": dict(rot=add(FORE, (0, 0, -2 * b))),
        "Fingers": dict(rot=FING),
        "Lid": dict(rot=(66 * blink, 0, 0)),
        "Ctrl_Eyes": dict(loc=(0.28 * glance, 0, 0)),
        "Ear_L": dict(rot=(0, -14 * flick_l, 12 * flick_l)),
        "Ear_R": dict(rot=(0, 14 * flick_r, -12 * flick_r)),
    }
    tail_pose(p, t, 8, 7)       # relevee : le bout ne pend plus entre les jambes (visible de face)
    return p

c = Clip("Idle", 64, True)
c.sample(idle_pose)
clips.append(c)

# ------------------------------------------------------------------------------------ Walk (pas lourds, balancement)
def on_ground(pitch):
    """Hauteur a ajouter a la cheville pour que le sabot incline ne s'enfonce pas dans le sol."""
    a = math.radians(abs(pitch))
    reach = 0.22 if pitch > 0 else 0.13          # distance cheville -> pointe / talon du sabot
    return max(0.0, reach * math.sin(a) - 0.165 * (1 - math.cos(a)))


def foot_cycle(p, stride, lift, stance, toe_off=26, toe_up=-12):
    """Sabot sur un cycle p dans [0, 1[ : appui (glisse vers l'arriere) puis vol (revient devant)."""
    if p < stance:
        u = p / stance
        y = -stride / 2 + stride * u
        pitch = toe_up * max(0.0, 1 - u * 5) + 14 * smooth((u - 0.8) / 0.2)
        return dict(loc=(0, y, on_ground(pitch)), rot=(pitch, 0, 0))
    u = (p - stance) / (1 - stance)
    y = stride / 2 - stride * smooth(u)
    z = lift * math.sin(math.pi * u) ** 0.9
    pitch = 14 + (toe_off - 14) * math.sin(math.pi * min(1.0, u * 1.6)) if u < 0.62 else toe_off * 0.6 * (1 - smooth((u - 0.62) / 0.3)) + toe_up * smooth((u - 0.7) / 0.3)
    return dict(loc=(0, y, max(z, on_ground(pitch))), rot=(pitch, 0, 0))

# Foulee longue et cycle lent (0,67 s) : pas lourds, moins presses (demande de Gabin, 2026-11-15).
# Les vitesses de lecture du blend tree Locomotion (unity_setup_bull.cs) sont calees sur ce cycle.
WALK_STRIDE = 0.40

def walk_pose(t):
    cy = math.cos(TWO_PI * t)
    lean = math.sin(TWO_PI * (t - 0.025))          # > 0 : appui sur la jambe gauche
    p = {
        "IK_Foot_L": foot_cycle(t % 1.0, WALK_STRIDE, 0.11, 0.55),
        "IK_Foot_R": mirror(foot_cycle((t + 0.5) % 1.0, WALK_STRIDE, 0.11, 0.55)),
        "Hips": dict(loc=(0.030 * lean, -0.02, -0.052 - 0.020 * math.cos(TWO_PI * (2 * t - 0.14))),
                     rot=(4, 4.5 * lean, -6 * cy)),
        "Spine": dict(rot=(4, -2 * lean, 4 * cy)),
        "Chest": dict(rot=(5 + 1.5 * s(t, 2, 0.1), -2.5 * lean, 7 * cy)),
        "Head": dict(rot=(-6 + 2 * s(t, 2, 0.2), 0, -5 * cy)),
        "Shoulder_L": dict(rot=(0, -2 - 2 * cy, 0)),
        "Shoulder_R": dict(rot=(0, 2 - 2 * cy, 0)),
        "UpperArm_L": dict(rot=add(ARM, (22 * cy, 2, 0))),
        "UpperArm_R": mirror(dict(rot=add(ARM, (-22 * cy, 2, 0)))),
        "Forearm_L": dict(rot=add(FORE, (0, 0, -14 * (0.5 - 0.5 * cy)))),
        "Forearm_R": mirror(dict(rot=add(FORE, (0, 0, -14 * (0.5 + 0.5 * cy))))),
        "Fingers": dict(rot=(0, 45, 0)),
        "Ear": dict(rot=(0, 5 * s(t, 2, 0.3), 4)),
        "Lid": dict(rot=(4, 0, 0)),
    }
    tail_pose(p, t, 2, 9, lag=0.14, flutter=3)
    return p

c = Clip("Walk", 20, True)
c.sample(walk_pose)
clips.append(c)

# ------------------------------------------------------------------------------------ Prepare (gratte le sol en soufflant)
def scrape_foot(p):
    """Sabot droit : gratte vers l'arriere au sol, puis revient devant en l'air."""
    if p < 0.42:
        u = p / 0.42
        e = u * u * (3 - 2 * u)
        return dict(loc=(0, -0.17 + 0.36 * e, on_ground(8 + 26 * e) - 0.012), rot=(8 + 26 * e, 0, 0))
    u = (p - 0.42) / 0.58
    pitch = 34 * (1 - smooth(u * 1.4)) - 14 * math.sin(math.pi * u) * u + 8 * smooth((u - 0.8) / 0.2)
    return dict(loc=(0, 0.19 - 0.36 * smooth(u), max(0.11 * math.sin(math.pi * u), on_ground(pitch) - 0.012)), rot=(pitch, 0, 0))

def prepare_pose(t):
    drag = bump(t, 0.21, 0.21)                    # effort pendant le grattage
    snort = max(bump(t, 0.12, 0.09), bump(t, 0.62, 0.09))
    p = {
        "IK_Foot_L": dict(loc=(0.03, -0.03, 0)),
        "IK_Foot_R": scrape_foot(t % 1.0),
        "Hips": dict(loc=(0.045, -0.07 - 0.015 * drag, -0.075 + 0.012 * drag), rot=(15 + 3 * drag, 3, -4 + 3 * drag)),
        "Spine": dict(rot=(12 + 2 * drag, 0, 2)),
        "Chest": dict(rot=(12 + 2 * drag, 0, 3 - 3 * drag), scale=(1 + 0.03 * snort, 1 + 0.04 * snort, 1)),
        "Head": dict(rot=(-20 + 5 * snort, 2 * s(t, 2), 3.5 * s(t, 2, 0.1))),
        "Muzzle": dict(scale=(1 + 0.12 * snort, 1 + 0.08 * snort, 1 + 0.10 * snort)),
        "Shoulder": dict(rot=(0, -5 - 2 * snort, 4)),
        "UpperArm": dict(rot=(20 + 4 * drag, 30 - 3 * snort, 0)),
        "Forearm": dict(rot=(0, 0, -52 - 5 * drag)),
        "Fingers": dict(rot=(0, 78, 0)),
        "Thumb": dict(rot=(0, 10, -20)),
        "Brow": ANGRY_BROW,
        "Lid": dict(rot=(13, 0, 0)),
        "Ctrl_Eyes": dict(loc=(0, 0, 0.38)),
        "Ear": dict(rot=(0, 8 + 4 * s(t, 2, 0.2), 30)),
    }
    tail_pose(p, t, [38, 16, 12, 10], 10, freq=2, lag=0.10, flutter=4)
    return p

c = Clip("Prepare", 20, True)
c.sample(prepare_pose)
clips.append(c)

# ------------------------------------------------------------------------------------ Charge (fonce tete baissee, cornes en avant)
def charge_pose(t):
    cy = math.cos(TWO_PI * t)
    lean = math.sin(TWO_PI * (t - 0.03))
    bob = math.cos(TWO_PI * (2 * t - 0.16))
    p = {
        "IK_Foot_L": foot_cycle(t % 1.0, 0.33, 0.15, 0.40, toe_off=42, toe_up=-18),
        "IK_Foot_R": mirror(foot_cycle((t + 0.5) % 1.0, 0.33, 0.15, 0.40, toe_off=42, toe_up=-18)),
        "Hips": dict(loc=(0.02 * lean, -0.10, -0.085 - 0.024 * bob), rot=(32 + 2 * bob, 4 * lean, -6 * cy)),
        "Spine": dict(rot=(12, -2 * lean, 3 * cy)),
        "Chest": dict(rot=(10 - 2 * bob, -2 * lean, 6 * cy), scale=(1, 1, 1 + 0.025 * bob)),
        "Head": dict(rot=(-19 + 3 * bob, 0, -3 * cy)),
        "Muzzle": dict(scale=(1 + 0.04 * c01(2 * t), 1, 1 + 0.04 * c01(2 * t))),
        "Shoulder": dict(rot=(0, -4, 10)),
        "UpperArm_L": dict(rot=(26 + 9 * cy, 50, 0)),
        "UpperArm_R": mirror(dict(rot=(26 - 9 * cy, 50, 0))),
        "Forearm": dict(rot=(0, 0, -30 - 6 * bob)),
        "Fingers": dict(rot=(0, 80, 0)),
        "Thumb": dict(rot=(0, 10, -20)),
        "Brow": ANGRY_BROW,
        "Lid": dict(rot=(9, 0, 0)),
        "Ctrl_Eyes": dict(loc=(0, 0, 0.95)),
        "Ear": dict(rot=(0, 6 + 5 * s(t, 2, 0.25), 46)),
    }
    tail_pose(p, t, [62, 14, 6, 2], 5, freq=2, lag=0.16, flutter=6)
    return p

c = Clip("Charge", 10, True)
c.sample(charge_pose)
clips.append(c)

# ------------------------------------------------------------------------------------ Stun (sonne : titube, yeux qui tournent)
def stun_pose(t):
    a = TWO_PI * 2 * t
    p = {
        "IK_Foot_L": dict(loc=(0.05, 0, 0), rot=(0, 0, 6)),
        "IK_Foot_R": dict(loc=(-0.05, 0, 0), rot=(0, 0, -6)),
        "Hips": dict(loc=(0.04 * s(t), 0.03 * s(t, 1, 0.25), -0.075 + 0.012 * s(t, 2)),
                     rot=(4 + 5 * s(t, 1, 0.25), 5 * s(t), 0)),
        "Spine": dict(rot=(6 + 3 * s(t, 1, 0.20), 4 * s(t, 1, -0.05), 0)),
        "Chest": dict(rot=(8 + 3 * s(t, 1, 0.15), 5 * s(t, 1, -0.10), 0)),
        "Head": dict(rot=(8 + 7 * s(t, 1, 0.05), 13 * s(t, 1, -0.20), 9 * s(t, 1, -0.15))),
        "Shoulder": dict(rot=(0, 5, 0)),
        "UpperArm_L": dict(rot=(4 + 7 * s(t, 1, -0.20), 56 + 4 * s(t, 1, -0.25), 0)),
        "UpperArm_R": mirror(dict(rot=(4 + 7 * s(t, 1, 0.30), 56 - 4 * s(t, 1, -0.25), 0))),
        "Forearm": dict(rot=(0, 0, -10)),
        "Fingers": dict(rot=(0, 12, 0)),
        "Brow": dict(rot=(0, 15, 0), loc=(0, 0, 0.016)),
        "Lid_L": dict(rot=(24 + 7 * s(t, 3), 0, 0)),
        "Lid_R": dict(rot=(30 + 7 * s(t, 3, 0.4), 0, 0)),
        "Ctrl_Eye_L": dict(loc=(0.33 * math.cos(a), 0, 0.33 * math.sin(a))),
        "Ctrl_Eye_R": dict(loc=(0.33 * math.cos(-a + 2.2), 0, 0.33 * math.sin(-a + 2.2))),
        "Ear_L": dict(rot=(0, 26 + 6 * s(t, 1, -0.3), 0)),
        "Ear_R": dict(rot=(0, -26 + 6 * s(t, 1, -0.3), 0)),
        "Muzzle": dict(scale=(1 + 0.03 * c01(t), 1, 1 + 0.03 * c01(t))),
    }
    tail_pose(p, t, [-12, -8, -6, -4], 3)
    return p

c = Clip("Stun", 60, True)
c.sample(stun_pose)
clips.append(c)

# ------------------------------------------------------------------------------------ Impact (percute le mur, rebondit, finit sonne)
c = Clip("Impact", 20, False)
start = charge_pose(0.0)
start["IK_Foot_L"] = dict(loc=(0, -0.10, 0))
start["IK_Foot_R"] = dict(loc=(0, 0.10, 0), rot=(14, 0, 0))
c.pose(0, start)
c.pose(2, {   # s'ecrase contre le mur
    "IK_Foot_L": dict(loc=(0, -0.10, 0)), "IK_Foot_R": dict(loc=(0, 0.06, 0)),
    "Hips": dict(loc=(0, -0.20, -0.10), rot=(20, 0, 0)),
    "Spine": dict(rot=(2, 0, 0)), "Chest": dict(rot=(-6, 0, 0), scale=(1.14, 0.78, 1.06)),
    "Head": dict(rot=(-38, 0, 6)), "Muzzle": dict(scale=(1.2, 0.7, 1.15)),
    "Shoulder": dict(rot=(0, -8, -10)), "UpperArm": dict(rot=(-55, 12, 0)), "Forearm": dict(rot=(0, 0, -30)),
    "Fingers": dict(rot=(0, 5, 0)),
    "Brow": dict(loc=(0, 0, 0.03)), "Lid": dict(rot=(-28, 0, 0)), "Ear": dict(rot=(0, -20, -40)),
    "Tail_01": dict(rot=(80, 0, 0)), "Tail_02": dict(rot=(30, 0, 0)), "Tail_03": dict(rot=(20, 0, 0)),
})
c.pose(6, {   # rebondit en arriere, bras en l'air
    "IK_Foot_L": dict(loc=(0.04, 0.10, 0.13), rot=(-18, 0, 0)), "IK_Foot_R": dict(loc=(-0.02, 0.14, 0.0)),
    "Hips": dict(loc=(0, 0.16, 0.06), rot=(-22, 0, 4)),
    "Spine": dict(rot=(-10, 0, 0)), "Chest": dict(rot=(-12, 0, 0), scale=(0.95, 1.08, 1.0)),
    "Head": dict(rot=(-18, 6, -8)), "Muzzle": dict(scale=(0.95, 1.1, 0.95)),
    "Shoulder": dict(rot=(0, -10, 0)), "UpperArm": dict(rot=(-24, -4, 0)), "Forearm": dict(rot=(0, 0, -20)),
    "Fingers": dict(rot=(0, -5, 0)),
    "Brow": dict(rot=(0, 12, 0), loc=(0, 0, 0.03)), "Lid": dict(rot=(-30, 0, 0)), "Ear": dict(rot=(0, 20, 30)),
    "Ctrl_Eye_L": dict(loc=(-0.25, 0, 0.1)), "Ctrl_Eye_R": dict(loc=(0.25, 0, 0.1)),
    "Tail_01": dict(rot=(25, 0, 0)), "Tail_02": dict(rot=(10, 0, 0)), "Tail_03": dict(rot=(5, 0, 0)),
})
c.pose(11, {  # retombe lourdement sur ses sabots
    "IK_Foot_L": dict(loc=(0.05, 0.05, 0), rot=(0, 0, 6)), "IK_Foot_R": dict(loc=(-0.05, 0.06, 0), rot=(0, 0, -6)),
    "Hips": dict(loc=(0.02, 0.07, -0.15), rot=(10, 0, 0)),
    "Spine": dict(rot=(10, 0, 0)), "Chest": dict(rot=(12, 0, 0), scale=(1.06, 1.0, 0.94)),
    "Head": dict(rot=(20, -6, 6)),
    "Shoulder": dict(rot=(0, 8, 0)), "UpperArm": dict(rot=(10, 62, 0)), "Forearm": dict(rot=(0, 0, -16)),
    "Fingers": dict(rot=(0, 20, 0)),
    "Brow": dict(rot=(0, 15, 0), loc=(0, 0, 0.016)), "Lid": dict(rot=(40, 0, 0)), "Ear": dict(rot=(0, 34, 0)),
    "Ctrl_Eye_L": dict(loc=(0.2, 0, -0.3)), "Ctrl_Eye_R": dict(loc=(-0.3, 0, 0.2)),
    "Tail_01": dict(rot=(10, 0, 0)), "Tail_02": dict(rot=(10, 0, 0)),
})
up = stun_pose(0.0)
up["Hips"] = dict(loc=(0, 0.02, -0.04), rot=(2, 0, 0))
up["Head"] = dict(rot=(-4, 6, -4))
c.pose(15, up)
c.pose(20, stun_pose(0.0))
clips.append(c)

# ------------------------------------------------------------------------------------ Hit (encaisse un coup)
c = Clip("Hit", 12, False)
c.pose(0, idle_pose(0.0))
c.pose(3, {
    "Hips": dict(loc=(0, 0.09, -0.03), rot=(-12, 0, 3)),
    "Spine": dict(rot=(-6, 0, 0)), "Chest": dict(rot=(-9, 0, -4), scale=(1.06, 0.92, 1.03)),
    "Head": dict(rot=(-20, 5, 12)), "Muzzle": dict(scale=(1.08, 0.9, 1.08)),
    "Shoulder": dict(rot=(0, -8, 0)), "UpperArm": dict(rot=(-4, 26, 0)), "Forearm": dict(rot=(0, 0, -62)),
    "Fingers": dict(rot=(0, 5, 0)),
    "Brow": dict(rot=(0, -12, 0), loc=(0, 0, -0.012)), "Lid": dict(rot=(58, 0, 0)), "Ear": dict(rot=(0, 10, 40)),
    "Tail_01": dict(rot=(30, 0, 0)), "Tail_02": dict(rot=(20, 0, 0)),
})
c.pose(7, {
    "Hips": dict(loc=(0, 0.03, -0.05), rot=(6, 0, -1)),
    "Spine": dict(rot=(5, 0, 0)), "Chest": dict(rot=(7, 0, 2), scale=(0.98, 1.03, 0.99)),
    "Head": dict(rot=(8, -2, -4)),
    "UpperArm": dict(rot=add(ARM, (6, 6, 0))), "Forearm": dict(rot=FORE), "Fingers": dict(rot=FING),
    "Brow": ANGRY_BROW, "Lid": dict(rot=(16, 0, 0)), "Ear": dict(rot=(0, -6, -8)),
    "Tail_01": dict(rot=(-10, 0, 0)),
})
c.pose(12, idle_pose(0.0))
clips.append(c)

# ------------------------------------------------------------------------------------ Die (sursaut, puis s'effondre a plat ventre)
c = Clip("Die", 34, False)
c.pose(0, idle_pose(0.0))
c.pose(4, {   # sursaut
    "IK_Foot_L": dict(loc=(0.02, 0, 0)), "IK_Foot_R": dict(loc=(-0.02, 0, 0)),
    "Hips": dict(loc=(0, 0.05, 0.03), rot=(-14, 0, 0)),
    "Spine": dict(rot=(-8, 0, 0)), "Chest": dict(rot=(-10, 0, 0), scale=(0.96, 1.06, 1.03)),
    "Head": dict(rot=(-22, 0, 0)), "Shoulder": dict(rot=(0, -10, 0)),
    "UpperArm": dict(rot=(-10, 6, 0)), "Forearm": dict(rot=(0, 0, -30)), "Fingers": dict(rot=(0, -5, 0)),
    "Brow": dict(rot=(0, 14, 0), loc=(0, 0, 0.03)), "Lid": dict(rot=(-30, 0, 0)), "Ear": dict(rot=(0, -18, -10)),
    "Tail_01": dict(rot=(60, 0, 0)), "Tail_02": dict(rot=(25, 0, 0)),
})
c.pose(11, {  # bascule vers l'avant
    "IK_Foot_L": dict(loc=(0.03, 0.02, 0), rot=(20, 0, 0)), "IK_Foot_R": dict(loc=(-0.03, 0.04, 0), rot=(24, 0, 0)),
    "Hips": dict(loc=(0, -0.22, -0.10), rot=(42, 0, 0)),
    "Spine": dict(rot=(8, 0, 0)), "Chest": dict(rot=(8, 0, 0)),
    "Head": dict(rot=(-24, 0, 0)), "Shoulder": dict(rot=(0, -6, 0)),
    "UpperArm": dict(rot=(-62, 14, 0)), "Forearm": dict(rot=(0, 0, -22)), "Fingers": dict(rot=(0, -8, 0)),
    "Brow": dict(rot=(0, 14, 0), loc=(0, 0, 0.02)), "Lid": dict(rot=(-10, 0, 0)), "Ear": dict(rot=(0, 10, 30)),
    "Tail_01": dict(rot=(30, 0, 0)),
})
DOWN = {      # a plat ventre, bras etales, tete sur le cote
    "IK_Foot_L": dict(loc=(0.06, 0.05, 0.02), rot=(78, 0, 8)), "IK_Foot_R": dict(loc=(-0.06, 0.07, 0.02), rot=(78, 0, -8)),
    "Hips": dict(loc=(0, -0.50, -0.24), rot=(86, 0, 0)),
    "Spine": dict(rot=(2, 0, 0)), "Chest": dict(rot=(2, 0, 0), scale=(1.06, 0.92, 1.0)),
    "Head": dict(rot=(-62, 0, 34)), "Shoulder": dict(rot=(0, -8, 0)),
    "UpperArm_L": dict(rot=(-14, -58, 0)), "UpperArm_R": dict(rot=(-10, 36, 0)),
    "Forearm_L": dict(rot=(0, 0, -10)), "Forearm_R": dict(rot=(0, 0, 8)),
    "Fingers": dict(rot=(0, 14, 0)),
    "Brow": dict(rot=(0, 10, 0), loc=(0, 0, 0.008)), "Lid": dict(rot=(68, 0, 0)),
    "Ear_L": dict(rot=(0, 24, 10)), "Ear_R": dict(rot=(0, -30, -10)),
    "Tail_01": dict(rot=(-30, 0, 10)), "Tail_02": dict(rot=(-25, 0, 10)), "Tail_03": dict(rot=(-20, 0, 8)),
}
slam = {k: dict(v) for k, v in DOWN.items()}
slam["Hips"] = dict(loc=(0, -0.50, -0.275), rot=(88, 0, 0))
slam["Chest"] = dict(rot=(4, 0, 0), scale=(1.14, 0.80, 1.0))
slam["Head"] = dict(rot=(-56, 0, 10))
slam["Lid"] = dict(rot=(50, 0, 0))
slam["Tail_01"] = dict(rot=(50, 0, 0))
c.pose(16, slam)
bounce = {k: dict(v) for k, v in DOWN.items()}
bounce["Hips"] = dict(loc=(0, -0.50, -0.20), rot=(83, 0, 0))
bounce["Head"] = dict(rot=(-68, 0, 30))
c.pose(20, bounce)
c.pose(25, DOWN)
c.pose(34, DOWN)
clips.append(c)

# ------------------------------------------------------------------------------------ Turn (demi-tour : pietine sur place)
def turn_pose(t):
    lift_l = bump(t, 0.25, 0.22)
    lift_r = bump(t, 0.75, 0.22)
    def step(lift):
        return dict(loc=(0.02 * lift, -0.05 * lift, 0.09 * lift), rot=(12 * lift, 0, 0))
    p = {
        "IK_Foot_L": step(lift_l),
        "IK_Foot_R": mirror(step(lift_r)),
        "Hips": dict(loc=(0.025 * (lift_r - lift_l), -0.01, -0.045 - 0.012 * (lift_l + lift_r)),
                     rot=(4, 4 * (lift_r - lift_l), 3 * (lift_l - lift_r))),
        "Spine": dict(rot=(3, -1.5 * (lift_r - lift_l), 0)),
        "Chest": dict(rot=(4, -2 * (lift_r - lift_l), 0)),
        "Head": dict(rot=(-4 + 2 * (lift_l + lift_r), 0, 0)),
        "Shoulder": dict(rot=(0, -2, 0)),
        "UpperArm_L": dict(rot=add(ARM, (10 * (lift_r - lift_l), 4 * lift_r, 0))),
        "UpperArm_R": mirror(dict(rot=add(ARM, (10 * (lift_l - lift_r), 4 * lift_l, 0)))),
        "Forearm": dict(rot=add(FORE, (0, 0, -6))),
        "Fingers": dict(rot=(0, 40, 0)),
        "Ear": dict(rot=(0, 4 * s(t, 2, 0.3), 6)),
        "Lid": dict(rot=(4, 0, 0)),
    }
    tail_pose(p, t, 8, 8, lag=0.14, flutter=2)
    return p

c = Clip("Turn", 16, True)
c.sample(turn_pose)
clips.append(c)

# ------------------------------------------------------------------------------------ WakeUp (se reveille : secoue la tete, s'ebroue)
def wake_pose(shake, ear, lid, eyes=(0.0, 0.0), snort=0.0, hips_dz=0.0):
    p = idle_pose(0.0)
    p["Hips"] = dict(loc=(0, 0, -0.02 + hips_dz), rot=(2, -0.25 * shake, 0))
    p["Chest"] = dict(rot=(3, -0.2 * shake, 0.25 * shake), scale=(1 + 0.04 * snort, 1 + 0.05 * snort, 1))
    p["Head"] = dict(rot=(4 - 6 * snort, 0.35 * shake, shake))
    p["Muzzle"] = dict(scale=(1 + 0.12 * snort, 1 + 0.08 * snort, 1 + 0.10 * snort))
    p["Ear_L"] = dict(rot=(0, ear - 0.6 * shake, 0.5 * shake))
    p["Ear_R"] = dict(rot=(0, -ear - 0.6 * shake, 0.5 * shake))
    p["Lid"] = dict(rot=(lid, 0, 0))
    p["Ctrl_Eye_L"] = dict(loc=(eyes[0], 0, eyes[1]))
    p["Ctrl_Eye_R"] = dict(loc=(-eyes[1], 0, eyes[0]))
    if snort > 0:
        p["Brow"] = ANGRY_BROW
    return p

c = Clip("WakeUp", 30, False)
c.pose(0, stun_pose(0.0))
c.pose(5, wake_pose(24, 20, 30, eyes=(0.25, 0.1), hips_dz=-0.02))
c.pose(10, wake_pose(-24, 20, 18, eyes=(-0.15, 0.2)))
c.pose(14, wake_pose(16, 10, 10, eyes=(0.08, -0.05)))
c.pose(18, wake_pose(-9, 4, 4))
c.pose(23, wake_pose(0, 0, 0, snort=1.0))
c.pose(30, idle_pose(0.0))
clips.append(c)

# ------------------------------------------------------------------------------------ Brake (freine des sabots, arrete par un joueur)
c = Clip("Brake", 15, False)
c.pose(0, charge_pose(0.0))
c.pose(4, {   # se cabre en arriere, sabots en avant qui labourent le sol
    "IK_Foot_L": dict(loc=(0.02, -0.14, on_ground(-20)), rot=(-20, 0, 0)), "IK_Foot_R": dict(loc=(-0.02, -0.07, on_ground(-12)), rot=(-12, 0, 0)),
    "Hips": dict(loc=(0, 0.04, -0.11), rot=(-4, 0, 0)),
    "Spine": dict(rot=(-4, 0, 0)), "Chest": dict(rot=(-8, 0, 0)),
    "Head": dict(rot=(-14, 0, 0)), "Muzzle": dict(scale=(1.08, 1, 1.08)),
    "Shoulder": dict(rot=(0, -6, 0)), "UpperArm": dict(rot=(-42, 28, 0)), "Forearm": dict(rot=(0, 0, -22)),
    "Fingers": dict(rot=(0, 10, 0)),
    "Brow": dict(loc=(0, 0, 0.02)), "Lid": dict(rot=(-20, 0, 0)), "Ear": dict(rot=(0, -12, -36)),
    "Tail_01": dict(rot=(45, 0, 0)), "Tail_02": dict(rot=(20, 0, 0)),
})
c.pose(9, {   # l'elan le fait basculer vers l'avant
    "IK_Foot_L": dict(loc=(0.03, -0.10, 0)), "IK_Foot_R": dict(loc=(-0.03, -0.04, 0)),
    "Hips": dict(loc=(0, -0.04, -0.09), rot=(14, 0, 0)),
    "Spine": dict(rot=(8, 0, 0)), "Chest": dict(rot=(10, 0, 0)),
    "Head": dict(rot=(12, 0, 4)),
    "Shoulder": dict(rot=(0, 4, 0)), "UpperArm": dict(rot=(-14, 50, 0)), "Forearm": dict(rot=(0, 0, -14)),
    "Fingers": dict(rot=(0, 15, 0)),
    "Brow": dict(rot=(0, 12, 0), loc=(0, 0, 0.016)), "Lid": dict(rot=(30, 0, 0)), "Ear": dict(rot=(0, 24, 10)),
    "Tail_01": dict(rot=(10, 0, 0)),
})
c.pose(15, stun_pose(0.0))
clips.append(c)

# ------------------------------------------------------------------------------------ Base (pose de repos du FBX)
# L'export FBX ecrit la pose de la premiere action (ordre alphabetique) comme pose par defaut :
# « Base » passe avant « Brake » et « Charge », l'apercu du FBX montre donc l'Idle. Ignoree par unity_setup_bull.cs.
c = Clip("Base", 1, False)
c.pose(0, idle_pose(0.0))
c.pose(1, idle_pose(0.0))
clips.append(c)

out = {}
for c in clips:
    c.bake()
    out[c.name] = {"frames": c.length, "loop": c.loop, "seconds": round(c.length / FPS, 3)}

# Laisse l'Idle affichee
rig.animation_data.action = bpy.data.actions["Idle"]
rig.animation_data.action_slot = bpy.data.actions["Idle"].slots[0]
scene.frame_start = 0
scene.frame_end = 64
scene.frame_set(0)
result = out
