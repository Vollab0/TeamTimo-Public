# Génère l'atlas peint partagé par tous les objets (100 % fait maison).
# Lancer depuis la racine du dépôt : python ArtSource/Objets/gen_items_atlas.py  (Python 3 + numpy + Pillow)
# Sortie : Assets/Art/Textures/Objects/T_Items_Atlas.png (1024²) et T_Items_Emission.png (masque, noir = pas d'émission).
# Disposition (bandes et cases) : items_layout.py.
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import items_layout as L  # noqa: E402

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "Textures", "Objects")
RNG = np.random.default_rng(20261118)
S = L.ATLAS_SIZE


# ------------------------------------------------------------------ outils
def periodic_noise(h, w, sy, sx, rng=RNG):
    """Bruit lissé périodique (flou gaussien par FFT), écarts-types sy, sx en pixels, normalisé -1..1."""
    n = rng.standard_normal((h, w))
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    k = np.exp(-2 * (np.pi ** 2) * ((fy * sy) ** 2 + (fx * sx) ** 2))
    r = np.real(np.fft.ifft2(np.fft.fft2(n) * k))
    r -= r.mean()
    return r / (np.abs(r).max() + 1e-9)


def col(*rgb):
    return np.array(rgb, dtype=np.float64) / 255.0


def mix(a, b, t):
    t = t[..., None] if np.ndim(t) == 2 else t
    return a * (1 - t) + b * t


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def band_t(h):
    """t de chaque ligne de la bande (0 en bas, 1 en haut, comme v)."""
    return (1.0 - (np.arange(h) + 0.5) / h)[:, None]


def with_edge(img, t, edge_col, h):
    """Zone « arête usée » en haut de la bande (t >= EDGE_T0), avec un fondu court."""
    k = smoothstep(L.EDGE_T0 - 0.04, L.EDGE_T0 + 0.02, t) * np.ones((1, img.shape[1]))
    return mix(img, edge_col * (1 + 0.06 * periodic_noise(h, img.shape[1], 2, 30)[..., None]), k)


# ------------------------------------------------------------------ bandes (64 × 1024, fil le long de u)
def paint_wood(h, w, base, dark, light, edge, grain_sx=70, contrast=1.0):
    t = band_t(h)
    g1 = periodic_noise(h, w, 2.5, grain_sx)
    g2 = periodic_noise(h, w, 6, grain_sx * 2.2)
    big = periodic_noise(h, w, 20, 300)
    img = np.ones((h, w, 3)) * base
    img = mix(img, dark, smoothstep(0.15, 0.75, g1 * contrast) * 0.75)
    img = mix(img, light, smoothstep(0.35, 0.9, -g2) * 0.45)
    img *= (1 + 0.08 * big)[..., None]
    return with_edge(img, t, edge, h)


def paint_bark(h, w):
    t = band_t(h)
    ridges = periodic_noise(h, w, 1.8, 45)
    patches = periodic_noise(h, w, 10, 90)
    img = np.ones((h, w, 3)) * col(128, 78, 40)
    img = mix(img, col(78, 45, 24), smoothstep(0.0, 0.6, ridges) * 0.85)
    img = mix(img, col(165, 108, 58), smoothstep(0.3, 0.9, -ridges) * 0.5)
    img = mix(img, col(105, 92, 60), smoothstep(0.5, 0.9, patches) * 0.35)   # lichen discret
    return with_edge(img, t, col(170, 120, 70), h)


def paint_metal(h, w, base, dark, edge, spots=0.0, rust=None):
    t = band_t(h)
    mott = periodic_noise(h, w, 8, 40)
    fine = periodic_noise(h, w, 1.5, 6)
    img = np.ones((h, w, 3)) * base
    img = mix(img, dark, smoothstep(0.1, 0.8, mott) * 0.6)
    img *= (1 + 0.05 * fine)[..., None]
    img *= (0.92 + 0.12 * t)[..., None]            # léger dégradé peint
    if spots > 0:                                  # petites bosses / chocs plus sombres
        d = periodic_noise(h, w, 1.8, 3.5)
        img = mix(img, dark * 0.8, smoothstep(0.55, 0.8, d) * spots)
    if rust is not None:
        r = periodic_noise(h, w, 5, 25)
        img = mix(img, rust, smoothstep(0.45, 0.85, r) * 0.55)
    return with_edge(img, t, edge, h)


def paint_leather(h, w):
    t = band_t(h)
    mott = periodic_noise(h, w, 6, 20)
    pores = periodic_noise(h, w, 1.2, 1.2)
    img = np.ones((h, w, 3)) * col(128, 76, 42)
    img = mix(img, col(88, 50, 28), smoothstep(0.0, 0.8, mott) * 0.6)
    img = mix(img, col(160, 104, 62), smoothstep(0.2, 0.9, -mott) * 0.4)
    img *= (1 + 0.05 * pores)[..., None]
    return with_edge(img, t, col(170, 115, 70), h)


def paint_string(h, w):
    """Ficelle rouge enroulée en croisillons : deux familles de spires obliques."""
    t = band_t(h) * np.ones((1, w))
    u = np.linspace(0, 1, w, endpoint=False)[None, :] * np.ones((h, 1))
    k, m = 64, 1.0
    s1 = np.abs(np.sin(np.pi * (u * k + t * m * 3)))
    s2 = np.abs(np.sin(np.pi * (u * k - t * m * 3)))
    strand = np.maximum(smoothstep(0.25, 0.75, s1), smoothstep(0.25, 0.75, s2) * 0.85)
    img = mix(np.ones((h, w, 3)) * col(70, 22, 18), col(185, 48, 40), strand)
    img = mix(img, col(225, 95, 80), smoothstep(0.85, 1.0, s1) * 0.4)
    return with_edge(img, band_t(h), col(200, 70, 60), h)


# ------------------------------------------------------------------ cases (128 × 128)
def grid(n=L.CELL_PX):
    y, x = np.mgrid[0:n, 0:n]
    return (x + 0.5) / n, 1.0 - (y + 0.5) / n      # s à droite, t vers le haut


def flat(c, var=0.06, sy=10, sx=10, dark=None):
    n = L.CELL_PX
    nz = periodic_noise(n, n, sy, sx)
    img = np.ones((n, n, 3)) * c * (1 + var * nz)[..., None]
    if dark is not None:
        img = mix(img, dark, smoothstep(0.3, 0.9, periodic_noise(n, n, 4, 4)) * 0.3)
    return img


def paint_gold():
    s, t = grid()
    img = flat(col(234, 156, 18), 0.07)
    img = mix(img, col(255, 210, 80), smoothstep(0.55, 0.95, t - 0.3 * s) * 0.5)
    return img


def paint_endgrain(bark):
    s, t = grid()
    r = np.hypot(s - 0.5, t - 0.5) * 2
    rings = 0.5 + 0.5 * np.sin(r * 34 + periodic_noise(128, 128, 8, 8) * 2.0)
    img = mix(np.ones((128, 128, 3)) * col(232, 190, 120), col(196, 140, 72), smoothstep(0.3, 0.9, rings) * 0.7)
    img = mix(img, col(176, 118, 58), smoothstep(0.0, 0.12, 0.12 - r) * 0.6)     # cœur
    if bark:
        img = mix(img, col(92, 55, 30), smoothstep(0.78, 0.86, r))
    return img


def paint_coin_paw():
    """Face de pièce : disque doré, liseré, empreinte de patte en creux (ombre en haut à gauche, reflet en bas à droite)."""
    s, t = grid()
    img = paint_gold()
    r = np.hypot(s - 0.5, t - 0.5)
    ring = smoothstep(0.035, 0.0, np.abs(r - 0.40))
    img = mix(img, col(176, 100, 10), ring * 0.8)

    def paw(ox, oy):
        sx, ty = s - ox, t - oy
        m = smoothstep(1.0, 0.85, ((sx - 0.5) / 0.13) ** 2 + ((ty - 0.40) / 0.105) ** 2)   # coussinet
        for cx, cy, rx, ry in ((0.33, 0.58, 0.055, 0.07), (0.44, 0.66, 0.055, 0.075),
                               (0.56, 0.66, 0.055, 0.075), (0.67, 0.58, 0.055, 0.07)):
            m = np.maximum(m, smoothstep(1.0, 0.8, ((sx - cx) / rx) ** 2 + ((ty - cy) / ry) ** 2))
        return m
    m = paw(0, 0)
    shade = paw(-0.012, 0.012)
    img = mix(img, col(160, 88, 8), m * 0.85)
    img = mix(img, col(255, 214, 100), np.clip(m - shade, 0, 1) * 0.9)
    return img


def paint_lock_face():
    s, t = grid()
    img = flat(col(78, 74, 76), 0.08)
    r = np.hypot(s - 0.5, t - 0.5)
    img = mix(img, col(150, 145, 140), smoothstep(0.37, 0.44, r))                    # bord usé
    hole = np.maximum(smoothstep(0.075, 0.06, np.hypot(s - 0.5, t - 0.55)),
                      ((np.abs(s - 0.5) < 0.03 + (0.55 - t) * 0.15) & (t < 0.55) & (t > 0.33)).astype(float))
    return mix(img, col(18, 14, 12), hole)


def paint_rope():
    s, t = grid()
    tw = np.abs(np.sin(np.pi * (s * 6 + t * 6)))
    img = mix(np.ones((128, 128, 3)) * col(150, 118, 76), col(212, 182, 132), smoothstep(0.2, 0.8, tw))
    return img


def paint_cloth_red():
    img = flat(col(196, 44, 40), 0.05)
    s, t = grid()
    folds = np.sin(s * 9 + periodic_noise(128, 128, 12, 12) * 1.5)
    return img * (0.9 + 0.12 * folds)[..., None]


def main():
    atlas = np.zeros((S, S, 3))
    w = S
    h = L.BAND_PX
    bands = {
        "wood_honey": paint_wood(h, w, col(214, 146, 62), col(170, 102, 38), col(238, 182, 98), col(240, 196, 120)),
        "bark": paint_bark(h, w),
        "wood_chest": paint_wood(h, w, col(204, 128, 48), col(150, 84, 30), col(232, 164, 82), col(120, 64, 26),
                                 grain_sx=90, contrast=1.3),
        "iron_blue": paint_metal(h, w, col(88, 104, 130), col(62, 74, 96), col(172, 186, 206), spots=0.5),
        "iron_dark": paint_metal(h, w, col(74, 72, 74), col(48, 46, 48), col(140, 136, 130), spots=0.3, rust=col(110, 74, 50)),
        "steel_light": paint_metal(h, w, col(172, 182, 198), col(128, 140, 158), col(232, 238, 246)),
        "leather": paint_leather(h, w),
        "string_red": paint_string(h, w),
    }
    for name, img in bands.items():
        y0, y1 = L.band_px(name)
        atlas[y0:y1] = img

    cells = {
        "gold": paint_gold(), "brass": flat(col(226, 160, 28), 0.08, dark=col(170, 108, 18)),
        "cloth_red": paint_cloth_red(), "rope": paint_rope(), "black": flat(col(24, 19, 17), 0.1),
        "endgrain": paint_endgrain(False), "endgrain_bark": paint_endgrain(True), "coin_paw": paint_coin_paw(),
        "tomato": flat(col(214, 46, 36)), "leaf": flat(col(92, 160, 62)), "canvas": flat(col(214, 198, 164)),
        "cardboard": flat(col(186, 140, 86)), "glass": flat(col(150, 200, 222), 0.03), "chalk": flat(col(242, 240, 232), 0.03),
        "mustard": flat(col(222, 172, 42)), "lock_face": paint_lock_face(),
    }
    n = L.CELL_PX
    for name, img in cells.items():
        y0, x0 = L.cell_px(name)
        atlas[y0:y0 + n, x0:x0 + n] = img
    # cases libres : gris neutre
    for r, row in enumerate(L.CELLS):
        for c, name in enumerate(row):
            if name is None:
                y0 = S // 2 + r * n
                atlas[y0:y0 + n, c * n:(c + 1) * n] = col(128, 128, 128)

    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(np.clip(atlas * 255 + 0.5, 0, 255).astype(np.uint8)).save(os.path.join(OUT, "T_Items_Atlas.png"))
    Image.fromarray(np.zeros((S, S, 3), np.uint8)).save(os.path.join(OUT, "T_Items_Emission.png"))
    print("écrit T_Items_Atlas.png et T_Items_Emission.png dans", OUT)


if __name__ == "__main__":
    main()
