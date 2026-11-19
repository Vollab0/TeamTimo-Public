# ABANDONNE le 2026-11-19 : pelage juge marbre / granuleux, remplace par unity_renard_pelage_uni.cs
# (couleur unie + leger degrade, Renard_Pelage_Uni.png). Garde pour reference ; ne pas relancer sans le vouloir.
# Pelage cartoon du renard : Renard_Pelage.png (couleur) + Renard_Pelage_Normal.png (relief des meches).
# A lancer apres renard_setup.py (scene du renard ouverte). Ne touche pas au modele : memes UV que Renard.png.
#
# Le Renard.png d'origine est en aplats (orange saumon 242/113/68 + creme) : avec la brillance de 0,5 du materiau,
# il donnait un aspect plastique qui ressortait fluo de nuit. Ici, chaque texel est recolore d'apres sa position 3D
# sur le renard au repos (comme les textures du taureau) :
#   - zones creme / orange reprises de Renard.png (ventre, joues, interieur des oreilles, bout de la queue...),
#     avec un bord un peu « touffu » ;
#   - orange renard plus profond et moins rose, plus sombre sur le dos et le dessus, plus clair dessous ;
#   - meches peintes (bruit etire dans le sens du poil : vers le bas sur le corps, vers l'arriere sur la tete
#     et la queue, le long des bras) en 3 tons, facon peinture cartoon ;
#   - « chaussettes » et « gants » brun fonce et bout des oreilles sombre, marques typiques du renard (SOCKS) ;
#   - occlusion ambiante douce cuite par sommet (creux sous la tete, entre les jambes).
# Changer une couleur = modifier PAL puis relancer ce script, puis unity_setup_renard_material.cs.
import bpy, os, time
import numpy as np

ROOT = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo"
TEX_DIR = os.path.join(ROOT, r"Assets\Art\Textures\Characters\Renard")
OLD_TEX = os.path.join(TEX_DIR, "Renard.png")
RES = globals().get("RES", 2048)
SOCKS = globals().get("SOCKS", False)    # Gabin (2026-11-18) : renard tout orange, sans mains / pieds bruns
AO_STRENGTH = 0.45
AO_DISTANCE = 0.45
NORMAL_STRENGTH = 2.2
ARM_Z = 1.116          # hauteur de l'origine de l'armature (le sol est a -ARM_Z dans son espace)


def srgb(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


PAL = {
    "orange": srgb("D2672C"),      # orange renard (moins rose et moins lumineux que 242/113/68)
    "orange_dark": srgb("A9481F"),  # dos, dessus de la tete, meches sombres
    "orange_light": srgb("E58A45"),  # flancs / dessous, meches claires
    "cream": srgb("F2E3CD"),
    "cream_shade": srgb("DCC6AA"),
    "sock": srgb("5A3427"),        # chaussettes / gants / bout des oreilles
}
t0 = time.time()
ob = bpy.data.objects["Renard"]
rig = bpy.data.objects["Armature"]
me = ob.data


# ------------------------------------------------------------------------------ occlusion ambiante par sommet
def bake_vertex_ao():
    scene = bpy.context.scene
    old_engine = scene.render.engine
    old_pose = rig.data.pose_position
    rig.data.pose_position = 'REST'
    keep = {"Renard", "Museau", "Oeil_Droit", "Oeil_Gauche", "Paupière_Droite", "Paupière_Gauche", "Sourcils"}
    hidden = [o for o in scene.objects if o.name not in keep and not o.hide_render]
    for o in hidden:
        o.hide_render = True
    old = me.color_attributes.get("AO")
    if old:
        me.color_attributes.remove(old)
    attr = me.color_attributes.new("AO", 'FLOAT_COLOR', 'POINT')
    me.color_attributes.active_color = attr
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 96
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.light_settings.distance = AO_DISTANCE
    for o in bpy.context.view_layer.objects:
        o.select_set(o == ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.bake(type='AO', target='VERTEX_COLORS')
    v = np.zeros(len(me.vertices) * 4, dtype=np.float32)
    me.color_attributes["AO"].data.foreach_get("color", v)
    me.color_attributes.remove(me.color_attributes["AO"])
    for o in hidden:
        o.hide_render = False
    scene.render.engine = old_engine
    rig.data.pose_position = old_pose
    return v.reshape(-1, 4)[:, 0].astype(np.float64)


AO_V = bake_vertex_ao()

# ------------------------------------------------------------------------------ donnees par sommet
nv = len(me.vertices)
co = np.zeros(nv * 3)
me.vertices.foreach_get("co", co)
co = co.reshape(-1, 3)
mw = np.array(ob.matrix_world)
P_v = co @ mw[:3, :3].T + mw[:3, 3] - np.array([0, 0, ARM_Z])     # espace de l'armature (repos)
nrm = np.zeros(nv * 3)
me.vertices.foreach_get("normal", nrm)
N_v = nrm.reshape(-1, 3) @ mw[:3, :3].T
N_v /= np.linalg.norm(N_v, axis=1, keepdims=True) + 1e-9

GROUPS = {
    "foot": ["Cheville.L", "Cheville.R", "Doigts_Pied.L", "Doigts_Pied.R", "Talon.L", "Talon.R"],
    "shin": ["Tibias.L", "Tibias.R"],
    "hand": ["Poignet.L", "Poignet.R", "Pouce.L", "Pouce.R", "Index.L", "Index.R", "Majeur.L", "Majeur.R",
             "Annulaire.L", "Annulaire.R", "Controlleur_Annulaire.L", "Controlleur_Annulaire.R"],
    "ear_top": ["Oreille_Haut.L", "Oreille_Haut.R"],
    "ear": ["Oreille_Bas.L", "Oreille_Bas.R", "Controlleur_Oreille_Bas.L", "Controlleur_Oreille_Bas.R"],
    "tail": ["Queue_Bas", "Queue_Milieu", "Queue_Bout"],
    "arm": ["Epaule.L", "Epaule.R", "Avant_Bras.L", "Avant_Bras.R"],
    "head": ["Tete"],
}
gidx = {g.index: g.name for g in ob.vertex_groups}
W_v = np.zeros((nv, len(GROUPS)))
gkeys = list(GROUPS)
name_to_k = {n: k for k, ns in enumerate(GROUPS.values()) for n in ns}
for v in me.vertices:
    for g in v.groups:
        k = name_to_k.get(gidx.get(g.group))
        if k is not None:
            W_v[v.index, k] += g.weight
# attributs interpoles : position (3), normale (3), poids (8), AO (1)
ATTR_V = np.concatenate([P_v, N_v, W_v, AO_V[:, None]], axis=1)
NA = ATTR_V.shape[1]

# ------------------------------------------------------------------------------ rasterisation des UV
me.calc_loop_triangles()
nt = len(me.loop_triangles)
tri_loops = np.zeros(nt * 3, dtype=np.int32)
me.loop_triangles.foreach_get("loops", tri_loops)
tri_verts = np.zeros(nt * 3, dtype=np.int32)
me.loop_triangles.foreach_get("vertices", tri_verts)
uv = np.zeros(len(me.loops) * 2)
me.uv_layers.active.data.foreach_get("uv", uv)
T_uv = uv.reshape(-1, 2)[tri_loops.reshape(-1, 3)] * RES
T_at = ATTR_V[tri_verts.reshape(-1, 3)]

idx_list, at_list, sc_list = [], [], []
PAD = 0.75
for i in range(nt):
    (x0, y0), (x1, y1), (x2, y2) = T_uv[i]
    den = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
    if abs(den) < 1e-12:
        continue
    xa, xb = int(max(0, np.floor(min(x0, x1, x2) - PAD))), int(min(RES - 1, np.ceil(max(x0, x1, x2) + PAD)))
    ya, yb = int(max(0, np.floor(min(y0, y1, y2) - PAD))), int(min(RES - 1, np.ceil(max(y0, y1, y2) + PAD)))
    if xb < xa or yb < ya:
        continue
    PX, PY = np.meshgrid(np.arange(xa, xb + 1) + 0.5, np.arange(ya, yb + 1) + 0.5)
    w0 = ((y1 - y2) * (PX - x2) + (x2 - x1) * (PY - y2)) / den
    w1 = ((y2 - y0) * (PX - x2) + (x0 - x2) * (PY - y2)) / den
    w2 = 1.0 - w0 - w1
    e0, e1, e2 = np.hypot(x2 - x1, y2 - y1), np.hypot(x0 - x2, y0 - y2), np.hypot(x1 - x0, y1 - y0)
    a2 = abs(den)
    inside = (w0 * a2 / e0 > -PAD) & (w1 * a2 / e1 > -PAD) & (w2 * a2 / e2 > -PAD)
    if not inside.any():
        continue
    w0, w1, w2 = w0[inside], w1[inside], w2[inside]
    score = np.minimum(np.minimum(w0 * a2 / e0, w1 * a2 / e1), w2 * a2 / e2)
    at = w0[:, None] * T_at[i, 0] + w1[:, None] * T_at[i, 1] + w2[:, None] * T_at[i, 2]
    idx_list.append(PY[inside].astype(np.int64) * RES + PX[inside].astype(np.int64))
    at_list.append(at)
    sc_list.append(score)
idx = np.concatenate(idx_list)
at = np.concatenate(at_list)
sc = np.concatenate(sc_list)
order = np.lexsort((sc, idx))
idx, at = idx[order], at[order]
last = np.ones(len(idx), dtype=bool)
last[:-1] = idx[1:] != idx[:-1]
idx, at = idx[last], at[last]
P = at[:, 0:3]
N = at[:, 3:6] / (np.linalg.norm(at[:, 3:6], axis=1, keepdims=True) + 1e-9)
W = {k: at[:, 6 + i] for i, k in enumerate(gkeys)}
AO = np.clip(at[:, 6 + len(gkeys)], 0, 1)
t_raster = time.time() - t0

# ------------------------------------------------------------------------------ ancienne texture (zones creme)
old = bpy.data.images.load(OLD_TEX, check_existing=False)
ow, oh = old.size
opx = np.zeros(ow * oh * 4, dtype=np.float32)
old.pixels.foreach_get(opx)      # image 8 bits : valeurs sRGB brutes (octet / 255)
opx = opx.reshape(oh, ow, 4)[..., :3]
bpy.data.images.remove(old)
step = ow // RES
opx = opx.reshape(RES, step, RES, step, 3).mean(axis=(1, 3))     # sous-echantillonnage par blocs


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


# bruit de valeur 3D (reseau entier hache + interpolation trilineaire lissee)
def vnoise(q, seed=0):
    i = np.floor(q).astype(np.int64)
    f = q - i
    u = f * f * (3 - 2 * f)
    out = np.zeros(len(q))
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                h = ((i[:, 0] + dx) * 73856093) ^ ((i[:, 1] + dy) * 19349663) ^ ((i[:, 2] + dz) * 83492791) ^ (seed * 2654435761)
                h = (h ^ (h >> 13)) * 1274126177
                r = ((h ^ (h >> 16)) & 0xFFFF) / 65535.0
                wx = u[:, 0] if dx else 1 - u[:, 0]
                wy = u[:, 1] if dy else 1 - u[:, 1]
                wz = u[:, 2] if dz else 1 - u[:, 2]
                out += r * wx * wy * wz
    return out


def fbm(q, octaves=3, seed=0):
    tot, amp, norm = 0.0, 1.0, 0.0
    for o in range(octaves):
        tot = tot + amp * vnoise(q * (2 ** o), seed + o)
        norm += amp
        amp *= 0.5
    return tot / norm


# sens du poil : vers le bas (corps, jambes), vers l'arriere (tete, queue), le long des bras
head_w = smoothstep(1.15, 1.45, P[:, 2]) * (1 - W["arm"] - W["hand"]).clip(0, 1)
tail_w = np.clip(W["tail"] * 1.5, 0, 1)
arm_w = np.clip((W["arm"] + W["hand"]) * 1.2, 0, 1) * (1 - head_w)
wy = np.clip(head_w + tail_w, 0, 1)
wx = arm_w * (1 - wy)
wz = np.clip(1 - wx - wy, 0, 1)
STRAND = 13.0        # frequence en travers du poil (par metre) : grosses meches peintes
ALONG = 2.2          # frequence le long du poil
nz = fbm(P * np.array([STRAND, STRAND, ALONG]), 2, 1)
ny = fbm(P * np.array([STRAND, ALONG, STRAND]), 2, 2)
nx = fbm(P * np.array([ALONG, STRAND, STRAND]), 2, 3)
strands = nz * wz + ny * wy + nx * wx
clumps = fbm(P * 3.0, 2, 7)                   # grosses meches (ombre douce)
fur = 0.65 * strands + 0.35 * clumps

# zones creme d'origine, bord legerement touffu (decalage UV par le bruit de meches)
iy, ix = idx // RES, idx % RES
jit = (fur - 0.5) * 7.0
jx = np.clip(ix + (jit * 1.0).astype(np.int64), 0, RES - 1)
jy = np.clip(iy + ((nz - 0.5) * 7.0).astype(np.int64), 0, RES - 1)
oc = opx[jy, jx]
oc_exact = opx[iy, ix]
orange_old, cream_old = np.array([242, 113, 68]) / 255.0, np.array([252, 229, 204]) / 255.0
seg = cream_old - orange_old


def classify(c):
    t = np.clip(((c - orange_old) @ seg) / (seg @ seg), 0, 1)
    dist = np.linalg.norm(c - (orange_old + t[:, None] * seg), axis=1)
    return t, dist


t_cream, _ = classify(oc)
_, dist_exact = classify(oc_exact)
cream = smoothstep(0.35, 0.65, t_cream)
other = smoothstep(0.06, 0.12, dist_exact)    # autres couleurs (coussinets roses, trait de la bouche) : gardees

# ------------------------------------------------------------------------------ couleurs
up = np.clip(N[:, 2], -1, 1)
back = np.clip(N[:, 1], -1, 1)
top = smoothstep(-0.2, 0.9, 0.75 * up + 0.45 * back)          # dessus / dos -> plus sombre
under = smoothstep(0.1, 0.8, -up)                             # dessous -> plus clair
base = PAL["orange"][None, :] * np.ones((len(P), 1))
base = base + (PAL["orange_dark"] - PAL["orange"]) * (0.55 * top)[:, None]
base = base + (PAL["orange_light"] - PAL["orange"]) * (0.45 * under)[:, None]
# meches en 3 tons (peinture cartoon) : sombres / claires nettes, adoucies par les grosses meches
dark_st = smoothstep(0.38, 0.30, strands) * 0.7
light_st = smoothstep(0.64, 0.72, strands) * 0.55
base = base + (PAL["orange_dark"] - base) * dark_st[:, None] * 0.55
base = base + (PAL["orange_light"] - base) * light_st[:, None] * 0.5
base = base * (0.97 + 0.06 * clumps)[:, None]

if SOCKS:
    # pieds jusqu'a la cheville et mains jusqu'au poignet seulement (bord en meches)
    foot = smoothstep(-0.62, -0.80, P[:, 2] + (fur - 0.5) * 0.10) * smoothstep(0.2, 0.5, W["foot"] + W["shin"])
    hand = smoothstep(0.45, 0.8, np.clip(W["hand"], 0, 1) + (fur - 0.5) * 0.35)
    # bout des oreilles (meme hauteur des deux cotes), surtout a l'arriere
    ear_back = smoothstep(-0.2, 0.3, N[:, 1])
    ear_zone = smoothstep(0.3, 0.6, W["ear"] + W["ear_top"]) * smoothstep(0.35, 0.5, np.abs(P[:, 0]))
    ear_tip = ear_zone * smoothstep(3.0, 3.2, P[:, 2] + (fur - 0.5) * 0.12)
    sock = np.clip(np.maximum(np.maximum(foot, hand), ear_tip * (0.4 + 0.6 * ear_back)), 0, 1)
    sock_col = PAL["sock"][None, :] * (0.9 + 0.2 * strands)[:, None]
    base = base + (sock_col - base) * sock[:, None]
else:
    sock = np.zeros(len(P))

cream_col = PAL["cream"][None, :] + (PAL["cream_shade"] - PAL["cream"])[None, :] * (0.5 * top + 0.35 * smoothstep(0.42, 0.3, strands))[:, None]
col = base + (cream_col - base) * (cream * (1 - sock * 0.85))[:, None]
col = col + (oc_exact - col) * other[:, None]
col = col * (1 - AO_STRENGTH + AO_STRENGTH * smoothstep(0.15, 0.95, AO))[:, None]

# ------------------------------------------------------------------------------ hauteur des meches -> normal map
height = 0.7 * strands + 0.3 * clumps
height = height * (1 - 0.6 * other)


def dilate(img, filled, iterations=12):
    for _ in range(iterations):
        acc = np.zeros_like(img)
        cnt = np.zeros(filled.shape, dtype=np.float32)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            sh_f = np.roll(filled, (dy, dx), axis=(0, 1))
            acc += np.roll(img, (dy, dx), axis=(0, 1)) * sh_f[..., None]
            cnt += sh_f
        new = (~filled) & (cnt > 0)
        img[new] = acc[new] / cnt[new][:, None]
        filled = filled | new
    return img, filled


def to_image(values, nch, background):
    img = np.empty((RES * RES, nch), dtype=np.float32)
    img[:] = background
    filled = np.zeros(RES * RES, dtype=bool)
    img[idx] = values
    filled[idx] = True
    img, _ = dilate(img.reshape(RES, RES, nch), filled.reshape(RES, RES))
    return img


def save(name, rgb):
    old_img = bpy.data.images.get(name)
    if old_img:
        bpy.data.images.remove(old_img)
    bimg = bpy.data.images.new(name, RES, RES, alpha=False)
    rgba = np.ones((RES, RES, 4), dtype=np.float32)
    rgba[..., :3] = np.clip(rgb, 0, 1)
    bimg.pixels.foreach_set(rgba.ravel())
    bimg.filepath_raw = os.path.join(TEX_DIR, name + ".png")
    bimg.file_format = 'PNG'
    bimg.save()
    return bimg


albedo = to_image(col, 3, PAL["orange"])
save("Renard_Pelage", albedo)

H = to_image(height[:, None], 1, 0.5)[..., 0]
dhdx = (np.roll(H, -1, 1) - np.roll(H, 1, 1)) * 0.5
dhdy = (np.roll(H, -1, 0) - np.roll(H, 1, 0)) * 0.5
n = np.stack([-dhdx * NORMAL_STRENGTH, -dhdy * NORMAL_STRENGTH, np.ones_like(H)], axis=-1)
n /= np.linalg.norm(n, axis=-1, keepdims=True)
save("Renard_Pelage_Normal", n * 0.5 + 0.5)

# couleurs a reporter sur les materiaux unis (paupieres = orange du dessus de la tete, sourcils = creme)
head_top = (P[:, 2] > 1.9) & (cream < 0.1) & (sock < 0.1) & (N[:, 1] < 0.2)
lid = col[head_top].mean(axis=0)
result = {
    "texels": int(len(idx)), "raster_s": round(t_raster, 1), "total_s": round(time.time() - t0, 1),
    "lid_srgb": [round(float(x), 3) for x in lid], "cream_srgb": [round(float(x), 3) for x in PAL["cream"]],
}
