# Génère la texture de rameau feuillu des buissons (T_BushLeaves, RGBA 1024 x 1024).
# À lancer dans Blender 5.x (numpy fourni).
#
# Atlas (même disposition que T_PineFrond) :
#   - u 0 -> 0,8 : un rameau vu de dessus, tige au centre (u = 0,4), base en bas (v = 0) ;
#     feuilles ovales pointues, alternées, qui se chevauchent, plus petites vers le bout ;
#   - u 0,8 -> 1 : zone pleine (cœur sombre des buissons).
# RVB = ombrage en niveaux de gris (nervure plus sombre, bord des feuilles plus clair), A = découpe.
import math
import os

import bpy
import numpy as np

PROJECT = os.environ.get("TEAMTIMO_PROJECT", r"C:\Users\gabin\Documents\Projets Unity\TeamTimo")
OUT = os.path.join(PROJECT, "Assets", "Art", "Textures", "Environment", "Trees", "T_BushLeaves.png")
SIZE = 1024
SS = 2
FROND_U = 0.8
ASPECT = 0.5              # largeur de la carte / longueur (doit correspondre à build_bushes.py)
LEAF_PAIRS = 7            # feuilles de chaque côté
SEED = 4


def render(n):
    rnd = np.random.default_rng(SEED)
    w = int(n * FROND_U)
    ys, xs = np.mgrid[0:n, 0:w].astype(np.float64)
    y = (ys + 0.5) / n                         # 0 base -> 1 bout (unités de longueur)
    x = ((xs + 0.5) / w - 0.5) * ASPECT        # écart à la tige
    alpha = np.zeros_like(x)
    shade = np.full_like(x, 0.8)
    leaves = []
    for i in range(LEAF_PAIRS):
        for side in (-1, 1):
            t = 0.1 + i / LEAF_PAIRS * 0.78 + (0.05 if side > 0 else 0.0)
            length = 0.26 * (1.0 - 0.45 * t) * rnd.uniform(0.85, 1.1)
            ang = math.radians(rnd.uniform(38, 55))
            leaves.append((t, side, length, ang, rnd.uniform(-0.07, 0.07)))
    leaves.append((0.86, 0, 0.15, 0.0, 0.0))  # feuille du bout
    for t, side, length, ang, var in leaves:
        # axe de la feuille : part de la tige, penche vers le bout du rameau
        dx, dy = math.sin(ang) * side, math.cos(ang)
        if side == 0:
            dx, dy = 0.0, 1.0
        px, py = x, y - t
        a = px * dx + py * dy                  # le long de la feuille
        b = -px * dy + py * dx                 # en travers
        tt = np.clip(a / length, 0, 1)
        half = 0.5 * length * 0.48 * np.sin(np.pi * tt ** 0.75) ** 0.8   # ovale pointu
        inside = (a > 0) & (a < length) & (np.abs(b) < half)
        edge = np.abs(b) / np.maximum(half, 1e-5)
        s = 0.72 + 0.18 * tt + 0.12 * edge + var             # plus clair au bord et au bout
        s = np.where(np.abs(b) < 0.0022 + 0.0015 * (1 - tt), s - 0.14, s)   # nervure
        shade = np.where(inside, s, shade)                   # dernière feuille par-dessus
        alpha = np.where(inside, 1.0, alpha)
    stem = (np.abs(x) < 0.005) & (y < 0.9)
    alpha = np.where(stem, 1.0, alpha)
    shade = np.where(stem, 0.6, shade)
    frond = np.dstack([shade, shade, shade, alpha])
    solid_w = n - w
    sy, sx = np.mgrid[0:n, 0:solid_w].astype(np.float64)
    h = np.modf(np.sin((np.floor(sy / 6) * 31 + np.floor(sx / 6)) * 12.9898) * 43758.5453)[0] % 1.0
    sshade = 0.62 + 0.12 * h
    solid = np.dstack([sshade, sshade, sshade, np.ones_like(sshade)])
    return np.concatenate([frond, solid], axis=1)


def main():
    img = render(SIZE * SS)
    img = img.reshape(SIZE, SS, SIZE, SS, 4).mean(axis=(1, 3))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    im = bpy.data.images.get("T_BushLeaves") or bpy.data.images.new("T_BushLeaves", SIZE, SIZE, alpha=True)
    im.scale(SIZE, SIZE)
    im.colorspace_settings.name = "sRGB"
    im.pixels.foreach_set(img.astype(np.float32).ravel())
    im.filepath_raw = OUT
    im.file_format = "PNG"
    im.save()
    return {"out": OUT, "coverage": float(img[:, : int(SIZE * FROND_U), 3].mean())}


result = main()
print(result)
