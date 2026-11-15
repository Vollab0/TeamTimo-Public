# Génère les textures tileables du mur de pierre stylisé (TeamTimo).
# Lancer avec le Python de Blender (numpy) :
#   "C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe" gen_stone_textures.py [outdir] [size]
# Sorties : T_WallStone_Albedo.png, T_WallStone_Normal.png (OpenGL), T_WallStone_Height.png, T_WallStone_AO.png
import sys, os, zlib, struct, math
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Art", "Textures", "Environment", "Walls"))
N = int(sys.argv[2]) if len(sys.argv) > 2 else 2048
S = N / 2048.0  # facteur d'échelle des tailles en pixels
rng = np.random.default_rng(7)

from texutil import save_png, tile_noise, box_blur, blur, smoothstep

# ---------------------------------------------------------------- disposition des pierres
GAP = 4.5 * S  # demi-joint
WOB = tile_noise(N, 28, 5, 2, 0.4)  # ondulation des contours
rows = []
y = 0.0
while y < N - 1:
    h = rng.uniform(125, 205) * S
    if N - y - h < 110 * S: h = N - y
    rows.append((y, h)); y += h
stones = []  # (x0, y0, w, h)
for (y0, h) in rows:
    x = rng.uniform(0, 300 * S)
    start = x
    while x < start + N - 1:
        w = rng.uniform(210, 470) * S
        if start + N - (x + w) < 150 * S: w = start + N - x
        # parfois deux pierres empilées / côte à côte plus petites
        r = rng.random()
        if r < 0.16 and h > 150 * S:
            hh = h * rng.uniform(0.4, 0.6)
            stones.append((x, y0, w, hh)); stones.append((x, y0 + hh, w, h - hh))
        elif r < 0.26 and w > 300 * S:
            ww = w * rng.uniform(0.35, 0.65)
            stones.append((x, y0, ww, h)); stones.append((x + ww, y0, w - ww, h))
        else:
            stones.append((x, y0, w, h))
        x += w

H = np.zeros((N, N), np.float32)           # hauteur
ID = np.full((N, N), -1, np.int32)         # index de pierre
PROF = np.zeros((N, N), np.float32)        # profil de biseau (0 bord -> 1 dessus)
CRACK = np.zeros((N, N), np.float32)
palette = np.array([
    (0.56, 0.56, 0.54), (0.50, 0.51, 0.52), (0.58, 0.56, 0.52),
    (0.50, 0.52, 0.49), (0.54, 0.54, 0.55), (0.62, 0.61, 0.58), (0.46, 0.47, 0.47),
], np.float32)
stone_col = []
micro = tile_noise(N, 96, 11, 3, 0.55)
lowf = tile_noise(N, 6, 12, 3, 0.5)

for si, (x0, y0, w, h) in enumerate(stones):
    # polygone convexe : rectangle rentré du joint, coins chanfreinés + jitter
    cx, cy = x0 + w / 2, y0 + h / 2
    hw, hh = w / 2 - GAP, h / 2 - GAP
    j = lambda: rng.uniform(-7, 7) * S
    corners = [(-hw, -hh), (hw, -hh), (hw, hh), (-hw, hh)]
    corners = [(px + j(), py + j()) for px, py in corners]
    poly = []
    for k in range(4):
        c = np.array(corners[k]); pv = np.array(corners[k - 1]); nx_ = np.array(corners[(k + 1) % 4])
        ch = rng.uniform(6, 46) * S if rng.random() < 0.85 else 0.0
        if ch > 0:
            dp = (pv - c) / (np.linalg.norm(pv - c) + 1e-6); dn = (nx_ - c) / (np.linalg.norm(nx_ - c) + 1e-6)
            poly.append(tuple(c + dp * ch * rng.uniform(0.6, 1.4))); poly.append(tuple(c + dn * ch * rng.uniform(0.6, 1.4)))
        else:
            poly.append(tuple(c))
    poly = np.array(poly, np.float32)
    # garantir l'ordre CCW (aire positive) pour les normales d'arête
    area = 0.5 * np.sum(poly[:, 0] * np.roll(poly[:, 1], -1) - np.roll(poly[:, 0], -1) * poly[:, 1])
    if area < 0: poly = poly[::-1]
    pad = 2
    bx0 = int(math.floor(cx - w / 2)) - pad; by0 = int(math.floor(cy - h / 2)) - pad
    bw = int(math.ceil(w)) + 2 * pad; bh = int(math.ceil(h)) + 2 * pad
    xs = np.arange(bx0, bx0 + bw, dtype=np.float32) + 0.5 - cx
    ys = np.arange(by0, by0 + bh, dtype=np.float32) + 0.5 - cy
    X, Y = np.meshgrid(xs, ys)
    # profondeur intérieure = min des distances signées aux arêtes
    depth = np.full(X.shape, 1e9, np.float32)
    for k in range(len(poly)):
        ax, ay = poly[k]; bx, by = poly[(k + 1) % len(poly)]
        ex, ey = bx - ax, by - ay; L = math.hypot(ex, ey) + 1e-6
        nx, ny = -ey / L, ex / L  # normale intérieure (CCW)
        depth = np.minimum(depth, (X - ax) * nx + (Y - ay) * ny)
    iy = (np.arange(by0, by0 + bh) % N); ix = (np.arange(bx0, bx0 + bw) % N)
    sub = np.ix_(iy, ix)
    # contours irréguliers (taillés à la main) + éclats sur les bords
    depth = depth + (WOB[sub] - 0.5) * 26 * S
    for _ in range(rng.integers(1, 5)):
        ang = rng.uniform(0, 2 * math.pi)
        dx_, dy_ = math.cos(ang), math.sin(ang)
        reach_ = abs(dx_) * hw + abs(dy_) * hh
        depth = np.minimum(depth, reach_ - rng.uniform(8, 30) * S - (X * dx_ + Y * dy_))
    inside = depth > 0
    if not inside.any(): continue
    bwid = rng.uniform(22, 42) * S
    t = np.clip(depth / bwid, 0, 1)
    prof = 1 - (1 - t) ** 2.6
    top = rng.uniform(0.62, 0.86)
    tilt_x = rng.uniform(-0.05, 0.05) / max(w, 1) * 2; tilt_y = rng.uniform(-0.05, 0.05) / max(h, 1) * 2
    hs = top + X * tilt_x + Y * tilt_y
    sh = 0.22 + (hs - 0.22) * prof
    # facettes taillées (plans qui coupent la pierre)
    for _ in range(rng.integers(3, 7)):
        ang = rng.uniform(0, 2 * math.pi)
        dx, dy = math.cos(ang), math.sin(ang)
        # point d'appui près d'un bord dans la direction ang
        reach = abs(dx) * hw + abs(dy) * hh
        d0 = reach * rng.uniform(0.3, 0.75)
        slope = rng.uniform(0.003, 0.0075) / S
        plane = top + 0.02 - (X * dx + Y * dy - d0) * slope
        sh = np.minimum(sh, np.maximum(plane, 0.22 + 0.12 * prof))
    # fissure
    cr = np.zeros_like(X)
    if rng.random() < 0.3:
        p = np.array([rng.uniform(-hw, hw) * 0.6, -hh * 1.1]); q = np.array([rng.uniform(-hw, hw) * 0.6, hh * 1.1])
        if rng.random() < 0.5: p, q = np.array([-hw * 1.1, rng.uniform(-hh, hh) * 0.5]), np.array([hw * 1.1, rng.uniform(-hh, hh) * 0.5])
        m = (p + q) / 2 + rng.uniform(-1, 1, 2) * np.array([hw, hh]) * 0.3
        for a_, b_ in ((p, m), (m, q)):
            v = b_ - a_; L2 = float(v @ v) + 1e-6
            tt = np.clip(((X - a_[0]) * v[0] + (Y - a_[1]) * v[1]) / L2, 0, 1)
            dd = np.hypot(X - (a_[0] + tt * v[0]), Y - (a_[1] + tt * v[1]))
            cr = np.maximum(cr, np.exp(-(dd / (2.6 * S)) ** 2))
        sh = sh - 0.09 * cr * (prof > 0.3)
    # écrire avec enroulement (tileable)
    curH = H[sub]; curI = ID[sub]; curP = PROF[sub]; curC = CRACK[sub]
    curH[inside] = sh[inside]; curI[inside] = si; curP[inside] = prof[inside]; curC[inside] = cr[inside]
    H[sub] = curH; ID[sub] = curI; PROF[sub] = curP; CRACK[sub] = curC
    base = palette[rng.integers(len(palette))] * rng.uniform(0.9, 1.08)
    stone_col.append(base)
    while len(stone_col) <= si: stone_col.append(base)

stone_col = np.array(stone_col + [palette[0]], np.float32)
mortar = ID < 0
# joints : creux bruités
H[mortar] = (0.08 + 0.1 * tile_noise(N, 64, 21, 2))[mortar]
# micro-détail (grain sableux) + ondulation douce
H += (micro - 0.5) * 0.025 + (lowf - 0.5) * 0.04 * (~mortar)

# ---------------------------------------------------------------- mousse dans les joints
cav = np.clip(blur(H, int(10 * S)) - H, 0, None)
cav_n = np.clip(cav / (np.percentile(cav, 99) + 1e-6), 0, 1)
mn = tile_noise(N, 7, 31, 4, 0.55)
mblob = tile_noise(N, 40, 32, 3, 0.6)
near_gap = 1 - smoothstep(0.0, 0.55, PROF)  # joint + bords de pierre
patchm = smoothstep(0.63, 0.73, mn + (mblob - 0.5) * 0.22)
moss = patchm * np.clip(near_gap * 1.1 + 0.8 * smoothstep(0.70, 0.80, mn + (mblob - 0.5) * 0.35), 0, 1)
moss = np.clip(moss, 0, 1)
H = H + moss * (0.1 + 0.12 * mblob) * (1 - PROF * 0.6)

# ---------------------------------------------------------------- normales (OpenGL, +Y = haut de l'image)
STR = 9.0 / S
Hp = blur(H, 1, 1)
dx = (np.roll(Hp, -1, 1) - np.roll(Hp, 1, 1)) * 0.5 * STR
drow = (np.roll(Hp, -1, 0) - np.roll(Hp, 1, 0)) * 0.5 * STR
nx = -dx; ny = drow; nz = np.ones_like(nx)
ln = np.sqrt(nx * nx + ny * ny + nz * nz)
nx /= ln; ny /= ln; nz /= ln
normal = np.stack([nx * 0.5 + 0.5, ny * 0.5 + 0.5, nz * 0.5 + 0.5], -1)

# ---------------------------------------------------------------- occlusion
ao = 1 - np.clip((blur(H, int(14 * S)) - H) * 3.2, 0, 1) * 0.85
ao = np.clip(ao * (0.85 + 0.15 * np.clip(H / 0.8, 0, 1)), 0, 1)

# ---------------------------------------------------------------- albédo peint
col = stone_col[ID]
mott = tile_noise(N, 18, 41, 3, 0.55)
patch = smoothstep(0.45, 0.65, tile_noise(N, 10, 42, 2, 0.5))
col = col * 0.88 * (0.9 + 0.2 * mott[..., None]) * (1 - 0.07 * patch[..., None])
# éclairage peint : bords hauts clairs, bas sombres, dessus plus clair
light = np.clip(ny * 2.2, -1, 1)   # face tournée vers le haut de l'image
col = col * (1 + 0.32 * np.clip(light, 0, 1)[..., None] - 0.3 * np.clip(-light, 0, 1)[..., None])
col = col * (0.82 + 0.22 * PROF[..., None])
edge = smoothstep(0.15, 0.5, PROF) * (1 - smoothstep(0.5, 0.95, PROF))
col = col + 0.06 * edge[..., None] * np.clip(light, 0, 1)[..., None]
# speckle sableux
col = col * (0.95 + 0.1 * micro[..., None])
# fissures
col = col * (1 - 0.55 * CRACK[..., None])
# joints sombres
mort_col = np.array([0.13, 0.12, 0.11], np.float32) * (0.8 + 0.4 * tile_noise(N, 50, 51, 2))[..., None]
col = np.where(mortar[..., None], mort_col, col)
col = col * (0.55 + 0.45 * ao[..., None])
# mousse
m1 = np.array([0.15, 0.25, 0.08], np.float32); m2 = np.array([0.36, 0.48, 0.15], np.float32)
mt = np.clip(mblob * 0.8 + np.clip(light, 0, 1) * 0.5, 0, 1)[..., None]
mcol = m1 * (1 - mt) + m2 * mt
mcol = mcol * (0.75 + 0.25 * ao[..., None])
col = col * (1 - moss[..., None]) + mcol * moss[..., None]

os.makedirs(OUT, exist_ok=True)
save_png(os.path.join(OUT, "T_WallStone_Albedo.png"), col)
save_png(os.path.join(OUT, "T_WallStone_Normal.png"), normal)
hn = (H - H.min()) / (H.max() - H.min())
save_png(os.path.join(OUT, "T_WallStone_Height.png"), hn)
save_png(os.path.join(OUT, "T_WallStone_AO.png"), ao)
import json
with open(os.path.join(HERE, "stone_layout.json"), "w") as f:  # lu par build_walls.py
    # rectangles en fraction de tuile, origine en HAUT à gauche de l'image (comme les pixels)
    json.dump({"gap": GAP / N, "stones": [[x / N, y / N, w / N, h / N] for (x, y, w, h) in stones]}, f)
print("ok", len(stones), "pierres")
