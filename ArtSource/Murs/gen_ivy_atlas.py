# Atlas de lierre stylisé (RGBA, alpha découpé) pour les cartes de végétation des murs.
# Disposition (UV, origine en bas à gauche) :
#   A = brin pendant   x 0.00-0.25, y 0-1
#   B = brin pendant   x 0.25-0.50, y 0-1
#   C = touffe         x 0.50-1.00, y 0.5-1   (haut de l'image)
#   D = touffe         x 0.50-1.00, y 0-0.5   (bas de l'image)
import sys, os, math
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from texutil import save_png, blur

N = 1024
rng = np.random.default_rng(3)
img = np.zeros((N, N, 4), np.float32)   # RGBA prémultiplié

def over(x0, y0, rgba):
    h, w = rgba.shape[:2]
    x1, y1 = x0 + w, y0 + h
    cx0, cy0 = max(x0, 0), max(y0, 0); cx1, cy1 = min(x1, N), min(y1, N)
    if cx0 >= cx1 or cy0 >= cy1: return
    src = rgba[cy0 - y0:cy1 - y0, cx0 - x0:cx1 - x0]
    dst = img[cy0:cy1, cx0:cx1]
    a = src[..., 3:4]
    dst[..., :3] = src[..., :3] * a + dst[..., :3] * (1 - a)
    dst[..., 3:4] = a + dst[..., 3:4] * (1 - a)

def draw_stem(pts, width, col, clip):
    pts = np.array(pts, np.float32)
    x0, y0 = np.floor(pts.min(0) - width - 2).astype(int); x1, y1 = np.ceil(pts.max(0) + width + 2).astype(int)
    xs = np.arange(x0, x1) + 0.5; ys = np.arange(y0, y1) + 0.5
    X, Y = np.meshgrid(xs, ys)
    d = np.full(X.shape, 1e9, np.float32); tpar = np.zeros(X.shape, np.float32)
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]; v = b - a; L2 = float(v @ v) + 1e-6
        t = np.clip(((X - a[0]) * v[0] + (Y - a[1]) * v[1]) / L2, 0, 1)
        dd = np.hypot(X - a[0] - t * v[0], Y - a[1] - t * v[1])
        better = dd < d
        tpar = np.where(better, (i + t) / (len(pts) - 1), tpar); d = np.minimum(d, dd)
    wloc = width * (1 - 0.6 * tpar)
    alpha = np.clip(wloc - d + 0.5, 0, 1)
    shade = np.clip(1 - d / (wloc + 1e-3), 0, 1)
    rgb = np.array(col)[None, None, :] * (0.7 + 0.4 * shade[..., None])
    rgba = np.concatenate([rgb, alpha[..., None]], -1)
    rgba = clip_region(rgba, x0, y0, clip)
    over(x0, y0, rgba)

def clip_region(rgba, x0, y0, clip):
    cx0, cy0, cx1, cy1 = clip
    h, w = rgba.shape[:2]
    X = np.arange(x0, x0 + w)[None, :]; Y = np.arange(y0, y0 + h)[:, None]
    m = (X >= cx0 + 2) & (X < cx1 - 2) & (Y >= cy0 + 2) & (Y < cy1 - 2)
    rgba = rgba.copy(); rgba[..., 3] *= m
    return rgba

def draw_leaf(px, py, ang, length, width, base, clip, dark=1.0):
    # feuille en amande légèrement lobée, nervure centrale, contour sombre (style peint)
    R = length + width + 4
    x0, y0 = int(px - R), int(py - R)
    xs = np.arange(x0, x0 + 2 * int(R)) + 0.5 - px; ys = np.arange(y0, y0 + 2 * int(R)) + 0.5 - py
    X, Y = np.meshgrid(xs, ys)
    ca, sa = math.cos(ang), math.sin(ang)
    u = (X * ca + Y * sa) / length          # le long de la feuille 0..1
    v = (-X * sa + Y * ca)                   # en travers (px)
    uc = np.clip(u, 0, 1)
    lobe = 1 + 0.18 * np.sin(uc * math.pi * 3) ** 2
    half = width * np.sin(np.clip(u, 0, 1) * math.pi) ** 0.75 * (1.15 - 0.45 * uc) * lobe
    sd = half - np.abs(v)                    # >0 dedans
    sd = np.where((u > 0) & (u < 1), sd, -1e3)
    alpha = np.clip(sd + 0.5, 0, 1)
    side = np.where(v > 0, 1.0, 0.0)
    col = np.array(base, np.float32)
    light = col * 1.25 + 0.03; darkc = col * 0.72
    rgb = darkc[None, None] * (1 - side[..., None]) + light[None, None] * side[..., None]
    rgb = rgb * (0.85 + 0.25 * uc[..., None])                   # pointe plus claire
    rib = np.exp(-(np.abs(v) / 1.3) ** 2) * (u < 0.85)
    rgb = rgb * (1 - 0.0 * rib[..., None]) + np.array([0.75, 0.85, 0.45]) * 0.55 * rib[..., None] * col.mean() * 2
    edge = np.clip(1 - sd / 2.2, 0, 1)                            # contour
    rgb = rgb * (1 - 0.55 * edge[..., None])
    rgb = rgb * dark
    rgba = np.concatenate([np.clip(rgb, 0, 1), alpha[..., None]], -1)
    rgba = clip_region(rgba, x0, y0, clip)
    over(x0, y0, rgba)

GREENS = [(0.22, 0.42, 0.10), (0.28, 0.50, 0.12), (0.35, 0.55, 0.14), (0.18, 0.36, 0.09), (0.30, 0.46, 0.10)]
STEM = (0.30, 0.24, 0.13)

def vine(path, clip, leaf_every=22, leaf_len=(30, 52), dark=1.0, stem_w=3.0):
    """path: liste de points ; feuilles alternées le long du brin."""
    path = np.array(path, np.float32)
    # rééchantillonnage
    seg = np.hypot(*np.diff(path, axis=0).T); cum = np.concatenate([[0], np.cumsum(seg)])
    total = cum[-1]
    pts = [path[0]]
    draw_stem(path, stem_w, STEM, clip)
    s = rng.uniform(5, 15); side = 1
    leaves = []
    while s < total - 4:
        i = np.searchsorted(cum, s) - 1; i = max(0, min(i, len(path) - 2))
        t = (s - cum[i]) / max(seg[i], 1e-3)
        p = path[i] + (path[i + 1] - path[i]) * t
        d = (path[i + 1] - path[i]) / max(seg[i], 1e-3)
        base_ang = math.atan2(d[1], d[0])
        ang = base_ang + side * rng.uniform(0.7, 1.4)
        scale = 1 - 0.45 * (s / total)  # feuilles plus petites vers le bout
        L = rng.uniform(*leaf_len) * (0.6 + 0.4 * scale)
        leaves.append((p[0], p[1], ang, L, L * rng.uniform(0.38, 0.5), GREENS[rng.integers(len(GREENS))]))
        side = -side; s += leaf_every * rng.uniform(0.7, 1.3) * (0.7 + 0.3 * scale)
    rng.shuffle(leaves)
    for (x, y, a, L, W, c) in leaves:
        draw_leaf(x, y, a, L, W, c, clip, dark * rng.uniform(0.85, 1.05))

def hanging(cx, clip, top, bottom, sway):
    pts = []
    n = 14
    ph = rng.uniform(0, 6.28)
    for k in range(n + 1):
        t = k / n
        pts.append((cx + math.sin(ph + t * 5.5) * sway * (0.3 + t), top + (bottom - top) * t))
    return pts

# A et B : brins pendants (2 brins chacun, l'un plus sombre derrière)
for (xa, xb) in ((0, 256), (256, 512)):
    clip = (xa, 0, xb, N)
    cx = (xa + xb) / 2
    vine(hanging(cx - 45, clip, 0, N * rng.uniform(0.55, 0.7), 22), clip, dark=0.65, leaf_len=(34, 56))
    vine(hanging(cx + 40, clip, 0, N * rng.uniform(0.6, 0.8), 22), clip, dark=0.75, leaf_len=(34, 56))
    vine(hanging(cx, clip, 0, N * rng.uniform(0.88, 0.97), 30), clip, leaf_len=(38, 60))

# C et D : touffes (brins qui rayonnent depuis le bas/centre)
for (ya, yb) in ((0, 512), (512, 1024)):
    clip = (512, ya, 1024, yb)
    ox, oy = 768, (ya + yb) / 2 + 40
    for k in range(7):
        a = -math.pi / 2 + rng.uniform(-1.4, 1.4)
        L = rng.uniform(140, 230)
        pts = []
        cur = np.array([ox + rng.uniform(-30, 30), oy + rng.uniform(-10, 30)])
        bend = rng.uniform(-0.25, 0.25)
        for i in range(8):
            pts.append(tuple(cur)); a += bend
            cur = cur + np.array([math.cos(a), math.sin(a)]) * L / 7
        vine(pts, clip, leaf_every=18, leaf_len=(28, 46), dark=0.7 if k < 3 else 1.0, stem_w=2.5)

# PNG : ligne 0 = haut. La couleur sous l'alpha nul est étalée pour éviter les franges en mip.
rgb = img[..., :3]; a = img[..., 3]
w = np.clip(a, 0, 1)
fill = np.zeros_like(rgb)
for c in range(3):
    num = blur(rgb[..., c] * w, 6, 2); den = blur(w, 6, 2) + 1e-4
    fill[..., c] = num / den
rgb = np.where(w[..., None] > 0.01, rgb, fill)
_here = os.path.dirname(os.path.abspath(__file__))
out = os.path.join(sys.argv[1] if len(sys.argv) > 1 else os.path.normpath(os.path.join(_here, "..", "..", "Assets", "Art", "Textures", "Environment", "Walls")), "T_WallIvy_Atlas.png")
save_png(out, np.concatenate([rgb, a[..., None]], -1))
print("ok", out)
