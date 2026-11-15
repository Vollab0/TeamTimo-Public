# Génère la texture des pierres sculptées des arches du Lobby (TeamTimo).
# Lancer avec le Python de Blender (numpy) :
#   "C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe" gen_gate_textures.py [outdir]
# Sorties (2048 x 1536) : T_GateCarving_Albedo.png, _Normal.png (OpenGL), _Height.png, _AO.png
# Disposition (lue par build_gate.py) :
#   tiers haut (v 2/3..1)   : frise de grecques (méandres), 8 motifs, tileable en U ;
#   tiers milieu (v 1/3..2/3) : 4 panneaux carrés sculptés (spirale, rosace, losange, quadrilobe), u 0..0,25 / ..0,5 / ..0,75 / ..1 ;
#   tiers bas (v 0..1/3)     : 4 roses des vents marquées N, E, S, W (lettres : compass_letters.json, gen_compass_letters.py).
# Unité de dessin : 32 px. Même éclairage peint et même teinte que T_WallStone pour que les deux se raccordent.
import sys, os, math
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from texutil import save_png, tile_noise, blur, smoothstep

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Art", "Textures", "Environment", "Walls"))
W, Hh = 2048, 1536
U = 32.0                  # pixels par unité
rng = np.random.default_rng(11)

FIELD = 0.22              # fond creusé
TOP = 0.84                # dessus du relief
BEV = 0.32                # largeur du biseau (unités)

def noise(cells, seed, octv=2, p=0.5):
    return tile_noise(W, cells, seed, octv, p)[:Hh]   # recadré : reste tileable en U

# ---------------------------------------------------------------- distances (unités)
def seg_box_dist(px, py, a, b):
    """Distance de Tchebychev à un segment horizontal ou vertical (extrémités carrées)."""
    x0, x1 = min(a[0], b[0]), max(a[0], b[0]); y0, y1 = min(a[1], b[1]), max(a[1], b[1])
    dx = np.maximum(np.maximum(x0 - px, px - x1), 0); dy = np.maximum(np.maximum(y0 - py, py - y1), 0)
    return np.maximum(dx, dy)

def poly_dist(px, py, pts):
    d = np.full(px.shape, 1e9, np.float32)
    for a, b in zip(pts, pts[1:]): d = np.minimum(d, seg_box_dist(px, py, a, b))
    return d

def relief(sd):
    """sd : distance signée (unités, > 0 à l'intérieur du relief) -> hauteur avec biseau arrondi."""
    t = np.clip(sd / BEV, 0, 1)
    return FIELD + (TOP - FIELD) * (1 - (1 - t) ** 2.2)

H = np.full((Hh, W), FIELD, np.float32)
INK = np.zeros((Hh, W), np.float32)      # lettres gravées : assombries dans l'albédo pour rester lisibles de loin

# ---------------------------------------------------------------- frise de grecques (rangées 0..511)
yy, xx = np.mgrid[0:512, 0:W].astype(np.float32)
bx = (xx + 0.5) / U                       # 0..64
by = 16.0 - (yy + 0.5) / U                # 0 en bas de la frise, 16 en haut
hy = by - 3.0                             # repère du crochet : bordure basse en 0, haute en 10
d = np.full(bx.shape, 1e9, np.float32)
for k in range(9):
    x0 = 8 * k + 1
    hook = [(x0, 0), (x0, 8), (x0 + 6, 8), (x0 + 6, 2), (x0 + 2, 2), (x0 + 2, 6), (x0 + 4, 6), (x0 + 4, 4)]
    for xoff in (0, -64):                 # enroulement pour la tuile
        d = np.minimum(d, poly_dist(bx, hy, [(p[0] + xoff, p[1]) for p in hook]))
d = np.minimum(d, np.abs(hy))                       # bordure basse (ligne en hy = 0)
d = np.minimum(d, np.abs(hy - 10))                  # bordure haute
sd_lines = 0.5 - d
# cadre plein au bord de la frise (rainure de 1 entre le cadre et la bordure)
sd_frame = np.maximum(1.5 - by, by - 14.5)
sd_frame = np.minimum(sd_frame, np.minimum(by, 16 - by) + 0.15)   # biseau vers le bord de la pièce
sd = np.maximum(sd_lines, sd_frame)
H[:512] = relief(sd)

# ---------------------------------------------------------------- panneaux (rangées 512..1023)
py_, px_ = np.mgrid[0:512, 0:512].astype(np.float32)
lx = (px_ + 0.5) / U - 8.0                # -8..8, centre du panneau
ly = 8.0 - (py_ + 0.5) / U
r = np.hypot(lx, ly); th = np.arctan2(ly, lx)
edge = 8 - np.maximum(np.abs(lx), np.abs(ly))      # distance au bord du panneau
sd_frame = np.minimum(edge + 0.15, 1.6 - edge)       # cadre : 1,6 unité de large au bord
def panel(sd_motif):
    return np.maximum(sd_frame, np.minimum(sd_motif, edge - 2.6))   # rainure de 1 entre cadre et motif

# 0 : spirale carrée (grecque)
sp = [(-5, -5), (-5, 5), (5, 5), (5, -3), (-3, -3), (-3, 3), (3, 3), (3, -1), (-1, -1), (-1, 1), (1, 1)]
p0 = panel(0.5 - poly_dist(lx, ly, sp))
# 1 : rosace à 8 pétales + bouton central
petal = 5.3 * (0.55 + 0.45 * np.abs(np.cos(4 * th)) ** 0.7)
p1 = panel(np.maximum(petal - r, 1.6 - r) * 0.8)
p1 = np.where((r > 1.6) & (r < 2.3), np.minimum(p1, (np.abs(r - 1.95) - 0.35) * 0.8 + 0.0), p1)  # sillon autour du bouton
# 2 : losange gigogne + disque central
dia = np.abs(lx) + np.abs(ly)
p2 = panel(np.maximum(np.maximum(0.6 - np.abs(dia - 5.2), 0.6 - np.abs(dia - 2.9)), 1.3 - r))
# 3 : quadrilobe (quatre cercles) + croix fine
q = np.minimum.reduce([np.hypot(lx - cx, ly - cy) for (cx, cy) in ((2.4, 0), (-2.4, 0), (0, 2.4), (0, -2.4))])
p3 = panel(np.maximum(0.55 - np.abs(q - 2.3), 0.9 - r))
for i, pnl in enumerate((p0, p1, p2, p3)):
    H[512:1024, i * 512:(i + 1) * 512] = relief(pnl)

# ---------------------------------------------------------------- roses des vents N, E, S, W (rangées 1024..1535)
import json
LETTERS = json.load(open(os.path.join(HERE, "compass_letters.json")))["letters"]
LETTER_H = 5.0            # hauteur de la lettre (unités)
DISC_R = 4.15             # disque central où la lettre est gravée
frame_h = relief(np.minimum(edge + 0.15, 0.9 - edge))   # cadre plus fin : la rose doit se lire de loin
inside = smoothstep(1.3, 1.6, edge)                   # le motif reste dans la rainure du cadre
# étoile : 4 grandes branches (points cardinaux) + 4 petites ; chaque branche a une arête centrale (2 facettes)
star = np.full(lx.shape, FIELD, np.float32)
for k in range(8):
    a = k * math.pi / 4
    L, w, hmax = (6.5, 2.0, TOP - 0.04) if k % 2 == 0 else (5.5, 1.5, TOP - 0.16)
    t = lx * math.cos(a) + ly * math.sin(a); s = np.abs(-lx * math.sin(a) + ly * math.cos(a))
    wt = w * np.clip(1 - t / L, 0, 1)
    ok = (t > 0) & (s < wt)
    hb = FIELD + 0.08 + (hmax - FIELD - 0.08) * (1 - s / np.maximum(wt, 1e-4)) * (0.55 + 0.45 * np.clip(1 - t / L, 0, 1))
    star = np.where(ok, np.maximum(star, hb), star)
ring = relief(0.28 - np.abs(r - 5.45)) - FIELD
disc = relief(DISC_R - r)
def letter_sd(segs):
    """Distance signée (unités, > 0 dans la lettre) : remplissage pair-impair + distance aux contours."""
    S = np.array(segs, np.float32) * LETTER_H
    inl = np.zeros(lx.shape, bool); d = np.full(lx.shape, 1e9, np.float32)
    for (x0, y0, x1, y1) in S:
        if y1 != y0:              # segment horizontal : ne coupe jamais le rayon
            inl ^= ((y0 > ly) != (y1 > ly)) & (lx < x0 + (ly - y0) * (x1 - x0) / (y1 - y0))
        ex, ey = x1 - x0, y1 - y0; L2 = ex * ex + ey * ey + 1e-12
        tt = np.clip(((lx - x0) * ex + (ly - y0) * ey) / L2, 0, 1)
        d = np.minimum(d, np.hypot(lx - (x0 + tt * ex), ly - (y0 + tt * ey)))
    return np.where(inl, d, -d)
for i, ch in enumerate("NESW"):
    sdl = letter_sd(LETTERS[ch])
    engrave = smoothstep(-0.04, 0.12, sdl)              # gravure à fond plat, bords nets
    hd = disc - (disc - FIELD - 0.06) * engrave * (r < DISC_R + 0.2)
    INK[1024:, i * 512:(i + 1) * 512] = engrave * (r < DISC_R + 0.2)
    motif = np.maximum(np.maximum(star, FIELD + ring), np.where(r < DISC_R + BEV, hd, FIELD))
    H[1024:, i * 512:(i + 1) * 512] = np.maximum(frame_h, FIELD + (motif - FIELD) * inside)

# ---------------------------------------------------------------- usure : éclats, grain, fissures
micro = noise(128, 3, 3, 0.55)
chip = smoothstep(0.66, 0.78, noise(40, 4, 3, 0.55))
low = noise(8, 5, 3, 0.5)
raised = (H > FIELD + 0.05).astype(np.float32)
H = H - chip * raised * 0.09 * (H - FIELD) / (TOP - FIELD)        # bords écornés
H = H + (micro - 0.5) * 0.03 + (low - 0.5) * 0.05 * raised
# mousse dans les creux
cav = np.clip(blur(H, 10) - H, 0, None)
mn = noise(9, 6, 4, 0.55)
moss = smoothstep(0.66, 0.76, mn) * np.clip(1 - raised * 0.9, 0, 1) * smoothstep(0.01, 0.06, cav) * 0.85
H = H + moss * 0.08

# ---------------------------------------------------------------- normales (OpenGL), occlusion
STR = 9.0
Hp = blur(H, 1, 1)
dx = (np.roll(Hp, -1, 1) - np.roll(Hp, 1, 1)) * 0.5 * STR
drow = (np.roll(Hp, -1, 0) - np.roll(Hp, 1, 0)) * 0.5 * STR
nx = -dx; ny = drow; nz = np.ones_like(nx)
ln = np.sqrt(nx * nx + ny * ny + nz * nz); nx /= ln; ny /= ln; nz /= ln
normal = np.stack([nx * 0.5 + 0.5, ny * 0.5 + 0.5, nz * 0.5 + 0.5], -1)
ao = 1 - np.clip((blur(H, 12) - H) * 3.4, 0, 1) * 0.85
ao = np.clip(ao * (0.8 + 0.2 * np.clip(H / 0.8, 0, 1)), 0, 1)

# ---------------------------------------------------------------- albédo peint (teinte des murs)
base = np.array([0.55, 0.55, 0.53], np.float32)
mott = noise(18, 7, 3, 0.55)
col = base * (0.86 + 0.24 * mott[..., None])
col = col * (0.95 + 0.1 * micro[..., None])
light = np.clip(ny * 2.2, -1, 1)
col = col * (1 + 0.34 * np.clip(light, 0, 1)[..., None] - 0.32 * np.clip(-light, 0, 1)[..., None])
depth = np.clip((H - FIELD) / (TOP - FIELD), 0, 1)
col = col * (0.58 + 0.42 * depth[..., None])                         # fond plus sombre
col = col * (0.55 + 0.45 * ao[..., None])
m1 = np.array([0.15, 0.25, 0.08], np.float32); m2 = np.array([0.34, 0.46, 0.14], np.float32)
mt = np.clip(noise(30, 8, 2) * 0.8 + np.clip(light, 0, 1) * 0.4, 0, 1)[..., None]
col = col * (1 - 0.5 * INK[..., None])
col = col * (1 - moss[..., None]) + (m1 * (1 - mt) + m2 * mt) * (0.75 + 0.25 * ao[..., None]) * moss[..., None]

os.makedirs(OUT, exist_ok=True)
save_png(os.path.join(OUT, "T_GateCarving_Albedo.png"), col)
save_png(os.path.join(OUT, "T_GateCarving_Normal.png"), normal)
save_png(os.path.join(OUT, "T_GateCarving_Height.png"), (H - H.min()) / (H.max() - H.min()))
save_png(os.path.join(OUT, "T_GateCarving_AO.png"), ao)
print("ok")
