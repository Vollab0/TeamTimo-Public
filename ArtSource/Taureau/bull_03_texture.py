# Etape 3 : textures du taureau (jour, nuit, emission de nuit) + materiaux Blender.
# Chaque texel est colore d'apres sa position 3D sur le modele au repos (taches du ventre, museau, iris...) :
# pas de peinture a la main, les limites de couleurs sont nettes et independantes du maillage.
# Changer une couleur = modifier DAY / NIGHT ci-dessous puis relancer ce script.
import bpy
import os
import time
import numpy as np

COMMON = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo\ArtSource\Taureau\bull_common.py"
exec(open(COMMON, encoding="utf-8").read())

RES = globals().get("RES", 2048)
os.makedirs(TEX_DIR, exist_ok=True)
t0 = time.time()

DAY = {k: np.array(srgb(v)) for k, v in {
    "fur": "A2654A", "belly": "EDE2D2", "muzzle": "F1E8DB", "horn": "F2EBDD", "hoof": "5B4138", "lid": "955B42",
    "brow": "EBD3B8", "ear_in": "E6D2BE", "line": "6A4534", "tuft": "5A382B",
    "eye_white": "F8F8F5", "iris": "55A865", "iris_rim": "2C6B3E", "pupil": "0D0D0F", "shine": "FFFFFF"}.items()}
NIGHT = {k: np.array(srgb(v)) for k, v in {
    "fur": "2E2528", "belly": "6F6862", "muzzle": "7A726B", "horn": "BDB5A8", "hoof": "151214", "lid": "221B1E",
    "brow": "8A8078", "ear_in": "5C4F4C", "line": "120E10", "tuft": "0F0C0E",
    "eye_white": "FF2A1A", "iris": "FF5A2A", "iris_rim": "C81008", "pupil": "FFC890", "shine": "FFE8D8"}.items()}

PART = {"body": 0, "hoof": 1, "horn": 2, "eye_L": 3, "eye_R": 4, "lid_L": 5, "lid_R": 6,
        "brow_L": 7, "brow_R": 8, "tail": 9, "tuft": 10, "ear_L": 11, "ear_R": 12}
GAZE_DOWN = 12.0       # ... et regardent un peu vers le bas
GAZE_IN = 8.0          # les iris convergent legerement (degres), comme sur la reference
IRIS_DEG, PUPIL_DEG = 30.0, 19.0


def soft(d, w=0.004):
    """1 a l'interieur (d < 0), 0 a l'exterieur, bord adouci sur w metres."""
    return np.clip(0.5 - d / w, 0.0, 1.0)


def mix(col, new, m):
    col += (np.asarray(new)[None, :] - col) * m[:, None]


def shade(P, part, pal, emission=False):
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    ax = np.abs(x)
    n = len(P)
    col = np.empty((n, 3), dtype=np.float32)
    col[:] = pal["fur"]
    b = (part == PART["body"]).astype(np.float32)

    # --- Tache creme du ventre (superellipse vue de face)
    wide = 1.0 + 0.07 * (z - BELLY_C[2]) / BELLY_RZ          # un peu plus large en haut
    e = (np.abs(x / (BELLY_RX * wide)) ** 2.3 + np.abs((z - BELLY_C[2]) / BELLY_RZ) ** 2.3) ** (1 / 2.3) - 1.0
    mix(col, pal["belly"], soft(e * BELLY_RZ, 0.006) * soft(y + 0.06, 0.03) * b)

    # --- Ombre douce sous les pectoraux et dans le sillon du torse (lit le relief meme en lumiere plate)
    chest = soft(y + 0.10, 0.04) * b
    z_arc = 1.085 - 0.045 * np.sin(np.pi * np.clip(ax / 0.34, 0, 1)) + 0.03 * np.clip(ax / 0.34, 0, 1) ** 2
    crease = np.exp(-((z - z_arc) / 0.016) ** 2) * soft(ax - 0.36, 0.05)
    cleft = np.exp(-(x / 0.012) ** 2) * soft(1.09 - z, 0.03) * soft(z - 1.30, 0.04)
    col *= (1.0 - 0.13 * np.maximum(crease, cleft) * chest)[:, None]

    # --- Museau creme, narines en arche et bouche
    d_muz = rbox(MUZZLE_C, MUZZLE_HALF, MUZZLE_RAD)(x, y, z) - 0.008
    mix(col, pal["muzzle"], soft(d_muz) * soft(y + 0.30, 0.01) * b)
    front = soft(y + 0.43, 0.01) * b
    nx, nz = ax - 0.105, z - 1.527
    ring = np.sqrt((nx / 0.034) ** 2 + (nz / 0.038) ** 2)
    mix(col, pal["line"], soft(np.abs(ring - 1.0) * 0.036 - 0.0095, 0.003) * soft(-nz - 0.016, 0.006) * front)
    t = np.clip(ax / 0.088, 0.0, 1.0)
    zc = 1.390 - 0.014 * t * t
    d_mouth = np.sqrt(np.maximum(ax - 0.088, 0.0) ** 2 + (z - zc) ** 2) - 0.0075
    mix(col, pal["line"], soft(d_mouth, 0.003) * front)

    # --- Interieur clair des oreilles (face avant)
    axis = EAR_TIP - EAR_ROOT
    L = float(np.hypot(axis[0], axis[2]))
    ux, uz = axis[0] / L, axis[2] / L
    rx_, rz_ = ax - EAR_ROOT[0], z - EAR_ROOT[2]
    te = (rx_ * ux + rz_ * uz) / L
    q = np.abs(-rx_ * uz + rz_ * ux)
    rad = np.where(te < 0.40, 0.044 + 0.022 * te / 0.40, 0.066 - 0.056 * (te - 0.40) / 0.60)
    y_mid = EAR_ROOT[1] + axis[1] * te
    is_ear = ((part == PART["ear_L"]) | (part == PART["ear_R"])).astype(np.float32)
    m_ear = soft(q - 0.62 * rad, 0.004) * soft(0.16 - te, 0.03) * soft(te - 0.90, 0.03) * soft(y - y_mid + 0.004, 0.004)
    mix(col, pal["ear_in"], m_ear * is_ear)
    # liseré fonce sur le bord de l'oreille (reference), des deux cotes
    m_rim = np.maximum(1.0 - soft(q - 0.90 * rad, 0.008), 1.0 - soft(te - 0.94, 0.02))
    mix(col, pal["tuft"], 0.85 * m_rim * is_ear)

    # --- Toupet entre les cornes : un peu plus fonce que le pelage
    m_tuft = soft(1.925 - z, 0.02) * soft(ax - 0.15, 0.03) * soft(-0.24 - y, 0.03) * soft(y + 0.02, 0.03)
    mix(col, pal["tuft"], 0.40 * m_tuft * b)

    # --- Pieces a couleur unie
    for key, name in (("hoof", "hoof"), ("horn", "horn"), ("lid_L", "lid"), ("lid_R", "lid"),
                      ("brow_L", "brow"), ("brow_R", "brow"), ("tuft", "tuft")):
        col[part == PART[key]] = pal[name]

    # --- Yeux : iris, pupille et reflet peints d'apres l'angle avec la direction du regard
    emis = np.zeros((n, 3), dtype=np.float32)
    for key, sx in (("eye_L", 1.0), ("eye_R", -1.0)):
        sel = part == PART[key]
        if not sel.any():
            continue
        c = np.array((EYE_C[0] * sx, EYE_C[1], EYE_C[2]))
        d = P[sel] - c
        d /= np.linalg.norm(d, axis=1)[:, None]
        gi = np.radians(GAZE_IN)
        gd = np.radians(GAZE_DOWN)
        g = np.array((-np.sin(gi) * sx * np.cos(gd), -np.cos(gi) * np.cos(gd), -np.sin(gd)))
        ang = np.degrees(np.arccos(np.clip(d @ g, -1, 1)))
        c_eye = np.empty((len(d), 3), dtype=np.float32)
        c_eye[:] = pal["eye_white"]
        rim = np.clip((ang / IRIS_DEG - 0.55) / 0.45, 0, 1)
        iris = pal["iris"][None, :] + (pal["iris_rim"] - pal["iris"])[None, :] * (rim * rim)[:, None]
        m_iris = soft((ang - IRIS_DEG) * 0.00175, 0.003)
        c_eye += (iris - c_eye) * m_iris[:, None]
        mix(c_eye, pal["pupil"], soft((ang - PUPIL_DEG) * 0.00175, 0.003))
        h = g + np.array((-0.16, 0.0, 0.17))
        h /= np.linalg.norm(h)
        ang_h = np.degrees(np.arccos(np.clip(d @ h, -1, 1)))
        mix(c_eye, pal["shine"], soft((ang_h - 4.8) * 0.00175, 0.002))
        col[sel] = c_eye
        emis[sel] = c_eye
    return emis if emission else col


# --------------------------------------------------------------------------------------
# Rasterisation : pour chaque texel couvert par un triangle, sa position 3D au repos
# --------------------------------------------------------------------------------------
OBJECTS = ["Bull_Body", "Bull_Eye_L", "Bull_Eye_R", "Bull_Lid_L", "Bull_Lid_R"]   # tous partagent la meme texture

# Occlusion ambiante : cuite par Cycles sur les sommets (pose de repos), puis interpolee dans chaque texel et multipliee
# dans l'albedo (creux, aisselles, sous le museau). Par sommet plutot que dans une image : les pieces qui partagent
# leurs UV (cotes gauche / droit) ne se melangent pas, et le resultat est lisse.
AO_STRENGTH = 0.35       # 0 = pas d'occlusion, 1 = occlusion pleine
AO_DISTANCE = 0.18       # metres


def bake_vertex_ao():
    scene = bpy.context.scene
    old_engine = scene.render.engine
    hidden = [o for o in scene.objects if o.name not in OBJECTS and not o.hide_render]
    for o in hidden:
        o.hide_render = True
    for name in OBJECTS:
        me_ = bpy.data.objects[name].data
        old = me_.color_attributes.get("AO")
        if old:
            me_.color_attributes.remove(old)
        attr = me_.color_attributes.new("AO", 'FLOAT_COLOR', 'POINT')
        me_.color_attributes.active_color = attr
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 128
    scene.world.light_settings.distance = AO_DISTANCE
    for o in bpy.context.view_layer.objects:
        o.select_set(o.name in OBJECTS)
    bpy.context.view_layer.objects.active = bpy.data.objects["Bull_Body"]
    bpy.ops.object.bake(type='AO', target='VERTEX_COLORS')
    out = {}
    for name in OBJECTS:
        me_ = bpy.data.objects[name].data
        attr = me_.color_attributes["AO"]
        v = np.zeros(len(me_.vertices) * 4, dtype=np.float32)
        attr.data.foreach_get("color", v)
        out[name] = v.reshape(-1, 4)[:, 0].astype(np.float64)
        me_.color_attributes.remove(attr)
    for o in hidden:
        o.hide_render = False
    scene.render.engine = old_engine
    return out


AO_V = bake_vertex_ao()
uv_l, co_l, part_l = [], [], []
for name in OBJECTS:
    me = bpy.data.objects[name].data
    me.calc_loop_triangles()
    nt = len(me.loop_triangles)
    tri_loops = np.zeros(nt * 3, dtype=np.int32)
    me.loop_triangles.foreach_get("loops", tri_loops)
    tri_verts = np.zeros(nt * 3, dtype=np.int32)
    me.loop_triangles.foreach_get("vertices", tri_verts)
    tri_poly = np.zeros(nt, dtype=np.int32)
    me.loop_triangles.foreach_get("polygon_index", tri_poly)
    co = np.zeros(len(me.vertices) * 3, dtype=np.float64)
    me.vertices.foreach_get("co", co)
    uv = np.zeros(len(me.loops) * 2, dtype=np.float64)
    me.uv_layers.active.data.foreach_get("uv", uv)
    face_part = np.zeros(len(me.polygons), dtype=np.int32)
    me.attributes["part"].data.foreach_get("value", face_part)
    uv_l.append(uv.reshape(-1, 2)[tri_loops.reshape(-1, 3)] * RES)      # (nt, 3, 2) en pixels
    co4 = np.concatenate([co.reshape(-1, 3), AO_V[name][:, None]], axis=1)   # position + occlusion
    co_l.append(co4[tri_verts.reshape(-1, 3)])                           # (nt, 3, 4)
    part_l.append(face_part[tri_poly])
T_uv = np.concatenate(uv_l)
T_co = np.concatenate(co_l)
T_part = np.concatenate(part_l)
nt = len(T_part)

idx_list, pos_list, part_list = [], [], []
PAD = 0.75                            # deborde un peu des triangles (couvre les coutures)
for i in range(nt):
    (x0, y0), (x1, y1), (x2, y2) = T_uv[i]
    den = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
    if abs(den) < 1e-12:
        continue
    xa, xb = int(max(0, np.floor(min(x0, x1, x2) - PAD))), int(min(RES - 1, np.ceil(max(x0, x1, x2) + PAD)))
    ya, yb = int(max(0, np.floor(min(y0, y1, y2) - PAD))), int(min(RES - 1, np.ceil(max(y0, y1, y2) + PAD)))
    if xb < xa or yb < ya:
        continue
    px = np.arange(xa, xb + 1) + 0.5
    py = np.arange(ya, yb + 1) + 0.5
    PX, PY = np.meshgrid(px, py)
    w0 = ((y1 - y2) * (PX - x2) + (x2 - x1) * (PY - y2)) / den
    w1 = ((y2 - y0) * (PX - x2) + (x0 - x2) * (PY - y2)) / den
    w2 = 1.0 - w0 - w1
    # tolerance : distance au bord exprimee en pixels
    e0 = np.hypot(x2 - x1, y2 - y1)
    e1 = np.hypot(x0 - x2, y0 - y2)
    e2 = np.hypot(x1 - x0, y1 - y0)
    area2 = abs(den)
    inside = (w0 * area2 / e0 > -PAD) & (w1 * area2 / e1 > -PAD) & (w2 * area2 / e2 > -PAD)
    if not inside.any():
        continue
    w0, w1, w2 = w0[inside], w1[inside], w2[inside]
    # score : les texels vraiment dans le triangle l'emportent sur les debordements
    score = np.minimum(np.minimum(w0 * area2 / e0, w1 * area2 / e1), w2 * area2 / e2)
    pos = w0[:, None] * T_co[i, 0] + w1[:, None] * T_co[i, 1] + w2[:, None] * T_co[i, 2]
    flat = (PY[inside].astype(np.int64)) * RES + PX[inside].astype(np.int64)
    idx_list.append(flat)
    pos_list.append(np.concatenate([pos, score[:, None]], axis=1))
    part_list.append(np.full(len(flat), T_part[i], dtype=np.int32))

idx = np.concatenate(idx_list)
pos = np.concatenate(pos_list)
prt = np.concatenate(part_list)
# un texel touche par plusieurs triangles garde celui ou il est le plus a l'interieur
order = np.lexsort((pos[:, 4], idx))
idx, pos, prt = idx[order], pos[order], prt[order]
last = np.ones(len(idx), dtype=bool)
last[:-1] = idx[1:] != idx[:-1]
idx, ao_tex, pos, prt = idx[last], pos[last, 3], pos[last, :3], prt[last]
AO_MUL = 1.0 - AO_STRENGTH + AO_STRENGTH * np.clip(ao_tex, 0.0, 1.0)
AO_MUL[(prt == PART["eye_L"]) | (prt == PART["eye_R"])] = 1.0     # les yeux restent nets (blanc franc)
t_raster = time.time() - t0


def dilate(img, filled, iterations=10):
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
    return img


def build(name, pal, filename, emission=False, background=None):
    rgb = shade(pos, prt, pal, emission)
    if not emission:
        rgb = rgb * AO_MUL[:, None]
    img = np.zeros((RES * RES, 3), dtype=np.float32)
    filled = np.zeros(RES * RES, dtype=bool)
    img[idx] = rgb
    filled[idx] = True
    img = dilate(img.reshape(RES, RES, 3), filled.reshape(RES, RES))
    if background is not None:
        empty = ~dilate_mask
        img[empty] = background
    old = bpy.data.images.get(name)
    if old:
        bpy.data.images.remove(old)
    bimg = bpy.data.images.new(name, RES, RES, alpha=False)
    rgba = np.ones((RES, RES, 4), dtype=np.float32)
    rgba[..., :3] = np.clip(img, 0, 1)        # image 8 bits : valeurs deja en sRGB
    bimg.pixels.foreach_set(rgba.ravel())
    bimg.filepath_raw = os.path.join(TEX_DIR, filename)
    bimg.file_format = 'PNG'
    bimg.save()
    return bimg


# masque des texels atteints apres dilatation (pour remplir le reste d'une couleur neutre)
_f = np.zeros(RES * RES, dtype=bool)
_f[idx] = True
_f = _f.reshape(RES, RES)
for _ in range(10):
    _f = _f | np.roll(_f, 1, 0) | np.roll(_f, -1, 0) | np.roll(_f, 1, 1) | np.roll(_f, -1, 1)
dilate_mask = _f

img_day = build("Bull_Albedo_Day", DAY, "Bull_Albedo_Day.png", background=DAY["fur"])
img_night = build("Bull_Albedo_Night", NIGHT, "Bull_Albedo_Night.png", background=NIGHT["fur"])
img_emis = build("Bull_Emission_Night", NIGHT, "Bull_Emission_Night.png", emission=True, background=(0, 0, 0))


def make_material(name, albedo, emission=None):
    mat = bpy.data.materials.get(name)
    if mat:
        bpy.data.materials.remove(mat)
    mat = bpy.data.materials.new(name)
    nt_ = mat.node_tree
    bsdf = principled(mat)
    tex = nt_.nodes.new("ShaderNodeTexImage")
    tex.image = albedo
    tex.location = (-360, 300)
    nt_.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.72
    if emission is not None:
        tex_e = nt_.nodes.new("ShaderNodeTexImage")
        tex_e.image = emission
        tex_e.location = (-360, -40)
        nt_.links.new(tex_e.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 2.5
    mat.use_fake_user = True
    return mat


mat_day = make_material("Bull_Day", img_day)
mat_night = make_material("Bull_Night", img_night, img_emis)
for name in OBJECTS:
    m_ = bpy.data.objects[name].data
    m_.materials.clear()
    m_.materials.append(mat_day)

result = {"res": RES, "texels": int(len(idx)), "coverage": round(len(idx) / (RES * RES), 3),
          "raster_seconds": round(t_raster, 1), "seconds": round(time.time() - t0, 1)}
