# Génère les textures peintes partagées par les bâtiments du village du Lobby (100 % fait maison).
# Lancer depuis la racine du dépôt : python ArtSource/Lobby/gen_village_textures.py  (Python 3 + numpy + Pillow)
# Sortie : Assets/Art/Textures/Environment/Village/
#
# T_Village_Wood_Albedo/_Normal (1024²) : « trim sheet » lu par build_houses.py
#   - rangée du haut (lignes 0-255 de l'image = v 0,75-1) : 4 carrés de 256 px
#       u 0-0,25 verre clair · 0,25-0,5 fenêtre à croisillons en losange · 0,5-0,75 laiton · 0,75-1 noir (intérieur)
#   - 3 bandes de bois de 256 px (v 0,5-0,75 / 0,25-0,5 / 0-0,25) : fil du bois le long de u, raccord en u
#     (une répétition = WOOD_U_METERS m de poutre dans build_houses.py).
# T_Village_Plaster_Albedo/_Normal (1024², raccord dans les 2 sens) : enduit crème.
# T_Village_WoodWhite_Albedo : même disposition, bois peint en blanc (encadrements de la grange).
# T_Village_RoofTile_<Couleur>_Albedo (1024², 4 × 2 tuiles de 256 × 512 px) + T_Village_RoofTile_Normal.
# T_Village_Planks / RedPlanks _Albedo + T_Village_Planks_Normal : bardage vertical (8 m par répétition).
# T_Village_Canvas_Albedo/_Normal : toile de tente (8 m) ; T_Village_PaintRed_Albedo : croix rouge.
import os

import numpy as np
from PIL import Image

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "Textures", "Environment", "Village")
RNG = np.random.default_rng(20261116)


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


def normal_from_height(hgt, strength, wrap=True):
    """Normal map (convention OpenGL / Unity : vert = haut) depuis une hauteur 0..1."""
    if wrap:
        dx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * 0.5
        dy = (np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * 0.5
    else:
        dy, dx = np.gradient(hgt)
    nx, ny, nz = -dx * strength, dy * strength, np.ones_like(hgt)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    n = np.stack([nx / ln, ny / ln, nz / ln], -1)
    return (n * 0.5 + 0.5)


def save(arr, name):
    os.makedirs(OUT, exist_ok=True)
    img = Image.fromarray(np.clip(arr * 255 + 0.5, 0, 255).astype(np.uint8))
    img.save(os.path.join(OUT, name))
    print("écrit", name, img.size)


def mix(a, b, t):
    t = t[..., None] if np.ndim(t) == 2 else t
    return a * (1 - t) + b * t


def col(*rgb):
    return np.array(rgb, dtype=np.float64) / 255.0


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


# ------------------------------------------------------------------ bois (bande de fil, raccord en u)
def wood_band(h, w, seed):
    rng = np.random.default_rng(seed)
    x = np.linspace(0, 1, w, endpoint=False)[None, :] * np.ones((h, 1))
    y = np.linspace(0, 1, h)[:, None] * np.ones((1, w))

    # Bruits périodiques en u pour déformer organiquement le fil du bois.
    warp_big = periodic_noise(h, w, 55, 115, rng)
    warp_mid = periodic_noise(h, w, 22, 46, rng)
    cathedral = periodic_noise(h, w, 85, 80, rng)
    fibers = periodic_noise(h, w, 1.4, 55, rng)
    streak = periodic_noise(h, w, 12, 150, rng)
    blot = periodic_noise(h, w, 45, 95, rng)

    # Nœuds du bois (1 par bande 0 et 1, subtil sur bande 2) : écartent les fibres et forment un ovale naturel.
    dy_knot = np.zeros((h, w))
    knot_rings = np.zeros((h, w))
    knot_core = np.zeros((h, w))
    knot_mask = np.ones((h, w))
    n_knots = 1 if (seed % 3) != 2 else 0
    for _ in range(n_knots):
        xk = rng.uniform(0.22, 0.78)
        yk = rng.uniform(0.32, 0.68)
        rx = rng.uniform(0.055, 0.075)
        ry = rng.uniform(0.075, 0.105)
        dx = ((x - xk + 0.5) % 1.0) - 0.5
        dy = y - yk
        re = np.sqrt(np.maximum(0.0, (dx / rx) ** 2 + (dy / ry) ** 2)) + 0.05 * warp_mid
        dy_knot += (dy / (re ** 2 + 0.25)) * np.exp(-0.60 * re ** 2) * 0.13
        inner = smoothstep(0.98, 0.62, re)
        knot_mask = np.minimum(knot_mask, smoothstep(0.55, 1.02, re))
        ring = smoothstep(0.75, 0.30, np.abs(np.sin(re * np.pi * 3.5 + 0.3 * warp_mid)))
        knot_rings = np.maximum(knot_rings, ring * inner * 0.85)
        core = smoothstep(0.32, 0.08, re)
        cross = smoothstep(0.08, 0.0, np.abs(dy / ry)) * smoothstep(0.22, 0.04, np.abs(dx / rx))
        knot_core = np.maximum(knot_core, np.maximum(core * 0.7, cross * 0.9))

    # Arches/ogives douces intégrées au champ de déformation (courbes naturelles sans taches).
    arch_field = np.zeros((h, w))
    for sign in (-1.0, 1.0):
        xc = rng.uniform(0.0, 1.0)
        yc = rng.uniform(0.25, 0.75)
        dx = ((x - xc + 0.5) % 1.0) - 0.5
        arch_field += sign * 0.13 * np.exp(-((dx / 0.18) ** 2 + ((y - yc) / 0.45) ** 2))

    y_flow = y + 0.12 * warp_big + 0.05 * warp_mid + 0.09 * cathedral + arch_field - dy_knot

    # Veines nettes à épaisseur régulière en pixels (normalisée par le gradient) et pointes effilées.
    freq = rng.uniform(4.6, 5.6)
    phase = y_flow * np.pi * 2.0 * freq + 0.60 * periodic_noise(h, w, 26, 85, rng)
    gy, gx = np.gradient(phase)
    gmag = np.maximum(0.08, np.hypot(gx, gy))
    seg_mask = smoothstep(-0.35, 0.35, periodic_noise(h, w, 14, 65, rng))
    seg_mask2 = smoothstep(-0.15, 0.45, periodic_noise(h, w, 18, 55, rng))
    # Distance en pixels aux crêtes des veines primaires et secondaires.
    d1 = np.abs(np.cos(phase * 0.5)) * 2.0 / gmag
    d2 = np.abs(np.cos((phase * 1.9 + 1.1) * 0.5)) * 2.0 / (gmag * 1.9)
    w_px1 = 1.6 + 1.4 * seg_mask
    w_px2 = 1.1 + 1.1 * seg_mask2
    veins = np.maximum(
        smoothstep(w_px1, 0.35, d1) * (0.45 + 0.55 * seg_mask),
        smoothstep(w_px2, 0.35, d2) * 0.72 * seg_mask2,
    ) * knot_mask

    # Fentes / gerces fines et effilées (épaisseur mesurée en pixels).
    dy_grad = np.maximum(0.4, np.abs(np.gradient(y_flow, axis=0)) * h)
    cracks = np.zeros((h, w))
    for _ in range(5):
        xc = rng.uniform(0.0, 1.0)
        yc = rng.uniform(0.18, 0.82)
        half_len = rng.uniform(0.05, 0.14)
        dx = ((x - xc + 0.5) % 1.0) - 0.5
        along = smoothstep(half_len, 0.0, np.abs(dx))
        ix = int(xc * w) % w
        iy = int(yc * h) % h
        target_flow = y_flow[iy, ix]
        px_dist = np.abs(y_flow - target_flow) * h / dy_grad
        c_mask = smoothstep(1.8 * along + 0.5, 0.25, px_dist) * smoothstep(0.08, 0.28, along)
        cracks = np.maximum(cracks, c_mask)

    # Palette brun chêne/noyer chaud (fini le ton trop orange).
    dark_vein = col(52, 27, 13)
    shadow = col(100, 58, 30)
    base = col(144, 88, 48)
    light = col(180, 122, 74)
    rim_hl = col(196, 140, 90)

    tone = 0.5 + 0.24 * streak + 0.12 * blot + 0.08 * fibers
    c = mix(shadow, base, np.clip(tone * 1.55, 0, 1))
    c = mix(c, light, np.clip((tone - 0.54) * 2.1, 0, 1))

    # Rehaut peint clair juste en dessous des creux des veines (effet sculpté à la main).
    all_grooves = np.clip(np.maximum.reduce([veins, knot_rings * 0.9, cracks]), 0, 1)
    hl_rim = np.clip(np.roll(all_grooves, 2, axis=0) - all_grooves, 0, 1)
    c = mix(c, rim_hl, hl_rim * 0.42)

    # Tracés sombres des veines, cernes de nœuds et gerces.
    c = mix(c, dark_vein, np.clip(veins * 0.65 + knot_rings * 0.72, 0, 1))
    c = mix(c, dark_vein * 0.75, np.clip(np.maximum(cracks, knot_core), 0, 1) * 0.88)

    # Légère ombre sur les bords de la bande (arêtes des poutres).
    edge = np.abs(y - 0.5) * 2
    c = c * (1 - 0.16 * smoothstep(0.72, 1.0, edge))[..., None]
    c = c * (1 + 0.05 * (1 - edge))[..., None]

    height = np.clip(0.58 + 0.14 * fibers + 0.12 * streak - 0.34 * veins - 0.28 * knot_rings - 0.42 * cracks - 0.35 * knot_core, 0, 1)
    return c, height


def glass(n):
    yy, xx = np.mgrid[0:n, 0:n] / (n - 1)
    c = mix(col(150, 182, 205), col(214, 232, 240), smoothstep(0.0, 1.0, 1 - yy) * 0.8)
    sheen = smoothstep(0.08, 0.0, np.abs((xx + (1 - yy)) - 0.9)) + smoothstep(0.04, 0.0, np.abs((xx + (1 - yy)) - 1.15)) * 0.6
    c = mix(c, col(245, 250, 252), np.clip(sheen, 0, 1) * 0.7)
    return c, np.full((n, n), 0.5)


def lattice(n):
    """Fenêtre à croisillons : losanges de verre sombre séparés par des baguettes de bois."""
    yy, xx = np.mgrid[0:n, 0:n] / n
    k = 3.0
    a = np.abs(((xx + yy) * k) % 1 - 0.5)
    b = np.abs(((xx - yy) * k) % 1 - 0.5)
    bar = np.maximum(smoothstep(0.40, 0.46, a), smoothstep(0.40, 0.46, b))
    pane = mix(col(70, 84, 96), col(120, 146, 160), smoothstep(0.5, 0.0, np.minimum(a, b)) * 0.6)
    wood = col(130, 76, 42)
    c = mix(pane, wood, bar)
    return c, 0.4 + 0.4 * bar


def brass(n):
    yy, xx = np.mgrid[0:n, 0:n] / (n - 1)
    c = mix(col(150, 112, 40), col(240, 205, 120), 1 - yy)
    return c, np.full((n, n), 0.5)


def white_paint(c, h, seed):
    """Bois peint en blanc (grange) : garde le relief du fil, peinture un peu écaillée."""
    rng = np.random.default_rng(seed)
    hh, ww = h.shape
    lum = c.mean(-1)
    paint = mix(col(214, 208, 196), col(246, 243, 236), np.clip((lum - 0.3) * 2.2, 0, 1))
    chips = smoothstep(0.55, 0.7, periodic_noise(hh, ww, 6, 25, rng)) * 0.8
    return mix(paint, c, chips[..., None] * 0.9)


def make_wood():
    W = 1024
    alb = np.zeros((W, W, 3))
    hgt = np.zeros((W, W))
    for i, f in enumerate((glass, lattice, brass)):
        c, h = f(256)
        alb[0:256, i * 256:(i + 1) * 256] = c
        hgt[0:256, i * 256:(i + 1) * 256] = h
    alb[0:256, 768:1024] = col(28, 20, 16)
    hgt[0:256, 768:1024] = 0.5
    white = alb.copy()
    for b in range(3):
        c, h = wood_band(256, W, 100 + b)
        alb[256 + b * 256:512 + b * 256] = c
        hgt[256 + b * 256:512 + b * 256] = h
        white[256 + b * 256:512 + b * 256] = white_paint(c, h, 300 + b)
    save(alb, "T_Village_Wood_Albedo.png")
    save(white, "T_Village_WoodWhite_Albedo.png")
    save(normal_from_height(hgt, 2.5), "T_Village_Wood_Normal.png")


# ------------------------------------------------------------------ planches verticales (grange), toile (tente)
def make_planks():
    """Bardage de planches verticales, raccord dans les 2 sens. 1 répétition = 8 m (6 planches)."""
    N, n = 1024, 6
    rng = np.random.default_rng(55)
    yy, xx = np.mgrid[0:N, 0:N] / N
    k = np.floor(xx * n).astype(int)
    fx = (xx * n) % 1
    plank_tone = rng.uniform(-1, 1, n)[k]
    gap = smoothstep(0.035, 0.0, np.minimum(fx, 1 - fx))
    edge = smoothstep(0.12, 0.0, np.minimum(fx, 1 - fx))
    # Joints horizontaux décalés (planches aboutées).
    joint_y = rng.uniform(0, 1, n)[k]
    joint = smoothstep(0.004, 0.0, np.abs(((yy - joint_y) % 1) - 0.5)) * (rng.random(n)[k] < 0.6)

    # Chaque planche verticale reçoit une bande de bois retravaillée (wood_band) transposée verticalement.
    wood_alb = np.zeros((N, N, 3))
    wood_hgt = np.zeros((N, N))
    for i in range(n):
        x0 = round(i * N / n)
        x1 = round((i + 1) * N / n)
        cb, hb = wood_band(x1 - x0, N, 500 + i)
        wood_alb[:, x0:x1] = np.transpose(cb, (1, 0, 2))
        wood_hgt[:, x0:x1] = hb.T

    planks_c = wood_alb * (1.0 + 0.09 * plank_tone)[..., None] * (1.0 - 0.25 * edge)[..., None]
    planks_c = mix(planks_c, col(42, 22, 10), np.clip(gap + joint, 0, 1))
    save(planks_c, "T_Village_Planks_Albedo.png")

    # Bardage rouge (grange) : garde le relief et les veines du bois sous la peinture rouge écaillée.
    lum = wood_alb.mean(-1)
    t_red = np.clip(0.5 + (lum - 0.35) * 1.4 + 0.1 * plank_tone, 0, 1)
    red_base, red_light, red_dark = col(178, 50, 40), col(206, 78, 62), col(118, 30, 26)
    rc = mix(red_dark, red_base, np.clip(t_red * 1.7, 0, 1))
    rc = mix(rc, red_light, np.clip((t_red - 0.55) * 2.0, 0, 1))
    rc = rc * (1 - 0.25 * edge)[..., None]
    wear = smoothstep(0.62, 0.8, periodic_noise(N, N, 10, 40, rng)) * 0.7
    rc = mix(rc, wood_alb, wear)
    rc = mix(rc, red_dark * 0.55, np.clip(gap + joint, 0, 1))
    save(rc, "T_Village_RedPlanks_Albedo.png")

    hgt = wood_hgt - 0.55 * gap - 0.18 * edge - 0.4 * joint
    save(normal_from_height(hgt, 3.0), "T_Village_Planks_Normal.png")


def make_canvas():
    """Toile de tente blanc cassé, raccord dans les 2 sens. 1 répétition = 8 m, couture verticale à mi-largeur."""
    N = 1024
    rng = np.random.default_rng(77)
    yy, xx = np.mgrid[0:N, 0:N] / N
    folds = periodic_noise(N, N, 140, 18, rng)          # plis verticaux larges
    stains = periodic_noise(N, N, 70, 70, rng)
    weave = periodic_noise(N, N, 1.0, 1.0, rng)
    seam_d = np.minimum(np.abs(xx - 0.5), np.minimum(xx, 1 - xx))
    seam = smoothstep(0.004, 0.0, np.abs(seam_d - 0.006)) + smoothstep(0.004, 0.0, np.abs(seam_d - 0.016))
    base, light, shade = col(232, 224, 204), col(246, 241, 228), col(196, 184, 158)
    t = 0.55 + 0.3 * folds + 0.12 * stains
    c = mix(shade, base, np.clip(t * 1.6, 0, 1))
    c = mix(c, light, np.clip((t - 0.6) * 2.5, 0, 1))
    c = mix(c, col(190, 170, 130), smoothstep(0.45, 0.8, stains) * 0.35)      # taches de boue / thé
    c = c * (1 + 0.03 * weave)[..., None]
    c = mix(c, col(150, 136, 110), np.clip(seam, 0, 1) * 0.6)
    save(c, "T_Village_Canvas_Albedo.png")
    hgt = 0.5 + 0.45 * folds + 0.03 * weave - 0.2 * np.clip(seam, 0, 1)
    save(normal_from_height(hgt, 6.0), "T_Village_Canvas_Normal.png")
    # Peinture rouge (croix de la tente) : rouge peint au pinceau.
    M = 256
    r = 0.5 + 0.5 * periodic_noise(M, M, 10, 30, rng)
    save(mix(col(170, 36, 32), col(214, 64, 52), r), "T_Village_PaintRed_Albedo.png")


# ------------------------------------------------------------------ enduit
def make_plaster():
    N = 1024
    big = periodic_noise(N, N, 90, 90)
    mid = periodic_noise(N, N, 22, 22)
    fine = periodic_noise(N, N, 3, 3)
    strokes = periodic_noise(N, N, 4, 30)       # coups de taloche horizontaux
    base, light, shade = col(238, 226, 199), col(248, 241, 222), col(214, 196, 160)
    t = 0.5 + 0.35 * big + 0.2 * mid
    c = mix(shade, base, np.clip(t * 1.5, 0, 1))
    c = mix(c, light, np.clip((t - 0.55) * 2.5, 0, 1))
    c = c * (1 + 0.025 * fine + 0.02 * strokes)[..., None]
    save(c, "T_Village_Plaster_Albedo.png")
    hgt = 0.5 + 0.25 * mid + 0.1 * fine + 0.15 * strokes
    save(normal_from_height(hgt, 3.0), "T_Village_Plaster_Normal.png")


# ------------------------------------------------------------------ tuiles
ROOF_PALETTES = {
    # (base, clair, sombre)
    "Red": (col(214, 102, 52), col(240, 150, 92), col(160, 62, 36)),
    "Blue": (col(98, 126, 172), col(142, 168, 204), col(62, 82, 124)),
    "Yellow": (col(226, 184, 92), col(246, 216, 140), col(176, 130, 56)),
    "Wood": (col(138, 86, 48), col(174, 118, 72), col(88, 52, 28)),
    "Slate": (col(82, 96, 120), col(116, 132, 156), col(52, 62, 82)),       # grange
}


def tile_cell(cw, ch, rng, pal, wood_grain):
    """Une tuile (cellule cw × ch, v=0 en haut = sous la rangée suivante, v=1 = bord bas visible)."""
    base, light, dark = pal
    yy, xx = np.mgrid[0:ch, 0:cw]
    u, v = xx / (cw - 1), yy / (ch - 1)
    shift = rng.uniform(-0.16, 0.16)
    tone = 0.5 + shift + 0.12 * periodic_noise(ch, cw, 60, 40, rng) + 0.03 * periodic_noise(ch, cw, 4, 4, rng)
    tone = tone + 0.05 * periodic_noise(ch, cw, 40, 2.5, rng)        # coups de pinceau dans le sens de la pente
    c = mix(dark, base, np.clip(tone * 1.7, 0, 1))
    c = mix(c, light, np.clip((tone - 0.5) * 2.0, 0, 1) * 0.8)
    # Haut caché sous la rangée du dessus : plus sombre ; bas éclairé.
    c = c * (0.72 + 0.28 * smoothstep(0.0, 0.45, v))[..., None]
    rim = np.minimum.reduce([u, 1 - u, (1 - v) * ch / cw])
    c = mix(c, light, smoothstep(0.06, 0.0, (1 - v) * ch / cw) * 0.55)       # arête basse claire
    c = c * (1 - 0.25 * smoothstep(0.07, 0.0, np.minimum(u, 1 - u)))[..., None]  # côtés sombres
    hgt = 0.5 + 0.3 * smoothstep(0.0, 0.08, rim)
    if wood_grain:
        g = periodic_noise(ch, cw, 60, 1.5, rng)
        grain = smoothstep(0.25, 0.6, np.abs(g))
        c = mix(c, dark, grain * 0.35)
        hgt = hgt - 0.15 * grain
    # Fente verticale qui part du bas (sur une tuile sur deux).
    if rng.random() < 0.55:
        cx = rng.uniform(0.3, 0.7)
        length = rng.uniform(0.2, 0.45)
        crack = smoothstep(0.018, 0.0, np.abs(u - cx - (1 - v) * 0.04)) * smoothstep(1 - length, 1 - length + 0.05, v)
        c = mix(c, dark * 0.7, crack * 0.85)
        hgt = hgt - 0.4 * crack
    return c, hgt


def make_roofs():
    CW, CH = 256, 512
    hgt_shared = None
    for name, pal in ROOF_PALETTES.items():
        rng = np.random.default_rng(7)          # même graine : mêmes fentes pour toutes les couleurs (normal partagée)
        alb = np.zeros((2 * CH, 4 * CW, 3))
        hgt = np.zeros((2 * CH, 4 * CW))
        for r in range(2):
            for k in range(4):
                c, h = tile_cell(CW, CH, rng, pal, wood_grain=(name == "Wood"))
                alb[r * CH:(r + 1) * CH, k * CW:(k + 1) * CW] = c
                hgt[r * CH:(r + 1) * CH, k * CW:(k + 1) * CW] = h
        save(alb, f"T_Village_RoofTile_{name}_Albedo.png")
        if name == "Red":
            hgt_shared = hgt
    save(normal_from_height(hgt_shared, 4.0, wrap=False), "T_Village_RoofTile_Normal.png")


# ------------------------------------------------------------------ forge : briques, tôle, fer, braises
def make_bricks():
    """Briques en rangées décalées, raccord dans les 2 sens. 1 répétition = 6 m (4 briques × 8 rangées) :
    briques de 1,5 × 0,75 m. Rangées le long de u (u = le long du mur, v = vers le haut)."""
    N, cols, rows = 1024, 4, 8
    rng = np.random.default_rng(91)
    yy, xx = np.mgrid[0:N, 0:N] / N
    r = np.floor(yy * rows).astype(int)
    fy = (yy * rows) % 1
    xs = xx * cols + 0.5 * (r % 2)
    k = np.floor(xs).astype(int) % cols
    fx = xs % 1
    ident = (r * cols + k) % (rows * cols)
    tone = rng.uniform(-1, 1, rows * cols)[ident]
    # Distance au joint (en fraction de brique), mortier en creux, arêtes arrondies.
    dx, dy = np.minimum(fx, 1 - fx) * 2.0, np.minimum(fy, 1 - fy)
    d = np.minimum(dx, dy)
    mortar = smoothstep(0.06, 0.03, d)
    bevel = smoothstep(0.0, 0.16, d)
    mottle = periodic_noise(N, N, 14, 20, rng)
    fine = periodic_noise(N, N, 2, 2, rng)
    chips = smoothstep(0.62, 0.75, periodic_noise(N, N, 5, 5, rng)) * (d < 0.2)
    hgt = 0.25 + 0.5 * bevel - 0.25 * mortar + 0.05 * fine - 0.2 * chips
    for name, (base, light, dark, mort) in {
        "Brick": (col(170, 84, 50), col(208, 120, 76), col(116, 52, 32), col(152, 132, 112)),
        "BrickDark": (col(118, 76, 54), col(150, 104, 76), col(72, 44, 30), col(98, 84, 70)),
    }.items():
        t = 0.5 + 0.22 * tone + 0.18 * mottle
        c = mix(dark, base, np.clip(t * 1.6, 0, 1))
        c = mix(c, light, np.clip((t - 0.55) * 2.2, 0, 1) * 0.8)
        c = c * (0.82 + 0.18 * bevel)[..., None] * (1 + 0.03 * fine)[..., None]
        c = mix(c, dark * 0.8, chips * 0.6)
        c = mix(c, mort, mortar)
        save(c, f"T_Village_{name}_Albedo.png")
    save(normal_from_height(hgt, 5.0), "T_Village_Brick_Normal.png")


def make_metal():
    """Tôle bleu-gris rivetée : une répétition en u = une plaque (joints rivetés à u = 0 / 1),
    un joint horizontal riveté à v = 0,5. Raccord dans les 2 sens."""
    N = 1024
    rng = np.random.default_rng(23)
    yy, xx = np.mgrid[0:N, 0:N] / N
    base, light, dark = col(98, 114, 138), col(146, 162, 184), col(58, 68, 88)
    big = periodic_noise(N, N, 120, 60, rng)
    streak = periodic_noise(N, N, 60, 3, rng)
    t = 0.5 + 0.25 * big + 0.1 * streak
    c = mix(dark, base, np.clip(t * 1.6, 0, 1))
    c = mix(c, light, np.clip((t - 0.6) * 2.0, 0, 1) * 0.7)
    seam_u = np.minimum(xx, 1 - xx)
    seam_v = np.abs(yy - 0.5)
    hgt = 0.5 + 0.06 * big
    # Recouvrement des plaques : bande légèrement surélevée + ligne sombre.
    lap = smoothstep(0.065, 0.06, seam_u)
    c = mix(c, dark * 0.8, smoothstep(0.006, 0.0, np.abs(seam_u - 0.06)) * 0.8)
    c = mix(c, dark * 0.8, smoothstep(0.004, 0.0, np.abs(seam_v - 0.02)) * 0.8)
    hgt = hgt + 0.1 * lap
    # Rivets : demi-sphères le long des joints.
    def rivets(cx, cy, rad):
        dd = np.sqrt(((xx - cx + 0.5) % 1 - 0.5) ** 2 + ((yy - cy + 0.5) % 1 - 0.5) ** 2) / rad
        dome = np.sqrt(np.clip(1 - dd ** 2, 0, 1))
        return dome, dd
    total = np.zeros((N, N))
    hl = np.zeros((N, N))
    for i in range(8):
        for cx, cy in ((0.0, (i + 0.5) / 8), ((i + 0.5) / 8, 0.5)):
            dome, dd = rivets(cx, cy, 0.022)
            total = np.maximum(total, dome)
            # Reflet en haut à gauche.
            hd, _ = rivets(cx - 0.007, cy - 0.007, 0.009)
            hl = np.maximum(hl, hd)
    c = mix(c, dark * 0.75, smoothstep(0.0, 0.3, total) * (1 - total) * 0.9)
    c = mix(c, base * 1.05, total * 0.6)
    c = mix(c, light * 1.1, hl * 0.8)
    hgt = hgt + 0.5 * total
    save(c, "T_Village_Metal_Albedo.png")
    save(normal_from_height(hgt, 6.0), "T_Village_Metal_Normal.png")


def make_iron():
    """Fer forgé sombre, marbré (portes, charnières, enclume, conduit). Raccord dans les 2 sens."""
    N = 512
    rng = np.random.default_rng(31)
    big = periodic_noise(N, N, 40, 40, rng)
    fine = periodic_noise(N, N, 2, 2, rng)
    hammer = smoothstep(0.3, 0.8, np.abs(periodic_noise(N, N, 6, 6, rng)))
    t = 0.5 + 0.3 * big + 0.1 * fine
    c = mix(col(34, 33, 38), col(78, 80, 90), np.clip(t, 0, 1))
    c = mix(c, col(110, 70, 46), smoothstep(0.55, 0.85, periodic_noise(N, N, 12, 12, rng)) * 0.25)   # un peu de rouille
    c = c * (1 - 0.12 * hammer)[..., None]
    save(c, "T_Village_Iron_Albedo.png")
    save(normal_from_height(0.5 + 0.2 * hammer + 0.05 * fine, 4.0), "T_Village_Iron_Normal.png")


def make_embers():
    """Braises : charbon noir parcouru de fentes orange (sert aussi de carte d'émission)."""
    N = 512
    rng = np.random.default_rng(47)
    a = np.abs(periodic_noise(N, N, 16, 16, rng))
    b = np.abs(periodic_noise(N, N, 7, 7, rng))
    glow = np.clip(smoothstep(0.12, 0.0, a) + 0.35 * smoothstep(0.06, 0.0, b), 0, 1)
    heat = 0.5 + 0.5 * periodic_noise(N, N, 50, 50, rng)
    hot = mix(col(214, 70, 12), col(255, 196, 70), glow * heat)
    c = mix(col(30, 22, 20), hot, glow)
    c = mix(c, col(90, 40, 22), (1 - glow) * heat * 0.4)
    save(c, "T_Village_Embers_Albedo.png")


# ------------------------------------------------------------------ étals et accessoires
def flat_tex(name, base, light, dark, sy, sx, seed, n=512, extra=None, normal=None):
    """Texture unie peinte : trois tons mélangés par bruit (sy, sx = taille des taches en pixels)."""
    rng = np.random.default_rng(seed)
    big = periodic_noise(n, n, sy, sx, rng)
    fine = periodic_noise(n, n, max(1, sy / 8), max(1, sx / 8), rng)
    t = 0.5 + 0.35 * big + 0.12 * fine
    c = mix(col(*dark), col(*base), np.clip(t * 1.6, 0, 1))
    c = mix(c, col(*light), np.clip((t - 0.6) * 2.2, 0, 1) * 0.8)
    if extra is not None:
        c = extra(c, rng)
    save(c, f"T_Village_{name}_Albedo.png")
    if normal:
        save(normal_from_height(0.5 + 0.3 * big + 0.2 * fine, normal), f"T_Village_{name}_Normal.png")


def make_market():
    """Toile rayée (étal du marchand) : 1 répétition en u = 1 rayure rouge + 1 blanche. Raccord dans les 2 sens."""
    N = 512
    rng = np.random.default_rng(83)
    yy, xx = np.mgrid[0:N, 0:N] / N
    red = ((xx % 1) < 0.5).astype(float)          # 1 = rouge, 0 = blanc
    folds = periodic_noise(N, N, 60, 14, rng)
    weave = periodic_noise(N, N, 1.0, 1.0, rng)
    white = mix(col(214, 206, 190), col(248, 244, 234), np.clip(0.6 + 0.4 * folds, 0, 1))
    reds = mix(col(160, 34, 30), col(214, 58, 46), np.clip(0.6 + 0.4 * folds, 0, 1))
    c = mix(white, reds, red) * (1 + 0.04 * weave)[..., None]
    edge = smoothstep(0.012, 0.0, np.minimum(np.abs(xx - 0.5), np.minimum(xx, 1 - xx)))
    c = mix(c, c * 0.8, edge)
    save(c, "T_Village_Stripes_Albedo.png")
    flat_tex("Clay", (186, 96, 58), (216, 130, 86), (130, 62, 38), 40, 40, 84)
    # Toile de jute : trame visible.
    def weave_fx(c, rng):
        yy, xx = np.mgrid[0:512, 0:512] / 512
        w = (np.sin(xx * np.pi * 2 * 64) * np.sin(yy * np.pi * 2 * 64)) * 0.5 + 0.5
        return c * (0.88 + 0.12 * w)[..., None]
    flat_tex("Burlap", (176, 146, 102), (206, 180, 136), (126, 100, 66), 30, 30, 85, extra=weave_fx, normal=2.0)
    # Foin : brins le long de v.
    def straw(c, rng):
        s = periodic_noise(512, 512, 30, 1.2, rng)
        return mix(c, col(150, 110, 40), smoothstep(0.2, 0.7, np.abs(s)) * 0.5)
    flat_tex("Hay", (222, 182, 82), (246, 216, 124), (168, 126, 46), 50, 6, 86, extra=straw, normal=3.0)
    # Terre retournée (potager).
    def clods(c, rng):
        s = smoothstep(0.5, 0.8, periodic_noise(512, 512, 4, 4, rng))
        return mix(c, col(150, 108, 74), s * 0.5)
    flat_tex("Soil", (96, 64, 42), (128, 90, 60), (62, 40, 26), 20, 20, 87, extra=clods, normal=3.0)
    flat_tex("Water", (92, 150, 182), (150, 200, 220), (56, 104, 140), 60, 120, 88)
    flat_tex("LampGlass", (250, 206, 110), (255, 240, 190), (226, 150, 60), 60, 60, 89)
    flat_tex("Leaf", (92, 168, 64), (140, 206, 92), (52, 112, 40), 30, 30, 90)
    # Bois de bout (dessus des souches, rondins, bûches) : cernes concentriques harmonisés avec le nouveau bois.
    N = 512
    rng = np.random.default_rng(91)
    yy, xx = (np.mgrid[0:N, 0:N] + 0.5) / N * 2 - 1
    wob = periodic_noise(N, N, 28, 28, rng) * 0.06
    r = np.sqrt(xx ** 2 + yy ** 2) + wob
    rings = np.sin(r * np.pi * 2 * 7) * 0.5 + 0.5
    rings = smoothstep(0.25, 0.85, rings)
    c = mix(col(186, 130, 82), col(148, 92, 52), rings * 0.75)
    c = mix(c, col(116, 70, 38), smoothstep(0.80, 0.89, r))                 # aubier plus sombre
    c = mix(c, col(76, 44, 22), smoothstep(0.91, 0.97, r))                  # écorce
    ang = np.arctan2(yy, xx)
    crack = (smoothstep(0.022, 0.0, np.abs((ang - 0.7 + np.pi) % (2 * np.pi) - np.pi)) * smoothstep(0.10, 0.32, r)
             + smoothstep(0.016, 0.0, np.abs((ang + 2.1 + np.pi) % (2 * np.pi) - np.pi)) * smoothstep(0.25, 0.45, r)) * (r < 0.82)
    c = mix(c, col(52, 27, 13), np.clip(crack, 0, 1) * 0.85)
    save(c, "T_Village_EndGrain_Albedo.png")


if __name__ == "__main__":
    make_wood()
    make_plaster()
    make_roofs()
    make_planks()
    make_canvas()
    make_bricks()
    make_metal()
    make_iron()
    make_embers()
    make_market()
