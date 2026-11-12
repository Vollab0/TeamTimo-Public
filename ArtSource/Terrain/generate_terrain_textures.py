# Génère les textures stylisées du terrain (herbe + terre), raccordables, façon « RV There Yet ».
# À lancer dans Blender (onglet Scripting > Run Script, ou via le MCP) : numpy est fourni avec Blender.
# Sortie : Assets/Art/Textures/Environment/Terrain/*.png
import os
import numpy as np
import bpy

PROJECT = os.environ.get("TEAMTIMO_PROJECT", r"C:\Users\gabin\Documents\Projets Unity\TeamTimo")
OUT_DIR = os.path.join(PROJECT, "Assets", "Art", "Textures", "Environment", "Terrain")
SIZE = 1024
rng = np.random.default_rng(1234)


def value_noise(size, cells, seed):
    """Bruit de valeur raccordable (le motif boucle sur `size`)."""
    r = np.random.default_rng(seed).random((cells, cells))
    coords = np.arange(size) * cells / size
    i0 = np.floor(coords).astype(int)
    i1 = (i0 + 1) % cells
    t = coords - i0
    t = t * t * t * (t * (t * 6 - 15) + 10)  # quintic
    # interpolation en x puis en y
    a = r[np.ix_(i0, i0)]
    b = r[np.ix_(i0, i1)]
    c = r[np.ix_(i1, i0)]
    d = r[np.ix_(i1, i1)]
    tx = t[None, :]
    ty = t[:, None]
    top = a + (b - a) * tx
    bot = c + (d - c) * tx
    return top + (bot - top) * ty


def fbm(size, base_cells, octaves, seed, gain=0.5):
    total = np.zeros((size, size))
    amp, norm, cells = 1.0, 0.0, base_cells
    for o in range(octaves):
        total += value_noise(size, cells, seed + o * 17) * amp
        norm += amp
        amp *= gain
        cells *= 2
    return total / norm


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def hexrgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


def lerp(a, b, t):
    return a + (b - a) * t[..., None]


def save_png(name, rgb, non_color=False):
    os.makedirs(OUT_DIR, exist_ok=True)
    h, w, _ = rgb.shape
    img = bpy.data.images.new(name, width=w, height=h, alpha=False)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    rgba = np.ones((h, w, 4), dtype=np.float32)
    rgba[..., :3] = np.clip(rgb, 0, 1)
    img.pixels.foreach_set(rgba[::-1].ravel())  # Blender : origine en bas à gauche
    path = os.path.join(OUT_DIR, name + ".png")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    return path


def normal_from_height(height, strength):
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5
    n = np.dstack([-dx * strength, dy * strength, np.ones_like(height)])  # convention OpenGL (Y+)
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n * 0.5 + 0.5


def stamp_pebbles(size, count, rmin, rmax, seed):
    """Petits cailloux arrondis (hauteur 0..1), raccordables."""
    r = np.random.default_rng(seed)
    height = np.zeros((size, size))
    yy, xx = np.mgrid[0:size, 0:size]
    for _ in range(count):
        cx, cy = r.random() * size, r.random() * size
        rad = r.uniform(rmin, rmax)
        sx, sy = r.uniform(0.7, 1.3), r.uniform(0.7, 1.3)
        dx = (xx - cx + size / 2) % size - size / 2
        dy = (yy - cy + size / 2) % size - size / 2
        d = np.sqrt((dx / sx) ** 2 + (dy / sy) ** 2) / rad
        dome = np.sqrt(np.clip(1 - d * d, 0, 1))
        height = np.maximum(height, dome * r.uniform(0.6, 1.0))
    return height


# ---------------------------------------------------------------- HERBE (sol)
def make_grass():
    # Contraste volontairement faible sur les grandes taches : la texture est répétée tous les 25 m
    # et un motif trop marqué dessine des bandes au loin.
    dark = hexrgb("#6A9836")
    mid = hexrgb("#76A33C")
    light = hexrgb("#86AE44")
    yellow = hexrgb("#9AB04C")

    large = fbm(SIZE, 4, 4, 10)
    patches = fbm(SIZE, 12, 3, 40)
    fine = fbm(SIZE, 128, 3, 70)
    straw = value_noise(SIZE, 256, 90)

    col = lerp(np.broadcast_to(dark, (SIZE, SIZE, 3)), np.broadcast_to(mid, (SIZE, SIZE, 3)), smoothstep(0.25, 0.65, large))
    col = lerp(col, np.broadcast_to(light, (SIZE, SIZE, 3)), smoothstep(0.55, 0.8, patches) * 0.6)
    col = lerp(col, np.broadcast_to(yellow, (SIZE, SIZE, 3)), smoothstep(0.62, 0.85, fbm(SIZE, 10, 3, 55)) * 0.3)
    col *= (0.95 + 0.1 * fine)[..., None]                     # grain très doux
    col *= (1.0 - 0.07 * smoothstep(0.75, 0.95, straw))[..., None]  # petites touches sombres
    return col


# ---------------------------------------------------------------- TERRE
def make_dirt():
    dark = hexrgb("#C29B76")
    mid = hexrgb("#D1AD87")
    light = hexrgb("#DEC3A0")

    large = fbm(SIZE, 4, 4, 110)
    mottling = fbm(SIZE, 16, 3, 140)
    fine = fbm(SIZE, 96, 2, 170)

    col = lerp(np.broadcast_to(dark, (SIZE, SIZE, 3)), np.broadcast_to(mid, (SIZE, SIZE, 3)), smoothstep(0.2, 0.6, large))
    col = lerp(col, np.broadcast_to(light, (SIZE, SIZE, 3)), smoothstep(0.5, 0.8, mottling) * 0.6)
    col *= (0.96 + 0.08 * fine)[..., None]

    pebbles = stamp_pebbles(SIZE, 70, 3, 9, 200)
    small = stamp_pebbles(SIZE, 160, 1.2, 3, 210)
    peb = np.maximum(pebbles, small * 0.8)
    pebble_col = hexrgb("#E6C9A2")
    shade = np.roll(peb, 2, axis=0)  # ombre décalée vers le bas
    col *= (1.0 - 0.18 * np.clip(shade - peb, 0, 1))[..., None]
    col = lerp(col, np.broadcast_to(pebble_col, (SIZE, SIZE, 3)), np.clip(peb * 1.6, 0, 1) * 0.85)
    col *= (0.9 + 0.1 * peb)[..., None]

    height = peb * 1.0 + mottling * 0.25 + fine * 0.05
    normal = normal_from_height(height, 6.0)
    return col, normal


grass = make_grass()
dirt, dirt_n = make_dirt()
paths = [
    save_png("T_Ground_Grass", grass),
    save_png("T_Ground_Dirt", dirt),
    save_png("T_Ground_Dirt_N", dirt_n, non_color=True),
]
# couleur moyenne (utile pour régler le shader d'herbe)
result = {"paths": paths, "grass_mean": (grass.reshape(-1, 3).mean(0) * 255).round().tolist(),
          "dirt_mean": (dirt.reshape(-1, 3).mean(0) * 255).round().tolist()}
print(result)
