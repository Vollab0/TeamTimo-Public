# Génère l'écorce stylisée des sapins (M_PineBark_01) : T_PineBark_Albedo.png + T_PineBark_Normal.png (1024 x 1024).
# Remplace l'ancienne texture T_Tree_01_Base / T_Tree_01_Normal du pack « Stylized Low Poly Nature Lite » (Asset Store) :
# plus aucun asset de l'Asset Store dans le projet.
#
# Python + numpy seuls (pas besoin de Blender) : python ArtSource/Trees/generate_bark_texture.py
#
# Mise en page (imposée par les UV du tronc dans build_pine_tree.py) :
#   le tronc fait le tour de la bande u 0,05 -> 0,45 (v 0,02 -> 0,98 du pied à la cime).
#   Le motif est donc périodique en u avec une période de 0,4 : pas de couture à la jonction du tronc.
#   Le reste de l'image prolonge le même motif (inutilisé, mais sans discontinuité).
# Style : plaques d'écorce irrégulières (Voronoï étiré) séparées par des fissures sombres, grain fin,
#   dans les tons de l'ancienne texture (le matériau la multiplie par sa teinte _BaseColor).
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "Murs"))
from texutil import save_png, smoothstep  # noqa: E402

PROJECT = os.environ.get("TEAMTIMO_PROJECT", os.path.abspath(os.path.join(HERE, "..", "..")))
OUT_DIR = os.path.join(PROJECT, "Assets", "Art", "Textures", "Environment", "Trees")
SIZE = 1024
BAND_U0, BAND_W = 0.05, 0.4       # bande d'UV du tronc (build_pine_tree.py)
STRIP_W, STRIP_H = 512, 1024      # motif de base, périodique sur les deux axes
PLATES = 7                        # plaques d'écorce sur le tour du tronc (x 9 germes sur la hauteur)
SEED = 7

# Couleurs (proches de l'ancienne écorce, avant la teinte du matériau)
COL_CRACK = np.array([0.33, 0.18, 0.10])
COL_BASE = np.array([0.58, 0.35, 0.20])
COL_RIDGE = np.array([0.76, 0.50, 0.31])


def periodic_noise(w, h, cx, cy, rng):
    """Bruit de valeur périodique en x et en y (cx x cy cellules), interpolation cosinus. [0,1]"""
    g = rng.random((cy, cx))
    x = np.arange(w) / w * cx
    y = np.arange(h) / h * cy
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int)
    fx = (1 - np.cos((x - x0) * math.pi)) * 0.5
    fy = (1 - np.cos((y - y0) * math.pi)) * 0.5
    x1 = (x0 + 1) % cx; y1 = (y0 + 1) % cy
    x0 %= cx; y0 %= cy
    a = g[np.ix_(y0, x0)]; b = g[np.ix_(y0, x1)]
    c = g[np.ix_(y1, x0)]; d = g[np.ix_(y1, x1)]
    top = a + (b - a) * fx[None, :]
    bot = c + (d - c) * fx[None, :]
    return top + (bot - top) * fy[:, None]


def fractal(w, h, cx, cy, rng, octaves=3, persist=0.5):
    out = np.zeros((h, w)); amp = 1.0; tot = 0.0
    for o in range(octaves):
        out += amp * periodic_noise(w, h, cx * 2 ** o, cy * 2 ** o, rng)
        tot += amp; amp *= persist
    return out / tot


def voronoi_f1_f2(w, h, n_points, stretch, rng, warp_x, warp_y):
    """Distances aux deux germes les plus proches (F1, F2), cellules étirées verticalement, périodique.
    Renvoie aussi l'indice du germe le plus proche (pour varier la teinte par plaque)."""
    px = rng.random(n_points); py = rng.random(n_points)
    x = (np.arange(w)[None, :] + 0.5) / w + warp_x
    y = (np.arange(h)[:, None] + 0.5) / h + warp_y
    f1 = np.full((h, w), 9.0); f2 = np.full((h, w), 9.0); idx = np.zeros((h, w), int)
    for i in range(n_points):
        ddx = np.abs(x - px[i]); ddx = np.minimum(ddx % 1.0, 1.0 - ddx % 1.0)
        ddy = np.abs(y - py[i]); ddy = np.minimum(ddy % 1.0, 1.0 - ddy % 1.0)
        dist = np.sqrt(ddx ** 2 + (ddy * stretch) ** 2)
        closer = dist < f1
        f2 = np.where(closer, f1, np.minimum(f2, dist))
        idx = np.where(closer, i, idx)
        f1 = np.where(closer, dist, f1)
    return f1, f2, idx


def build_strip():
    """Hauteur et couleur du motif de base (STRIP_H x STRIP_W), périodique."""
    rng = np.random.default_rng(SEED)
    w, h = STRIP_W, STRIP_H
    # Plaques d'écorce : cellules de Voronoï étirées le long du tronc, bords ondulés par un bruit.
    warp_x = (fractal(w, h, 4, 8, rng, octaves=3) - 0.5) * 0.10
    warp_y = (fractal(w, h, 4, 8, rng, octaves=3) - 0.5) * 0.06
    f1, f2, cell = voronoi_f1_f2(w, h, PLATES * 9, 0.28, rng, warp_x, warp_y)
    edge = f2 - f1                                     # 0 sur la fissure, grand au centre de la plaque
    crack = 1.0 - smoothstep(0.002, 0.011, edge)
    # Fissures secondaires fines, verticales, dans les grandes plaques.
    fine = fractal(w, h, 22, 3, rng, octaves=2)
    fine_crack = (1.0 - smoothstep(0.0, 0.012, np.abs(fine - 0.5))) * smoothstep(0.03, 0.06, edge) * 0.5
    # Relief : plaques bombées, fissures creuses, grain vertical fin.
    dome = smoothstep(0.0, 0.07, edge)
    grain = fractal(w, h, 64, 8, rng, octaves=2) - 0.5
    blotch = fractal(w, h, 6, 10, rng, octaves=3)
    tone = rng.random(PLATES * 9)[cell]                # chaque plaque a sa nuance
    height = dome * (0.8 + 0.2 * tone) * (1.0 - 0.5 * fine_crack) + 0.08 * grain
    height *= (1.0 - crack)
    # Couleur : fissures sombres, plaques claires au centre, grain et taches.
    light = np.clip(0.50 * dome + 0.20 * tone + 0.20 * blotch + 0.45 * grain, 0, 1)
    col = COL_BASE + (COL_RIDGE - COL_BASE) * smoothstep(0.35, 0.95, light)[..., None]
    col = col + (COL_CRACK - col) * np.maximum(crack, fine_crack * 0.7)[..., None]
    return height, col


def normal_from_height(hm, strength):
    """Normal map en espace tangent (convention OpenGL / Unity : +Y vers le haut), périodique."""
    dx = (np.roll(hm, -1, axis=1) - np.roll(hm, 1, axis=1)) * 0.5
    dy = (np.roll(hm, 1, axis=0) - np.roll(hm, -1, axis=0)) * 0.5   # ligne 0 = haut de l'image
    n = np.stack([-dx * strength, -dy * strength, np.ones_like(hm)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


def sample_on_band(strip):
    """Projette le motif périodique sur l'image finale : période BAND_W en u, à partir de BAND_U0."""
    u = (np.arange(SIZE) + 0.5) / SIZE
    sx = np.floor(((u - BAND_U0) / BAND_W % 1.0) * STRIP_W).astype(int) % STRIP_W
    sy = (np.arange(SIZE) * STRIP_H // SIZE)
    return strip[np.ix_(sy, sx)]


def main():
    height, col = build_strip()
    nrm = normal_from_height(height, strength=10.0)
    albedo = sample_on_band(col)
    normal = sample_on_band(nrm)
    os.makedirs(OUT_DIR, exist_ok=True)
    save_png(os.path.join(OUT_DIR, "T_PineBark_Albedo.png"), albedo)
    save_png(os.path.join(OUT_DIR, "T_PineBark_Normal.png"), normal)
    print("Écrit :", OUT_DIR)


if __name__ == "__main__":
    main()
