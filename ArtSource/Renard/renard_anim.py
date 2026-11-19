# Animations cartoon du renard (30 images/s, sur place) - a lancer apres renard_setup.py.
#   Marche, Course, Saut, Chute, Atterrissage, Roulade, Attaque, Poussee, Degats,
#   Effondrement, AuSol, Ramper, Mort, Relever (etat a terre puis mort, GDD Sante_et_Degats)
# L'Idle reste celui de Gabin ("Repos", dans Renard.fbx). On reprend ses procedes :
#   - regard en saccades : Controlleur_Yeux reste fixe, saute en 2-3 images, puis se fige (les yeux le visent) ;
#   - clignements rapides (fermeture en 3 images, ouverture en 2) ;
#   - petits coups d'oreille (Oreille_Haut, entree vive, retour amorti) ;
#   - queue en chaine : chaque os suit le precedent avec du retard (overlapping action).
# On y ajoute le langage cartoon : anticipation, squash & stretch (echelle de Epaules / Bassin), depassement.
#
# Conventions (espace de l'armature : le renard regarde vers -Y, sa gauche est +X, Z vers le haut) :
#   rot = (rx, ry, rz) en degres autour des axes de l'armature, au pivot de l'os (axes portes par le parent),
#   appliquee PAR-DESSUS la pose de base de l'Idle (bras baisses, doigts plies) :
#     - rx > 0 : bascule vers l'avant (torse / tete vers le bas ; pied : pointe vers le bas ; queue : se releve ;
#                bras qui pend : part vers l'arriere)
#     - ry > 0 : penche vers sa gauche (+X) ; bras gauche : s'abaisse (ry < 0 = leve le bras sur le cote) ;
#                oreille gauche : ry < 0 = tombe vers l'exterieur
#     - rz > 0 : tourne vers sa gauche ; avant-bras gauche : rz < 0 = plie le coude vers l'avant
#   Bras (Epaule) : ordre Y puis X. ry < 0 leve le bras gauche sur le cote (-150 = bras en l'air), puis rx le
#   fait tourner autour de l'axe gauche-droite : bras qui pend, rx < 0 = vers l'avant ; bras leve, rx < 0 = derriere la tete.
#   loc = (dx, dy, dz) en metres (dy < 0 = vers l'avant) ; scale = (largeur X, profondeur Y, hauteur Z)
#   Un nom sans suffixe (ex. "Epaule") s'applique aux deux cotes, en miroir. ".L" = gauche, ".R" = droite.
#   Jambes : on anime IK_Pied.L / IK_Pied.R (position et inclinaison du pied) et Bassin ; les genoux suivent.
#   Extras : "_lids" (0 ouvert -> 1 ferme, d'apres le clignement de l'Idle), "_flick_L/R" (coup d'oreille 0..1),
#            regard = loc de "Controlleur_Yeux" (dx, 0, dz), yeux qui s'ecarquillent = scale de "Oeil".
import bpy, json, math
from mathutils import Vector, Euler, Quaternion, Matrix

SRC = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo\ArtSource\Renard"
FPS = 30
rig = bpy.data.objects["Armature"]
arm = rig.data
scene = bpy.context.scene
scene.render.fps = FPS

with open(SRC + r"\renard_base_pose.json", encoding="utf-8") as f:
    BASE = json.load(f)

REST = {b.name: b.matrix_local.to_3x3() for b in arm.bones}
REST_INV = {k: v.inverted() for k, v in REST.items()}
HEAD = {b.name: b.head_local.copy() for b in arm.bones}
ALL_BONES = [b.name for b in arm.bones]
SIDED = {b.name[:-2] for b in arm.bones if b.name.endswith(".L")}
TAIL = ["Queue_Bas", "Queue_Milieu", "Queue_Bout"]
LOOK_TARGETS = {"Controlleur_Yeux", "Controlleur_Oeil.L", "Controlleur_Oeil.R"}
# Os de controle qui doivent recopier leur os de deformation (la main gauche pend sous Controlleur_Avant_Bras.L,
# l'annulaire et la base des oreilles sont en partie peints sur leur controleur).
FOLLOW = {b: b.replace("Controlleur_", "") for b in ALL_BONES
          if b.startswith("Controlleur_") and b.replace("Controlleur_", "") in REST and b not in LOOK_TARGETS}
TWO_PI = 2 * math.pi
GROUND = -1.116          # sol, dans l'espace de l'armature
LID_CLOSED = {k: Quaternion(v) for k, v in BASE["lid_closed"].items()}
EAR_FLICK = {k: Quaternion(v) for k, v in BASE["ear_flick"].items()}


def base_q(b):
    return Quaternion(BASE["base"][b]["q"])


# Bras : d'abord ecarter / lever (ry), puis balancer d'avant en arriere (rx).
ORDER = {"Epaule.L": 'YXZ', "Epaule.R": 'YXZ'}


def delta_q(bone, rot_deg):
    rw = Euler([math.radians(a) for a in rot_deg], ORDER.get(bone, 'XYZ')).to_matrix()
    return (REST_INV[bone] @ rw @ REST[bone]).to_quaternion()


def local_scale(bone, s_arm):
    m = REST[bone]
    out = []
    for i in range(3):
        colv = [abs(m[j][i]) for j in range(3)]
        out.append(s_arm[colv.index(max(colv))])
    return out


def mirror(d):
    out = dict(d)
    if d.get("rot") is not None:
        r = d["rot"]
        out["rot"] = (r[0], -r[1], -r[2])
    if d.get("loc") is not None:
        l = d["loc"]
        out["loc"] = (-l[0], l[1], l[2])
    return out


def expand(pose):
    out = {}
    for name, d in pose.items():
        if name in SIDED:
            out.setdefault(name + ".L", d)
            out.setdefault(name + ".R", mirror(d))
        else:
            out[name] = d
    return out


def add(a, b):
    return tuple(x + y for x, y in zip(a, b))


def s(t, freq=1.0, phase=0.0):
    return math.sin(TWO_PI * (t * freq + phase))


def co(t, freq=1.0, phase=0.0):
    return math.cos(TWO_PI * (t * freq + phase))


def smooth(u):
    u = min(1.0, max(0.0, u))
    return u * u * (3 - 2 * u)


def lerp(a, b, u):
    if isinstance(a, tuple):
        return tuple(x + (y - x) * u for x, y in zip(a, b))
    return a + (b - a) * u


def c01(t):
    """0 -> 1 -> 0 sur un cycle (respiration)."""
    return 0.5 - 0.5 * math.cos(TWO_PI * t)


def bump(t, center, width):
    """Bosse lisse 0 -> 1 -> 0 autour de `center` (cyclique), de demi-largeur `width`."""
    d = ((t - center + 0.5) % 1.0) - 0.5
    x = max(0.0, 1.0 - abs(d) / width)
    return x * x * (3 - 2 * x)


def blink(t, center, close=0.025, open_=0.018):
    """Clignement facon Idle : ferme vite, rouvre encore plus vite (t et center en fraction de clip)."""
    d = t - center
    if -close <= d <= 0:
        return smooth(1 + d / close)
    if 0 < d <= open_:
        return 1 - smooth(d / open_)
    return 0.0


def saccade(t, keys, move=0.06):
    """Regard qui se fige puis saute : keys = [(t0, (dx, dz)), ...] (cyclique, trie)."""
    n = len(keys)
    for i in range(n):
        t0, v0 = keys[i]
        t1, v1 = keys[(i + 1) % n]
        if t1 <= t0:
            t1 += 1.0
        tt = t if t >= t0 else t + 1.0
        if t0 <= tt < t1:
            if tt > t1 - move:
                return lerp(v0, v1, smooth((tt - (t1 - move)) / move))
            return v0
    return keys[0][1]


def keyframes(t, keys, ease=smooth):
    """Interpolation par cles : keys = [(t, valeur), ...] (valeurs nombres ou tuples), lissees."""
    if t <= keys[0][0]:
        return keys[0][1]
    for (t0, v0), (t1, v1) in zip(keys, keys[1:]):
        if t0 <= t <= t1:
            return lerp(v0, v1, ease((t - t0) / (t1 - t0)))
    return keys[-1][1]


def tail_pose(p, t, lift, swing, freq=1.0, lag=0.12, flutter=0.0, wave_lift=0.0):
    for i, b in enumerate(TAIL):
        lf = lift[i] if isinstance(lift, (list, tuple)) else lift
        p[b] = dict(rot=(lf + flutter * s(t, freq * 2, -lag * i) + wave_lift * s(t, freq, -lag * i - 0.25),
                         0, swing * (0.6 + 0.5 * i) * s(t, freq, -lag * i)))


def squash(k):
    """k > 0 : etire en hauteur ; k < 0 : ecrase. Volume a peu pres conserve."""
    z = 1 + k
    xy = 1 / math.sqrt(max(z, 0.2))
    return (xy, xy, z)


class Clip:
    def __init__(self, name, length, loop):
        self.name, self.length, self.loop = name, length, loop
        self.frames = {}

    def sample(self, fn, step=1):
        for f in list(range(0, self.length, step)) + [self.length]:
            t = f / self.length
            if self.loop and f == self.length:
                t = 0.0
            self.frames[f] = expand(fn(t))

    def bake(self, ground_fix=False):
        old = bpy.data.actions.get(self.name)
        if old:
            bpy.data.actions.remove(old)
        act = bpy.data.actions.new(self.name)
        act.use_fake_user = True
        ad = rig.animation_data or rig.animation_data_create()
        ad.action = act
        ad.action_slot = act.slots.new(id_type='OBJECT', name=rig.name)
        prev = {}
        for f in sorted(self.frames):
            apply_pose(self.frames[f], prev)
            if ground_fix:
                fix_ground()
            for b in ALL_BONES:
                pb = rig.pose.bones[b]
                for path in ("rotation_quaternion", "location", "scale"):
                    pb.keyframe_insert(path, frame=f, group=b)
        for fc in iter_fcurves(act):
            for k in fc.keyframe_points:
                k.interpolation = 'LINEAR'
        act.use_frame_range = True
        act.frame_start = 0
        act.frame_end = self.length
        act.use_cyclic = self.loop
        return act


def iter_fcurves(act):
    for l in act.layers:
        for st in l.strips:
            for cb in st.channelbags:
                for fc in cb.fcurves:
                    yield fc


def apply_pose(pose, prev):
    final = {}
    for b in ALL_BONES:
        if b in FOLLOW:
            continue
        d = pose.get(b, {})
        q = base_q(b)
        if b in LID_CLOSED and pose.get("_lids"):
            q = q.slerp(LID_CLOSED[b], min(1.0, pose["_lids"]))
        if b in EAR_FLICK and pose.get("_flick_" + b[-1]):
            q = q.slerp(EAR_FLICK[b], pose["_flick_" + b[-1]])
        q = delta_q(b, d.get("rot", (0, 0, 0))) @ q
        loc = Vector(BASE["base"][b]["loc"]) + REST_INV[b] @ Vector(d.get("loc", (0, 0, 0)))
        sc = Vector(BASE["base"][b]["scale"])
        ls = local_scale(b, d.get("scale", (1, 1, 1)))
        sc = Vector((sc.x * ls[0], sc.y * ls[1], sc.z * ls[2]))
        final[b] = (q, loc, sc)
    for f, lead in FOLLOW.items():
        q, loc, sc = final[lead]
        rp = REST[lead] @ q.to_matrix() @ REST_INV[lead]
        qf = (REST_INV[f] @ rp @ REST[f]).to_quaternion()
        locf = REST_INV[f] @ (REST[lead] @ loc)
        final[f] = (qf, locf, sc)
    for b, (q, loc, sc) in final.items():
        pb = rig.pose.bones[b]
        if b in prev:
            q.make_compatible(prev[b])
        prev[b] = q.copy()
        pb.rotation_quaternion = q
        pb.location = loc
        pb.scale = sc


def fix_ground():
    """Pose le point le plus bas du corps sur le sol (roulade) en decalant Bassin et les cibles des pieds."""
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    zmin = 1e9
    for name in ("Renard", "Museau"):
        ob = bpy.data.objects[name].evaluated_get(dg)
        me = ob.to_mesh()
        mw = rig.matrix_world.inverted() @ ob.matrix_world
        zmin = min(zmin, min((mw @ v.co).z for v in me.vertices))
        ob.to_mesh_clear()
    dz = GROUND - zmin
    for b in ("Bassin", "Controlleur_Bassin", "IK_Pied.L", "IK_Pied.R"):
        pb = rig.pose.bones[b]
        pb.location = pb.location + REST_INV[b] @ Vector((0, 0, dz))
    bpy.context.view_layer.update()


# ---------------------------------------------------------------------------------------------- pieds
ANKLE_H = 0.38     # cheville -> plante
TOE_REACH = 0.62   # cheville -> bout du pied (vers l'avant)
HEEL_REACH = 0.24


def on_ground(pitch):
    a = math.radians(abs(pitch))
    reach = TOE_REACH if pitch > 0 else HEEL_REACH
    return max(0.0, reach * math.sin(a) - ANKLE_H * (1 - math.cos(a)))


def foot_cycle(p, stride, lift, stance, toe_off=35, heel=-14):
    """Pied sur un cycle p dans [0, 1[ : appui (glisse vers l'arriere) puis vol (revient devant, genou haut)."""
    if p < stance:
        u = p / stance
        y = -stride / 2 + stride * u
        pitch = heel * max(0.0, 1 - u * 4) + toe_off * 0.7 * smooth((u - 0.75) / 0.25)
        return dict(loc=(0, y, on_ground(pitch)), rot=(pitch, 0, 0))
    u = (p - stance) / (1 - stance)
    y = stride / 2 - stride * smooth(u)
    z = lift * math.sin(math.pi * u) ** 0.8
    pitch = lerp(toe_off * 0.7, toe_off, smooth(u / 0.3)) * (1 - smooth((u - 0.45) / 0.4)) + heel * smooth((u - 0.75) / 0.25)
    return dict(loc=(0, y, max(z, on_ground(pitch))), rot=(pitch, 0, 0))


# Bras : la pose de base (Idle) les baisse deja de ~60 degres.
clips = []


# ---------------------------------------------------------------------------------------------- Marche / Course
# Pas ici : la marche et la course sont celles d'origine de Gabin (Renard.fbx). La course recoit un petit
# rebond dans renard_course_rebond.py (a lancer apres ce script). Les essais facon Mario 3D ont ete retires.


# ---------------------------------------------------------------------------------------------- Saut
# 12 images : micro-ecrasement (le saut part tout de suite en jeu), etirement, bras lances au ciel,
# jambes repliees ; la derniere image est tenue jusqu'a la chute.
SAUT = [  # (t, bassin_z, squash, bras_ry, bras_rx, pied_z, pied_y, pied_pitch, oreilles_ry, queue_rx, tete_rx)
    (0.00, 0.00, 0.00, 0, 0, 0.00, 0.00, 0, 0, 0, 0),
    (0.12, -0.24, -0.16, 18, 30, 0.00, 0.00, 0, -18, -10, 10),
    (0.35, 0.12, 0.20, -120, -10, -0.05, 0.05, 45, -30, -25, -12),
    (0.70, 0.18, 0.04, -100, -18, 0.40, -0.12, 10, 6, 18, -6),
    (1.00, 0.16, 0.02, -95, -15, 0.44, -0.14, 8, 10, 22, -4),
]


def jump_pose(t):
    k = keyframes(t, [(a[0], a[1:]) for a in SAUT])
    bz, sq, ary, arx, fz, fy, fp, ery, qrx, trx = k
    p = {
        "Bassin": dict(loc=(0, 0, bz), scale=squash(sq * 0.6)),
        "Epaules": dict(rot=(-trx * 0.5, 0, 0), scale=squash(sq)),
        "Tete": dict(rot=(trx, 0, 0)),
        "Epaule": dict(rot=(arx, ary, 0)),
        "Avant_Bras": dict(rot=(0, 0, -15)),
        "Poignet": dict(rot=(0, -20 * smooth(t / 0.4), 0)),
        "IK_Pied.L": dict(loc=(0, fy, fz + bz * 0.3 if fz > 0 else 0), rot=(fp, 0, 0)),
        "IK_Pied.R": dict(loc=(0, fy + 0.10, fz * 0.85 + bz * 0.3 if fz > 0 else 0), rot=(fp * 0.9, 0, 0)),
        "Oreille_Bas": dict(rot=(-6, ery, 0)),
        "Oreille_Haut": dict(rot=(-4, ery * 1.3, 0)),
        "Controlleur_Yeux": dict(loc=(0, 0, 0.10 * smooth((t - 0.2) / 0.3))),
        "_lids": 0.35 * bump(t, 0.12, 0.12),
        "Oeil": dict(scale=(1 + 0.12 * bump(t, 0.45, 0.3),) * 3),
    }
    # pieds : la cible IK est dans l'espace de l'armature -> elle suit le bassin quand le renard decolle
    if t > 0.25:
        for side in ("IK_Pied.L", "IK_Pied.R"):
            l = p[side]["loc"]
            p[side]["loc"] = (l[0], l[1], l[2] + max(0.0, bz))
    tail_pose(p, t, (qrx, qrx * 0.6, qrx * 0.3), 4, lag=0.15)
    return p


c = Clip("Saut", 12, False)
c.sample(jump_pose)
clips.append((c, False))


# ---------------------------------------------------------------------------------------------- Chute
# Boucle de 16 images : bras en l'air qui moulinent, jambes qui pedalent, oreilles soulevees par l'air,
# yeux ronds qui regardent en bas (on cherche le sol !).
def fall_pose(t):
    pl = s(t)
    pc = co(t)
    flap = s(t, 2, 0.15)
    p = {
        "Bassin": dict(loc=(0, 0, 0.2), rot=(-4, 3 * pl, 0), scale=squash(0.04 + 0.02 * s(t, 2))),
        "Epaules": dict(rot=(-6, -3 * pl, 4 * pc), scale=squash(0.03)),
        "Tete": dict(rot=(10, 0, 4 * pc)),
        "Epaule.L": dict(rot=(-10 + 22 * pc, -105 + 16 * pl, 0)),
        "Epaule.R": mirror(dict(rot=(-10 - 22 * pc, -105 - 16 * pl, 0))),
        "Avant_Bras.L": dict(rot=(0, -20 + 15 * pc, -10)),
        "Avant_Bras.R": mirror(dict(rot=(0, -20 - 15 * pc, -10))),
        "Poignet": dict(rot=(0, -25 + 15 * s(t, 2), 0)),
        "IK_Pied.L": dict(loc=(0, 0.22 * pc, 0.48 + 0.16 * pl), rot=(20 + 15 * pl, 0, 0)),
        "IK_Pied.R": dict(loc=(0, -0.22 * pc, 0.48 - 0.16 * pl), rot=(20 - 15 * pl, 0, 0)),
        "Oreille_Bas": dict(rot=(-4, 14 + 5 * flap, 0)),
        "Oreille_Haut": dict(rot=(-6, 18 + 9 * s(t, 2, 0.35), 0)),
        "Controlleur_Yeux": dict(loc=(0.03 * pl, 0, -0.16)),
        "Oeil": dict(scale=(1.12,) * 3),
    }
    tail_pose(p, t, (28, 16, 8), 9, lag=0.14, flutter=8)
    return p


c = Clip("Chute", 16, True)
c.sample(fall_pose)
clips.append((c, False))


# ---------------------------------------------------------------------------------------------- Atterrissage
# 14 images : gros ecrasement a l'impact (yeux plisses, oreilles qui s'ecrasent), rebond etire, retour au repos
# avec un petit depassement des oreilles et de la queue.
LAND = [  # (t, bassin_z, squash, bras_ry, oreille_ry, queue_rx, lids, tete_rx)
    (0.00, -0.34, -0.26, -45, -36, -24, 0.65, 12),
    (0.14, -0.40, -0.30, -55, -42, -28, 0.75, 14),
    (0.38, 0.02, 0.08, 6, 14, 14, 0.00, -6),
    (0.62, -0.04, -0.03, -2, -6, -6, 0.00, 2),
    (1.00, 0.00, 0.00, 0, 0, 0, 0.00, 0),
]


def land_pose(t):
    bz, sq, ary, ery, qrx, lids, trx = keyframes(t, [(a[0], a[1:]) for a in LAND])
    p = {
        "Bassin": dict(loc=(0, 0, bz), scale=squash(sq * 0.5)),
        "Epaules": dict(rot=(trx * 0.6, 0, 0), scale=squash(sq)),
        "Tete": dict(rot=(trx, 0, 0)),
        "Epaule": dict(rot=(-14 * (1 - t), ary, 0)),
        "Avant_Bras": dict(rot=(0, 0, 12 * min(0, ary) / 55)),
        "Oreille_Bas": dict(rot=(0, ery, 0)),
        "Oreille_Haut": dict(rot=(0, ery * 1.3, 0)),
        "_lids": lids,
        "Controlleur_Yeux": dict(loc=(0, 0, -0.05 * (1 - smooth(t / 0.5)))),
    }
    tail_pose(p, t, (qrx, qrx * 0.8, qrx * 0.5), 3, lag=0.1)
    return p


c = Clip("Atterrissage", 14, False)
c.sample(land_pose)
clips.append((c, False))


# ---------------------------------------------------------------------------------------------- Roulade
# 18 images (0,6 s) : se roule en boule, tour complet vers l'avant (le corps reste pose sur le sol, calcule
# image par image), se deplie d'un coup avec un depassement. Sur place : le deplacement viendra du code.
PIVOT = Vector((0, -0.25, 0.55))     # centre de la boule (espace de l'armature, avant recalage au sol)


def roll_pose(t):
    tuck = keyframes(t, [(0.0, 0.0), (0.16, 1.0), (0.78, 1.0), (0.92, 0.0), (1.0, 0.0)])
    spin = keyframes(t, [(0.0, 0.0), (0.12, 0.0), (0.84, 1.0), (1.0, 1.0)], ease=lambda u: u ** 1.15 * (3 - 2 * u ** 1.15) * 0.5 + u * 0.5)
    rebound = bump(t, 0.93, 0.07)
    th = 360.0 * spin
    R = Matrix.Rotation(math.radians(th), 3, 'X')
    # pose "en boule", exprimee comme si le renard etait debout puis tournee autour de PIVOT
    pelvis = Vector((0, 0.05, -0.30)) * tuck
    piv = PIVOT * tuck
    pel_w = piv + R @ (pelvis - piv)
    p = {
        "Bassin": dict(loc=tuple(pel_w), rot=(th + 25 * tuck, 0, 0), scale=squash(-0.06 * tuck + 0.08 * rebound)),
        "Epaules": dict(rot=(38 * tuck - 8 * rebound, 0, 0), scale=squash(-0.04 * tuck + 0.08 * rebound)),
        "Tete": dict(rot=(30 * tuck - 10 * rebound, 0, 0)),
        "Epaule": dict(rot=(-55 * tuck, 20 * tuck - 50 * rebound, 0)),
        "Avant_Bras": dict(rot=(0, 0, -60 * tuck)),
        "Oreille_Bas": dict(rot=(-30 * tuck, -10 * tuck + 14 * rebound, 0)),
        "Oreille_Haut": dict(rot=(-20 * tuck, -10 * tuck + 20 * rebound, 0)),
        "_lids": 0.85 * tuck,
    }
    for side, x in (("L", 0.29), ("R", -0.29)):
        rest = HEAD["IK_Pied." + side]
        foot_tuck = Vector((x * (1 - 0.15 * tuck), -0.55 * tuck, -0.732 + 0.55 * tuck))
        foot_w = piv + R @ (foot_tuck - piv)
        p["IK_Pied." + side] = dict(loc=tuple(foot_w - rest), rot=(th + 10 * tuck, 0, 0))
    tail_pose(p, t, (40 * tuck, 30 * tuck, 25 * tuck), 3 * (1 - tuck), lag=0.1)
    return p


c = Clip("Roulade", 18, False)
c.sample(roll_pose)
clips.append((c, True))


# ---------------------------------------------------------------------------------------------- Attaque
# 18 images (0,6 s) : coup de la main droite (main de l'outil).
# Coup vertical par-dessus la tete (convient au baton, a la pelle, a la fourche).
# Anticipation (se tord et arme le bras derriere la tete, oeil plisse, oreilles en arriere), frappe tres rapide (3 images),
# depassement, retour. Pied droit qui avance a la frappe.
ATK = [  # (t, buste_rz, buste_rx, bras_rx, bras_ry, coude_rz, squash, bassin_y, pied_y, oreille_rx, lids)
    (0.00, 0, 0, 0, 0, 0, 0.00, 0.00, 0.00, 0, 0.0),
    (0.24, -26, -12, -40, -150, 55, -0.08, 0.10, 0.00, -26, 0.35),
    (0.32, -28, -13, -48, -155, 62, -0.09, 0.11, 0.00, -28, 0.40),
    (0.45, 22, 22, 112, -150, -5, 0.10, -0.18, -0.36, 14, 0.30),
    (0.58, 30, 26, 140, -135, -15, -0.05, -0.22, -0.40, 20, 0.25),
    (0.78, 8, 6, 30, -30, -6, 0.02, -0.06, -0.12, -4, 0.0),
    (1.00, 0, 0, 0, 0, 0, 0.00, 0.00, 0.00, 0, 0.0),
]

# Oreilles (revu le 2026-11-19, Gabin : elles se pliaient trop fort pendant l'elan) : environ un tiers de
# l'amplitude d'avant (-28 / +20, x1,2 pour le haut), en retard sur le buste ; la colonne oreille_rx de ATK
# n'est plus utilisee. Derniere cle a 0,88 : avec le retard, la fin du clip revient exactement a 0
# (les attaques s'enchainent toutes les 0,45 s).
EAR_ATK = [(0.00, 0.0), (0.24, -8.0), (0.34, -9.0), (0.47, 5.0), (0.60, 6.0), (0.78, -1.0), (0.88, 0.0)]
EAR_LAG = (0.04, 0.08)     # retard (fraction du clip) de la base et du bout de l'oreille


def ear_rx(t, lag):
    return keyframes(max(0.0, t - lag), EAR_ATK)


def attack_pose(t):
    brz, brx, arx, ary, erz, sq, by, fy, orx, lids = keyframes(t, [(a[0], a[1:]) for a in ATK])
    ear_w = keyframes(t, [(0.0, 0.0), (0.2, 1.0), (0.8, 1.0), (1.0, 0.0)])
    step = keyframes(t, [(0.0, 0.0), (0.32, 0.0), (0.42, 1.0), (0.80, 1.0), (1.0, 0.0)])
    p = {
        "Bassin": dict(loc=(0, by, -0.12 * abs(sq) / 0.1), rot=(brx * 0.3, 0, brz * 0.4)),
        "Epaules": dict(rot=(brx, -brz * 0.15, brz * 0.6), scale=squash(sq)),
        "Tete": dict(rot=(-brx * 0.6, 0, -brz * 0.5)),
        "Epaule.R": mirror(dict(rot=(arx, ary, 0))),
        "Avant_Bras.R": mirror(dict(rot=(0, 0, erz))),
        "Poignet.R": mirror(dict(rot=(0, 0, -25 * smooth((t - 0.3) / 0.15)))),
        "Epaule.L": dict(rot=(-0.3 * max(0.0, arx), -30 - abs(ary) * 0.12, 0)),
        "Avant_Bras.L": dict(rot=(0, 0, -30)),
        "IK_Pied.R": dict(loc=(0, fy, 0.25 * math.sin(math.pi * min(1.0, max(0.0, (t - 0.30) / 0.14))) if 0.3 < t < 0.44 else 0.0),
                          rot=(-10 * step, 0, 0)),
        "IK_Pied.L": dict(loc=(0, 0.10 * step, on_ground(18 * step)), rot=(18 * step, 0, 0)),
        # oreilles : pli leger, en retard sur le buste (le bout suit la base) ; 0 exact au debut et a la fin
        "Oreille_Bas": dict(rot=(ear_rx(t, EAR_LAG[0]), -6 * ear_w, 0)),
        "Oreille_Haut": dict(rot=(ear_rx(t, EAR_LAG[1]) * 1.15, -8 * ear_w, 0)),
        "_lids": lids,
        "Controlleur_Yeux": dict(loc=(0, 0, -0.04)),
    }
    tail_pose(p, t, (10, 4, 0), -14 * (brz / 36.0) + 0.0001, lag=0.12)
    for i, b in enumerate(TAIL):    # la queue fouette a l'oppose du buste, en retard
        r = p[b]["rot"]
        p[b]["rot"] = (r[0], 0, -keyframes(max(0.0, t - 0.05 * (i + 1)), [(a[0], a[1]) for a in ATK]) * (0.5 + 0.3 * i))
    return p


c = Clip("Attaque", 18, False)
c.sample(attack_pose)
clips.append((c, False))


# ---------------------------------------------------------------------------------------------- Poussee
# 16 images : attaque a mains nues (repousse un mob ou un allie). Ramene les deux mains a la poitrine,
# puis pousse bras tendus en se jetant en avant, petit pas, retour.
PUSH = [  # (t, buste_rx, bras_rx, bras_ry, coude_rz, squash, bassin_y, pied_y, oreille_rx, lids)
    (0.00, 0, 0, 0, 0, 0.00, 0.00, 0.00, 0, 0.0),
    (0.25, -12, -45, 20, -110, -0.08, 0.12, 0.00, -24, 0.35),
    (0.40, 24, -112, 26, -5, 0.10, -0.24, -0.38, 16, 0.25),
    (0.55, 28, -118, 26, 0, -0.04, -0.28, -0.42, 20, 0.2),
    (0.80, 6, -25, 5, -8, 0.01, -0.06, -0.10, -4, 0.0),
    (1.00, 0, 0, 0, 0, 0.00, 0.00, 0.00, 0, 0.0),
]


def push_pose(t):
    brx, arx, ary, erz, sq, by, fy, orx, lids = keyframes(t, [(a[0], a[1:]) for a in PUSH])
    step = keyframes(t, [(0.0, 0.0), (0.28, 0.0), (0.40, 1.0), (0.80, 1.0), (1.0, 0.0)])
    p = {
        "Bassin": dict(loc=(0, by, -0.10 * abs(sq) / 0.1), rot=(brx * 0.3, 0, 0)),
        "Epaules": dict(rot=(brx, 0, 0), scale=squash(sq)),
        "Tete": dict(rot=(-brx * 0.5, 0, 0)),
        "Epaule": dict(rot=(arx, ary, 0)),
        "Avant_Bras": dict(rot=(0, 0, erz)),
        "Poignet": dict(rot=(0, 0, -35 * smooth((t - 0.3) / 0.1) * (1 - smooth((t - 0.6) / 0.3)))),
        "IK_Pied.R": dict(loc=(0, fy, 0.22 * math.sin(math.pi * min(1.0, max(0.0, (t - 0.26) / 0.14))) if 0.26 < t < 0.40 else 0.0)),
        "IK_Pied.L": dict(loc=(0, 0.10 * step, on_ground(18 * step)), rot=(18 * step, 0, 0)),
        "Oreille_Bas": dict(rot=(orx, -5, 0)),
        "Oreille_Haut": dict(rot=(orx * 1.2, -7, 0)),
        "_lids": lids,
    }
    tail_pose(p, t, (8 + brx * 0.5, 4, 0), 3, lag=0.12)
    return p


c = Clip("Poussee", 16, False)
c.sample(push_pose)
clips.append((c, False))


# ---------------------------------------------------------------------------------------------- Degats
# 18 images : le coup le projette en arriere (tete qui part, bras en avant, un pied decolle), yeux fermes
# tres fort, puis les yeux se rouvrent en grand et la tete tangue (sonne), retour.
HIT = [  # (t, buste_rx, tete_rx, bassin_y, squash, bras_rx, bras_ry, pied_z, oreille_rx, lids, oeil)
    (0.00, 0, 0, 0.00, 0.00, 0, 0, 0.00, 0, 0.0, 1.0),
    (0.10, -28, -30, 0.20, 0.10, -55, -55, 0.28, 34, 1.0, 1.0),
    (0.30, -22, -20, 0.16, -0.10, -40, -40, 0.10, 24, 1.0, 1.0),
    (0.45, -8, -4, 0.06, 0.03, -15, -15, 0.00, -10, 0.0, 1.22),
    (0.70, -2, 2, 0.02, -0.01, -4, -4, 0.00, 4, 0.0, 1.08),
    (1.00, 0, 0, 0.00, 0.00, 0, 0, 0.00, 0, 0.0, 1.0),
]


def hit_pose(t):
    brx, trx, by, sq, arx, ary, fz, orx, lids, eye = keyframes(t, [(a[0], a[1:]) for a in HIT])
    dizzy = math.sin(TWO_PI * 2.2 * max(0.0, t - 0.4)) * (1 - smooth((t - 0.4) / 0.6)) if t > 0.4 else 0.0
    p = {
        "Bassin": dict(loc=(0, by, -0.08 * (1 - abs(sq) / 0.1) * smooth(t / 0.1)), rot=(brx * 0.3, 0, 0)),
        "Epaules": dict(rot=(brx, 6 * dizzy, 0), scale=squash(sq)),
        "Tete": dict(rot=(trx, 10 * dizzy, 6 * dizzy)),
        "Epaule": dict(rot=(arx, ary, 0)),
        "Avant_Bras": dict(rot=(0, 0, -20 * smooth(t / 0.1))),
        "Poignet": dict(rot=(0, -30 * smooth(t / 0.1) * (1 - smooth((t - 0.4) / 0.5)), 0)),
        "IK_Pied.L": dict(loc=(0, 0.25 * fz, fz), rot=(-15 * fz / 0.28, 0, 0)),
        "Oreille_Bas": dict(rot=(orx, -10 * abs(dizzy), 0)),
        "Oreille_Haut": dict(rot=(orx * 1.3, -14 * dizzy, 0)),
        "_lids": lids,
        "Oeil": dict(scale=(eye,) * 3),
        "Controlleur_Yeux": dict(loc=(0.10 * dizzy, 0, 0.04 * abs(dizzy))),
    }
    tail_pose(p, t, (30 * (1 - smooth((t - 0.2) / 0.5)), 10, 0), 8 * dizzy + 0.0001, lag=0.1)
    return p


c = Clip("Degats", 18, False)
c.sample(hit_pose)
clips.append((c, False))


# ---------------------------------------------------------------------------------------------- A terre / mort
# Deroule du GDD (Sante_et_Degats) : a 0 coeur, le renard s'effondre sur le ventre (Effondrement), reste
# "a terre" ~30 s (AuSol : respire, regarde autour de lui, agite le bras pour appeler a l'aide) et peut
# ramper (Ramper). Un allie peut le relever (Relever). Sinon il meurt (Mort), puis le ragdoll prend le relais
# (corps transportable). Le corps bascule autour d'un pivot pose au sol devant les pieds, puis il est recale
# image par image sur le sol (point le plus bas du maillage).
LIE = 84.0                                   # bascule du corps quand il est couche sur le ventre (degres)
TIP_PIVOT = Vector((0, -0.55, GROUND))       # pivot de la bascule (au sol, devant les pieds)


def lying(th, extra=None, feet_up=0.0, feet_kick=(0.0, 0.0)):
    """Corps bascule de `th` degres vers l'avant ; `extra` = poses en plus (dictionnaire, la rotation de
    "Bassin" s'ajoute a la bascule). feet_up : pieds qui se relevent derriere (genoux plies)."""
    R = Matrix.Rotation(math.radians(th), 3, 'X')
    u = th / LIE
    p = {"Bassin": dict(loc=tuple(TIP_PIVOT + R @ (Vector((0, 0, 0)) - TIP_PIVOT)), rot=(th, 0, 0))}
    for side, kick in (("L", feet_kick[0]), ("R", feet_kick[1])):
        rest = HEAD["IK_Pied." + side]
        bend = feet_up + kick
        foot = Vector((rest.x, rest.y + 0.40 * bend, rest.z + 0.42 * bend))
        foot_w = TIP_PIVOT + R @ (foot - TIP_PIVOT)
        p["IK_Pied." + side] = dict(loc=tuple(foot_w - rest), rot=(th * 0.9 - 40 * bend + 25 * u, 0, 0))
    for k, v in (extra or {}).items():
        if k == "Bassin":
            v = dict(v)
            v["rot"] = add(p["Bassin"]["rot"], v.get("rot", (0, 0, 0)))
            v["loc"] = add(p["Bassin"]["loc"], v.get("loc", (0, 0, 0)))
        p[k] = v
    return p


def downed_upper(head_up=1.0):
    """Haut du corps quand il est couche : tete relevee (on voit son visage), bras tendus devant lui."""
    return {
        "Epaules": dict(rot=(-14 * head_up, 0, 0)),
        "Tete": dict(rot=(-46 * head_up, 0, 0)),
        "Epaule": dict(rot=(-150, 18, 0)),
        "Avant_Bras": dict(rot=(0, 0, -25)),
        "Oreille_Bas": dict(rot=(-10, -26, 0)),
        "Oreille_Haut": dict(rot=(-6, -30, 0)),
    }


# Effondrement : 30 images (1 s). Genoux qui flanchent, tete qui tourne, bascule en avant, rebond a l'impact.
def collapse_pose(t):
    th = LIE * keyframes(t, [(0.0, 0.0), (0.30, 0.04), (0.72, 1.0), (1.0, 1.0)], ease=lambda u: u * u)
    th += -6 * bump(t, 0.80, 0.08)                     # petit rebond quand il touche le sol
    wobble = math.sin(TWO_PI * 2.0 * t) * (1 - smooth((t - 0.1) / 0.4))
    sag = smooth(t / 0.3) * (1 - smooth((t - 0.45) / 0.2))
    u = smooth((t - 0.35) / 0.45)
    hit = bump(t, 0.78, 0.07)
    up = downed_upper(head_up=smooth((t - 0.8) / 0.2))
    extra = {
        "Bassin": dict(loc=(0, 0, -0.25 * sag), rot=(0, 6 * wobble, 4 * wobble), scale=squash(-0.10 * sag - 0.12 * hit)),
        "Epaules": dict(rot=lerp((8 * sag, -6 * wobble, 0), up["Epaules"]["rot"], u), scale=squash(-0.12 * hit)),
        "Tete": dict(rot=lerp((14 * sag, 12 * wobble, 8 * wobble), up["Tete"]["rot"], u)),
        "Epaule": dict(rot=lerp((0, 10 * sag, 0), (-150, 18, 0), smooth((t - 0.45) / 0.3))),
        "Avant_Bras": dict(rot=(0, 0, -25 * u)),
        "Oreille_Bas": dict(rot=(-10 * u, -12 * sag - 14 * u - 18 * bump(t, 0.8, 0.08), 0)),
        "Oreille_Haut": dict(rot=(-6 * u, -16 * sag - 14 * u - 24 * bump(t, 0.84, 0.08), 0)),
        "_lids": min(1.0, 0.45 * sag + 0.9 * hit + 0.2 * u),
        "Controlleur_Yeux": dict(loc=(0.12 * wobble, 0, 0.05 * sag)),
    }
    p = lying(th, extra, feet_up=0.6 * smooth((t - 0.7) / 0.3))
    tail_pose(p, t, (10 - 70 * u, 6 - 14 * u, 4 - 8 * u), 6 * wobble + 0.001, lag=0.12)
    return p


c = Clip("Effondrement", 30, False)
c.sample(collapse_pose)
clips.append((c, True))


# AuSol : boucle de 60 images (2 s). Couche sur le ventre, respire, regarde autour de lui, cligne,
# puis leve et agite le bras droit pour appeler a l'aide.
def downed_pose(t):
    br = c01(t * 2)
    wave = smooth((t - 0.45) / 0.08) * (1 - smooth((t - 0.85) / 0.08))
    wv = math.sin(TWO_PI * 6 * t)
    look = saccade(t, [(0.0, (0.0, 0.0)), (0.2, (0.15, 0.03)), (0.45, (-0.08, 0.06)), (0.8, (0.0, 0.0))], 0.06)
    up = downed_upper(head_up=0.85 + 0.15 * wave)
    extra = dict(up)
    extra["Bassin"] = dict(scale=(1 + 0.02 * br, 1 + 0.02 * br, 1 + 0.01 * br))
    extra["Epaules"] = dict(rot=add(up["Epaules"]["rot"], (2 * br, 0, 6 * wave)), scale=(1 + 0.03 * br, 1 + 0.04 * br, 1))
    extra["Tete"] = dict(rot=add(up["Tete"]["rot"], (3 * br, 0, look[0] * 50 - 10 * wave)))
    extra["Epaule.R"] = mirror(dict(rot=lerp((-150, 18, 0), (-170, -20, 0), wave)))
    extra["Avant_Bras.R"] = mirror(dict(rot=(0, 0, -25 + 30 * wave * wv)))
    extra["Poignet.R"] = mirror(dict(rot=(0, 0, 30 * wave * wv)))
    extra["Oreille_Haut"] = dict(rot=(-6, -30 + 18 * wave, 0))
    extra["Controlleur_Yeux"] = dict(loc=(look[0], 0, look[1]))
    extra["_lids"] = min(1.0, 0.25 * (1 - wave) + blink(t, 0.15) * 0.75 + blink(t, 0.62) * 0.75)
    p = lying(LIE, extra, feet_up=0.6 + 0.08 * s(t, 2), feet_kick=(0.12 * bump(t, 0.3, 0.1), 0.12 * bump(t, 0.7, 0.1)))
    tail_pose(p, t, (-58, -10, -4), 10, lag=0.12)   # queue posee au sol derriere lui
    return p


c = Clip("AuSol", 60, True)
c.sample(downed_pose)
clips.append((c, True))


# Ramper : boucle de 32 images (1,07 s, 2 tractions). Un bras tendu loin devant pendant que l'autre tire,
# le corps roule d'un cote a l'autre, les pieds poussent tour a tour, regard determine.
def crawl_pose(t):
    cy = co(t)
    sw = s(t)
    up = downed_upper(head_up=0.75)
    reach_l = 0.5 + 0.5 * cy          # 1 : bras gauche tendu loin devant
    reach_r = 1 - reach_l
    extra = dict(up)
    extra["Bassin"] = dict(rot=(0, 6 * sw, 5 * cy))
    extra["Epaules"] = dict(rot=add(up["Epaules"]["rot"], (0, -8 * sw, -10 * cy)))
    extra["Tete"] = dict(rot=add(up["Tete"]["rot"], (2 * c01(2 * t), 4 * sw, 6 * cy)))
    extra["Epaule.L"] = dict(rot=(-165 + 45 * (1 - reach_l), 10 + 20 * (1 - reach_l), 0))
    extra["Epaule.R"] = mirror(dict(rot=(-165 + 45 * (1 - reach_r), 10 + 20 * (1 - reach_r), 0)))
    extra["Avant_Bras.L"] = dict(rot=(0, 0, -10 - 55 * (1 - reach_l)))
    extra["Avant_Bras.R"] = mirror(dict(rot=(0, 0, -10 - 55 * (1 - reach_r))))
    extra["Oreille_Bas"] = dict(rot=(-14, -24 + 4 * s(t, 2), 0))
    extra["Oreille_Haut"] = dict(rot=(-10, -26 + 8 * s(t, 2, 0.15), 0))
    extra["_lids"] = 0.35 + 0.6 * blink(t, 0.55)
    extra["Controlleur_Yeux"] = dict(loc=(0, 0, 0.05))
    p = lying(LIE, extra, feet_up=0.35, feet_kick=(0.35 * max(0.0, -cy), 0.35 * max(0.0, cy)))
    tail_pose(p, t, (-55, -8, -2), 14, lag=0.14)
    return p


c = Clip("Ramper", 32, True)
c.sample(crawl_pose)
clips.append((c, True))


# Mort : 36 images (1,2 s). Dernier effort (releve la tete, tend le bras), puis tout retombe :
# tete au sol, yeux fermes, oreilles et queue a plat. La derniere image est tenue, puis ragdoll.
def death_pose(t):
    effort = bump(t, 0.25, 0.22)
    drop = smooth((t - 0.45) / 0.3)
    bounce = bump(t, 0.78, 0.06)
    up = downed_upper(head_up=0.85 + 0.25 * effort - 0.75 * drop)   # finit la joue au sol, visage visible
    extra = dict(up)
    extra["Tete"] = dict(rot=add(up["Tete"]["rot"], (12 * bounce, 22 * drop, 10 * drop)))
    extra["Epaules"] = dict(rot=add(up["Epaules"]["rot"], (0, 0, 4 * drop)), scale=squash(-0.06 * bounce))
    if t < 0.45:
        arm_r = lerp((-150, 18, 0), (-175, -10, 0), smooth(t / 0.3))
    else:
        arm_r = lerp((-175, -10, 0), (-140, 30, 0), drop)
    extra["Epaule.R"] = mirror(dict(rot=arm_r))
    extra["Epaule.L"] = dict(rot=lerp((-150, 18, 0), (-120, 34, 0), drop))
    extra["Avant_Bras"] = dict(rot=(0, 0, -25 + 20 * drop))
    extra["Oreille_Bas"] = dict(rot=(-10 - 10 * drop, -26 - 24 * drop - 8 * bounce, 0))
    extra["Oreille_Haut"] = dict(rot=(-6 - 8 * drop, -30 - 30 * drop - 12 * bounce, 0))
    extra["_lids"] = 0.3 + 0.7 * smooth((t - 0.4) / 0.25)
    extra["Controlleur_Yeux"] = dict(loc=(0, 0, 0.08 * effort))
    p = lying(LIE, extra, feet_up=0.6 * (1 - drop) + 0.1 * bounce, feet_kick=(0.2 * bump(t, 0.3, 0.1), 0.0))
    tail_pose(p, t, (-58 - 20 * drop, -10 - 4 * drop, -4 - 4 * drop), 6 * (1 - drop) + 0.001, lag=0.12)
    return p


c = Clip("Mort", 36, False)
c.sample(death_pose)
clips.append((c, True))


# Relever : 24 images (0,8 s). Pousse sur les bras, se redresse d'un coup avec un petit saut, secoue la tete.
def getup_pose(t):
    th = LIE * (1 - keyframes(t, [(0.0, 0.0), (0.15, 0.05), (0.6, 1.0), (1.0, 1.0)]))
    u = th / LIE
    shake = math.sin(TWO_PI * 3 * t) * bump(t, 0.82, 0.18)
    pop = bump(t, 0.6, 0.1)
    up = downed_upper(head_up=u)
    extra = {
        "Bassin": dict(loc=(0, 0, 0.15 * pop), scale=squash(0.10 * pop - 0.05 * bump(t, 0.72, 0.06))),
        "Epaules": dict(rot=lerp((0, 0, 0), up["Epaules"]["rot"], u)),
        "Tete": dict(rot=add(lerp((0, 0, 0), up["Tete"]["rot"], u), (0, 10 * shake, 18 * shake))),
        "Epaule": dict(rot=lerp((0, -30 * pop, 0), (-150 + 60 * bump(t, 0.25, 0.15), 18, 0), u)),
        "Avant_Bras": dict(rot=(0, 0, -25 * u - 40 * bump(t, 0.25, 0.15))),
        "Oreille_Bas": dict(rot=(-10 * u, -26 * u + 10 * pop + 8 * shake, 0)),
        "Oreille_Haut": dict(rot=(-6 * u, -30 * u + 16 * pop + 14 * shake, 0)),
        "_lids": min(1.0, 0.3 * u + 0.8 * bump(t, 0.82, 0.12)),
    }
    p = lying(th, extra, feet_up=0.6 * u)
    tail_pose(p, t, (-58 * u, -10 * u, -4 * u), 10 * shake + 0.001, lag=0.12)
    return p


c = Clip("Relever", 24, False)
c.sample(getup_pose)
clips.append((c, True))


# ---------------------------------------------------------------------------------------------- cuisson
made = []
for clip, gfix in clips:
    clip.bake(ground_fix=gfix)
    made.append(clip.name)
# pose neutre (repos) a la fin
rig.animation_data.action = None
for pb in rig.pose.bones:
    b = pb.name
    pb.rotation_quaternion = base_q(b)
    pb.location = BASE["base"][b]["loc"]
    pb.scale = BASE["base"][b]["scale"]
result = {"clips": made}
