# Génère la texture de branche de sapin (frondes d'aiguilles) utilisée par SM_Tree_Pine_01.
# À lancer dans Blender 5.x (numpy fourni). Sortie : T_PineFrond.png (RGBA, 1024 x 1024).
#
# Atlas :
#   - u 0 -> 0,8 : une branche vue de dessus, tige au centre (u = 0,4), base en bas (v = 0),
#     pointe en haut (v = 1) ; aiguilles qui partent en biais vers la pointe, bord en dents de scie,
#     silhouette en petits paquets ;
#   - u 0,8 -> 1 : zone pleine (alpha = 1) pour le cœur sombre autour du tronc.
# RVB = ombrage en niveaux de gris (multiplié aux couleurs du matériau), A = découpe.
import math
import os

import bpy
import numpy as np

PROJECT = os.environ.get("TEAMTIMO_PROJECT", r"C:\Users\gabin\Documents\Projets Unity\TeamTimo")
OUT = os.path.join(PROJECT, "Assets", "Art", "Textures", "Environment", "Trees", "T_PineFrond.png")
SIZE = 1024
SS = 2                    # sur-échantillonnage (bords d'alpha propres)
FROND_U = 0.8             # part de la largeur occupée par la branche
ASPECT = 0.45             # largeur de la carte / longueur (doit correspondre au modèle)
NEEDLE_SPACING = 0.034    # écart entre aiguilles (en longueur de branche)
NEEDLE_LEAN = 0.85        # inclinaison des aiguilles vers la pointe
CLUMPS = 4                # paquets le long de la branche


def hash1(i):
    return np.modf(np.sin(i * 12.9898 + 78.233) * 43758.5453)[0] % 1.0


def envelope(t):
    """Demi-largeur de la branche (unités de longueur) selon la position le long de la branche."""
    t = np.clip(t, 0.0, 1.0)
    rise = np.clip(t / 0.35, 0, 1) ** 0.55
    fall = np.clip((1.0 - t) / 0.65, 0, 1) ** 0.7
    shape = np.minimum(rise, fall)
    clumps = 0.78 + 0.22 * np.abs(np.cos(math.pi * t * CLUMPS))   # silhouette en paquets
    return 0.5 * ASPECT * 0.96 * shape * clumps


def render(n):
    w = int(n * FROND_U)
    ys, xs = np.mgrid[0:n, 0:w].astype(np.float64)
    v = (ys + 0.5) / n                         # 0 base -> 1 pointe
    dx = ((xs + 0.5) / w - 0.5) * ASPECT       # écart à la tige, en unités de longueur
    d = np.abs(dx)
    side = np.where(dx < 0, 0.0, 0.5)          # côtés décalés
    y0 = v - d * NEEDLE_LEAN                   # point de départ de l'aiguille sur la tige
    f = y0 / NEEDLE_SPACING + side
    idx = np.floor(f)
    phase = f - idx
    rnd = hash1(idx + side * 1000.0)
    length = envelope(y0) * (0.8 + 0.35 * rnd)
    rel = np.clip(d / np.maximum(length, 1e-4), 0, 1)
    half_w = 0.5 * (1.0 - rel) ** 1.1           # aiguilles en triangles pointus
    needle = (d < length) & (np.abs(phase - 0.5) < half_w) & (y0 > 0.0) & (y0 < 1.0)
    core = d < envelope(v) * 0.5
    stem = (d < 0.0035) & (v < 0.95)
    alpha = (needle | core | stem).astype(np.float64)
    shade = np.where(needle, 0.74 + 0.26 * rel + (rnd - 0.5) * 0.12, 0.7)
    shade = np.where(core & ~needle, 0.68 + 0.1 * hash1(np.floor(v * 90) + np.floor(d * 300)), shade)
    shade = np.where(stem, 0.62, shade)
    shade = np.where(alpha > 0, shade, 0.8)
    frond = np.dstack([shade, shade, shade, alpha])
    # zone pleine (cœur)
    solid_w = n - w
    sy, sx = np.mgrid[0:n, 0:solid_w].astype(np.float64)
    sshade = 0.62 + 0.12 * hash1(np.floor(sy / 6) * 31 + np.floor(sx / 6))
    solid = np.dstack([sshade, sshade, sshade, np.ones_like(sshade)])
    return np.concatenate([frond, solid], axis=1)


def main():
    img = render(SIZE * SS)
    img = img.reshape(SIZE, SS, SIZE, SS, 4).mean(axis=(1, 3))     # réduction 2x2
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    im = bpy.data.images.get("T_PineFrond") or bpy.data.images.new("T_PineFrond", SIZE, SIZE, alpha=True)
    im.scale(SIZE, SIZE)
    im.colorspace_settings.name = "sRGB"
    im.pixels.foreach_set(img.astype(np.float32).ravel())
    im.filepath_raw = OUT
    im.file_format = "PNG"
    im.save()
    return {"out": OUT, "coverage": float(img[:, : int(SIZE * FROND_U), 3].mean())}


result = main()
print(result)
