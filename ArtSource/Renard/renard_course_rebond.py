# Course du renard = la course d'origine de Gabin (Renard.fbx, "Armature|Course") avec un rebond tres leger et lisse.
# A lancer dans Renard_Animations.blend APRES renard_anim.py (qui ne fait plus ni Marche ni Course).
# La marche d'origine n'est pas retouchee : Unity la prend directement dans Renard.fbx.
#
# Ce qui change par rapport a l'animation d'origine (tout le reste est recopie tel quel) :
#   - la hauteur du bassin est remplacee par une sinusoide douce (2 rebonds par cycle, meme phase et meme hauteur
#     moyenne que l'original, amplitude BOB) : l'original montait/descendait de ~0,10 (2,4 % de la taille du renard),
#     la version "balle qui rebondit" de 2026-11-18 de ~0,23 ; Gabin la trouvait trop sautillante (2026-11-19).
#     Le pied au sol ne bouge pas : c'est le genou qui plie ;
#   - plus d'ecrasement / etirement du corps (SQUASH = 0 ; il agrandissait tout le renard via le bassin) ;
#   - les oreilles, la queue et la tete suivent le rebond avec un leger retard, en sinusoide elle aussi.
#
# Timing : l'action d'origine a 12 poses a 24 i/s (le cycle de 2 foulees dure 12 images ; dans Unity le clip
# d'origine en durait 11, d'ou un petit accroc a chaque boucle). Elle est reechantillonnee en 15 images a 30 i/s
# (0,5 s) ; vitesse de l'etat Course dans Unity : 1,64 = meme rythme que l'original (0,306 s par cycle).
import bpy, math, os
from mathutils import Vector, Quaternion, Matrix

SRC = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo"
FBX = os.path.join(SRC, r"Assets\Art\Models\Characters\Renard.fbx")

# ---- reglages du rebond
BOB = 0.022         # demi-amplitude du rebond du bassin (unites Blender ; le renard fait ~4,2 de haut, oreilles comprises)
                    # -> 0,044 du creux au sommet, ~1 % de sa taille
SQUASH = 0.0        # ecrasement / etirement du corps (0 = aucun ; 0,01 = a peine visible)
EAR = 3.0           # degres : balancement des oreilles
EAR_LAG = 0.06      # retard des oreilles (fraction du cycle de 2 foulees)
TAIL = (1.5, 2.0, 2.5)
TAIL_LAG = (0.04, 0.07, 0.10)
HEAD = 1.0
HEAD_LAG = 0.03
OUT_FRAMES = 15     # images a 30 i/s pour un cycle
SRC_PERIOD = 12     # images de l'action d'origine (24 i/s) pour un cycle

scene = bpy.context.scene
scene.render.fps = 30
rig = bpy.data.objects["Armature"]
arm = rig.data
REST = {b.name: b.matrix_local.to_3x3() for b in arm.bones}
REST_INV = {k: v.inverted() for k, v in REST.items()}
UP = Vector((0, 0, 1))

# ---- 1. course d'origine, importee dans une armature temporaire
before_o, before_a = set(bpy.data.objects), set(bpy.data.actions)
win = bpy.context.window_manager.windows[0]
area = next((a for a in win.screen.areas if a.type == 'VIEW_3D'), win.screen.areas[0])
with bpy.context.temp_override(window=win, area=area):
    bpy.ops.import_scene.fbx(filepath=FBX)
scene.render.fps = 30          # l'import FBX remet la scene a 24 i/s : tous les clips seraient exportes 25 % trop lents
tmp_o = [o for o in bpy.data.objects if o not in before_o]
tmp_a = [a for a in bpy.data.actions if a not in before_a]
src = next(o for o in tmp_o if o.type == 'ARMATURE')
run = next(a for a in tmp_a if a.name.endswith("|Course"))
src.animation_data.action = run
src.animation_data.action_slot = run.slots[0]
first = int(run.frame_range[0])


def grab(k):
    """Pose d'origine a l'indice k (0..SRC_PERIOD-1, fractionnaire) : {os: (loc, quat, scale)}."""
    f = first + k
    scene.frame_set(int(math.floor(f)), subframe=f - math.floor(f))
    return {pb.name: tuple(x.copy() for x in pb.matrix_basis.decompose()) for pb in src.pose.bones}


def sample(k):
    last = SRC_PERIOD - 1
    if k <= last:
        return grab(k)
    a, b, u = grab(last), grab(0), k - last          # boucle : de la derniere pose vers la premiere
    out = {}
    for n in a:
        qb = b[n][1].copy()
        qb.make_compatible(a[n][1])
        out[n] = (a[n][0].lerp(b[n][0], u), a[n][1].slerp(qb, u), a[n][2].lerp(b[n][2], u))
    return out


poses = [sample(i * SRC_PERIOD / OUT_FRAMES) for i in range(OUT_FRAMES)]


PHASE = 0.0         # calee plus bas sur l'original (sommet du bassin)


def wave(u):
    """Rebond lisse, 2 par cycle : +1 au sommet (vol), -1 au creux (appui), sans angle a l'impact."""
    return math.cos(4 * math.pi * u - PHASE)


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


def rot_delta(bone, axis_arm, deg):
    """Rotation dans l'espace de l'armature, ramenee dans l'espace local de l'os (au repos)."""
    r = Matrix.Rotation(math.radians(deg), 3, axis_arm)
    return (REST_INV[bone] @ r @ REST[bone]).to_quaternion()


def lift_axis(bone):
    """Axe qui fait monter le bout de l'os quand l'angle est positif."""
    d = REST[bone] @ Vector((0, 1, 0))
    ax = d.cross(UP)
    if ax.length < 0.2:                              # os presque vertical : axe lateral du renard
        ax = Vector((1, 0, 0))
    return ax.normalized()


def follower(lead):
    f = "Controlleur_" + lead
    return f if f in rig.pose.bones and arm.bones[f].matrix_local.to_3x3() == arm.bones[lead].matrix_local.to_3x3() else None


def apply(p):
    for pb in rig.pose.bones:
        loc, q, sc = p[pb.name]
        pb.rotation_mode = 'QUATERNION'
        pb.location, pb.rotation_quaternion, pb.scale = loc, q, sc


# ---- 2. hauteur des pieds (pour savoir lequel est au sol) et du bassin de l'original
foot_z = {s: [] for s in ("L", "R")}
hip_z = []
for p in poses:
    apply(p)
    bpy.context.view_layer.update()
    for s in foot_z:
        foot_z[s].append(rig.pose.bones["IK_Pied." + s].head.z)
    hip_z.append(rig.pose.bones["Bassin"].head.z)
foot_min = {s: min(v) for s, v in foot_z.items()}
# phase et hauteur moyenne du bassin d'origine (1re harmonique a 2 rebonds par cycle)
hip_mean = sum(hip_z) / OUT_FRAMES
c = sum(z * math.cos(4 * math.pi * j / OUT_FRAMES) for j, z in enumerate(hip_z))
s_ = sum(z * math.sin(4 * math.pi * j / OUT_FRAMES) for j, z in enumerate(hip_z))
PHASE = math.atan2(s_, c)

# ---- 3. rebond par-dessus
old = bpy.data.actions.get("Course")
if old:
    bpy.data.actions.remove(old)
act = bpy.data.actions.new("Course")
act.use_fake_user = True
ad = rig.animation_data or rig.animation_data_create()
ad.action = act
ad.action_slot = act.slots.new(id_type='OBJECT', name=rig.name)

prev = {}
for i in list(range(OUT_FRAMES)) + [OUT_FRAMES]:
    j = i % OUT_FRAMES
    u = j / OUT_FRAMES
    p = {n: (v[0].copy(), v[1].copy(), v[2].copy()) for n, v in poses[j].items()}
    h = wave(u)
    # bassin (et son controleur) : hauteur d'origine remplacee par la sinusoide douce
    dz = hip_mean + BOB * h - hip_z[j]
    e = SQUASH * h                                   # etirement en vol, ecrasement a l'appui (0 par defaut)
    for b in ("Bassin", "Controlleur_Bassin"):
        loc, q, sc = p[b]
        loc = loc + REST_INV[b] @ Vector((0, 0, dz))
        xz = 1 / math.sqrt(1 + e)
        sc = Vector((sc.x * xz, sc.y * (1 + e), sc.z * xz))
        p[b] = (loc, q, sc)
    # pieds en l'air : suivent le rebond ; pied au sol : reste pose (le genou plie)
    for s in ("L", "R"):
        b = "IK_Pied." + s
        w = smooth((foot_z[s][j] - foot_min[s] - 0.04) / 0.15)
        loc, q, sc = p[b]
        p[b] = (loc + REST_INV[b] @ Vector((0, 0, dz * w)), q, sc)
    # retards : oreilles, queue, tete
    lagged = [(f"Oreille_Haut.{s}", EAR, EAR_LAG) for s in ("L", "R")]
    lagged += [(t, a, l) for t, a, l in zip(("Queue_Bas", "Queue_Milieu", "Queue_Bout"), TAIL, TAIL_LAG)]
    lagged += [("Tete", HEAD, HEAD_LAG)]
    for b, amp, lag in lagged:
        hl = wave(u - lag)
        dq = rot_delta(b, lift_axis(b), amp * hl)
        for n in (b, follower(b)):
            if n:
                loc, q, sc = p[n]
                p[n] = (loc, dq @ q, sc)
    apply(p)
    for pb in rig.pose.bones:
        if pb.name in prev:
            pb.rotation_quaternion.make_compatible(prev[pb.name])
        prev[pb.name] = pb.rotation_quaternion.copy()
        for path in ("rotation_quaternion", "location", "scale"):
            pb.keyframe_insert(path, frame=i, group=pb.name)

act.use_frame_range = True
act.frame_start, act.frame_end = 0, OUT_FRAMES
act.use_cyclic = True

# ---- 4. menage
for o in tmp_o:
    bpy.data.objects.remove(o, do_unlink=True)
for a in tmp_a:
    bpy.data.actions.remove(a)
rig.animation_data.action = None
result = {"course": act.name, "frames": OUT_FRAMES, "foot_min": foot_min,
          "hip_orig_amp": max(hip_z) - min(hip_z), "hip_new_amp": 2 * BOB, "phase": PHASE}
