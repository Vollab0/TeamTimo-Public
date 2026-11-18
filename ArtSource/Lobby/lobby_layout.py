# -*- coding: utf-8 -*-
"""Convertit le relevé du concept art du Lobby (lobby_data.py) en plan de placement.

Lancer depuis la racine du dépôt :  python ArtSource/Lobby/lobby_layout.py
Sorties :
  ArtSource/Lobby/lobby_placement.json                      positions en mètres (source pour le placement dans Unity)
  GameDesign/99_Attachments/Lobby/Lobby_Plan_Dessus.png     plan vu de dessus annoté
  GameDesign/99_Attachments/Lobby/Lobby_Vue_Reperes.jpg     concept art avec les identifiants de chaque élément
  GameDesign/99_Attachments/Lobby/Lobby_Vue_Redressee.jpg   concept art redressé à plat (sol), grille de 50 m
  GameDesign/99_Attachments/Lobby/Lobby_Chemins_Masque.png  masque des chemins (blanc = terre), Nord en haut
  GameDesign/99_Attachments/Lobby/Refs/Ref_Lobby_*.jpg      recadrages de référence pour Gemini
Repère : centre du Lobby = origine, +X = Est, +Z = Nord, mètres. Lobby = carré de 400 m (murs à ±200).
"""
import json, math, os, random, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, HERE)
import lobby_data as D

SRC = os.path.join(ROOT, 'GameDesign', '99_Attachments', 'Brainstorming', 'Lobby_Vue.jpg')
OUT = os.path.join(ROOT, 'GameDesign', '99_Attachments', 'Lobby')
os.makedirs(os.path.join(OUT, 'Refs'), exist_ok=True)

# ---------------------------------------------------------------- caméra
_p = np.array(json.load(open(os.path.join(HERE, 'concept_camera.json')))['p'])


def _rot(yaw, pitch, roll):
    cy, sy, cp, sp, cr, sr = math.cos(yaw), math.sin(yaw), math.cos(pitch), math.sin(pitch), math.cos(roll), math.sin(roll)
    f = np.array([sy * cp, -sp, cy * cp]); r = np.array([cy, 0, -sy]); u = np.cross(f, r)
    return np.array([[cr, -sr, 0], [sr, cr, 0], [0, 0, 1]]) @ np.stack([r, u, f])


R = _rot(_p[3], _p[4], _p[5]); CAM = _p[:3]; F, U0, V0 = _p[6], _p[7], _p[8]


def project(X):
    d = (np.atleast_2d(np.asarray(X, float)) - CAM) @ R.T
    return np.stack([U0 + F * d[..., 0] / d[..., 2], V0 - F * d[..., 1] / d[..., 2]], -1)


def unproject(u, v, y=0.0):
    dw = np.array([(u - U0) / F, -(v - V0) / F, 1.0]) @ R
    t = (y - CAM[1]) / dw[1]
    return np.array([CAM[0] + t * dw[0], CAM[2] + t * dw[2]])


def W(pt):
    """Point du relevé -> (x, z) en mètres."""
    if pt[0] == 'w':
        return np.array([pt[1], pt[2]], float)
    return unproject(pt[0], pt[1])


def height_at(xz, v):
    """Hauteur du point situé à la verticale de xz et vu à la ligne v de l'image."""
    lo, hi = 0.0, 90.0
    for _ in range(40):
        mid = (lo + hi) / 2
        if project([xz[0], mid, xz[1]])[0][1] > v: lo = mid
        else: hi = mid
    return (lo + hi) / 2


def yaw_of(n):  # rotation Y Unity telle que l'avant (0,0,1) pointe vers n
    return (math.degrees(math.atan2(n[0], n[1])) + 360) % 360


def unit(v): return v / np.linalg.norm(v)
def perp(v): return np.array([-v[1], v[0]])
def r1(v): return [round(float(a), 1) for a in v]


# ---------------------------------------------------------------- conversion
# Bâtiments dont la porte est sur un pignon : le faîtage suit la profondeur (sinon il suit la façade).
PIGNON_EN_FACADE = ('G1', 'T1', 'F1', 'M01', 'M04', 'M05', 'M08', 'M09', 'M10', 'M11', 'M12')
# Cour sablée de la forge (adossée au mur Nord), en mètres.
FORGE_YARD = [[-103, 196], [-103, 128], [-44, 118], [-44, 196]]
# Forêt dense de la moitié Est (polygone en mètres) et clairière de la grange (x, z, rayon).
FOREST = [[-22, 194], [194, 194], [194, -194], [-12, -194], [2, -120], [18, -62], [22, -8], [6, 58], [-18, 120]]
FOREST_CLEARINGS = [[110, 53, 42], [70, 28, 34]]
FOREST_SPACING = 15


def convert():
    fire = W(next(c for c in D.CENTERED if c['id'] == 'C1')['px'])
    bat = []
    for b in D.BUILDINGS:
        g, d = W(b['G']), W(b['D'])
        if b['P'] is None:                                   # grange : façade G-D + profondeur
            a = unit(d - g); n = perp(a)
            if np.dot(n, (g + d) / 2 - CAM[[0, 2]]) > 0: n = -n   # normale sortante = vers la caméra
            width, depth = np.linalg.norm(d - g), b['profondeur']
            center = (g + d) / 2 - n * depth / 2
            corners = [g, d, d - n * depth, g - n * depth]
        else:
            p = W(b['P']); e1, e2 = g - p, d - p
            l1, l2 = np.linalg.norm(e1), np.linalg.norm(e2)
            if l1 >= l2:
                u1 = unit(e1); u2 = perp(u1); u2 = u2 if np.dot(u2, e2) > 0 else -u2
            else:
                u2 = unit(e2); u1 = perp(u2); u1 = u1 if np.dot(u1, e1) > 0 else -u1
            center = p + (u1 * l1 + u2 * l2) / 2
            corners = [p, p + u1 * l1, p + u1 * l1 + u2 * l2, p + u2 * l2]
            if b['porte'] == 'GP': n, width, depth, a = -u2, l1, l2, u1
            else: n, width, depth, a = -u1, l2, l1, u2
        h = height_at(center, b['faitage'][1]) if b['faitage'] else 0.4
        bat.append(dict(id=b['id'], nom=b['nom'], type=b['type'], toit=b['toit'], options=b.get('options', []),
                        x=round(float(center[0]), 1), z=round(float(center[1]), 1), yaw_deg=round(yaw_of(n)),
                        largeur_facade_m=round(float(width)), profondeur_m=round(float(depth)), hauteur_m=round(float(h)) if h > 1 else h,
                        pignon_en_facade=b['id'] in PIGNON_EN_FACADE,
                        coins=[r1(c) for c in corners], note=b.get('note', '')))
    elts = []
    for c in D.CENTERED:
        pos = W(c['px'])
        if c['face'] is None: yaw = 0
        else:
            tgt = fire if c['face'] == 'feu' else W(c['face'])
            yaw = round(yaw_of(unit(tgt - pos)))
        w, dp, h = c['taille']
        n = np.array([math.sin(math.radians(yaw)), math.cos(math.radians(yaw))]); a = perp(n)
        corners = [pos + a * w / 2 + n * dp / 2, pos - a * w / 2 + n * dp / 2, pos - a * w / 2 - n * dp / 2, pos + a * w / 2 - n * dp / 2]
        elts.append(dict(id=c['id'], nom=c['nom'], type=c['type'], x=round(float(pos[0]), 1), z=round(float(pos[1]), 1),
                         yaw_deg=yaw, largeur_facade_m=w, profondeur_m=dp, hauteur_m=h, coins=[r1(k) for k in corners]))
    clot = []
    for f in D.FENCES:
        pts = [W(q) for q in f['pts']]
        L = sum(np.linalg.norm(pts[i + 1] - pts[i]) for i in range(len(pts) - 1))
        clot.append(dict(id=f['id'], nom=f['nom'], style=f['style'], points=[r1(q) for q in pts], longueur_m=round(float(L))))
    chem = [dict(id=s['id'], nom=s['nom'], largeur_m=s['largeur'], ajout_hors_concept=bool(s.get('ajout')),
                 points=[r1(W(q)) for q in s['pts']]) for s in D.PATHS]
    plaza = [r1(W(q)) for q in D.PLAZA]
    acc = [dict(id='P%d' % (i + 1), type=t, nom=n, x=round(float(W(px)[0]), 1), z=round(float(W(px)[1]), 1))
           for i, (t, px, n) in enumerate(D.PROPS)]
    return dict(
        meta=dict(source='GameDesign/99_Attachments/Brainstorming/Lobby_Vue.jpg',
                  repere='Centre du Lobby = origine du monde. +X = Est, +Z = Nord, Y = haut. Mètres.',
                  lobby='Carré de 400 m : murs à x = ±200 et z = ±200 (face intérieure ≈ ±196,5).',
                  portes=dict(Nord=[25, 200], Sud=[-25, -200], Est=[200, -25], Ouest=[-200, 25]),
                  pignon='pignon_en_facade = vrai : le faîtage suit la profondeur ; faux : il suit la façade.',
                  yaw='yaw_deg = rotation Y Unity ; à 0 la façade (porte) regarde +Z (Nord), à 90 elle regarde +X (Est).',
                  coins='coins = emprise au sol [x, z] (4 points).',
                  precision='Relevé par reprojection du concept art : ±3 m près de la place, ±8 m pour la grange et la forge.',
                  camera_concept=dict(position=r1(CAM), yaw_deg=round(math.degrees(_p[3]), 1), pitch_deg=round(math.degrees(_p[4]), 1),
                                      focale_px=round(float(F), 1), image=[2000, 1116])),
        batiments=bat, elements=elts, clotures=clot, chemins=chem,
        place=dict(polygone=plaza, centre=r1(np.mean(np.array(plaza), 0))),
        foret=dict(polygone=FOREST, clairieres=FOREST_CLEARINGS, espacement_m=FOREST_SPACING,
                   note='Zone à densifier en sapins du terrain (espacement moyen), hors chemins et clairières.'),
        zones_terre=[dict(nom='Place', polygone=plaza), dict(nom='Cour de la forge', polygone=FORGE_YARD)],
        accessoires=acc, villageois=[r1(W(q)) for q in D.VILLAGERS], arbres_heros=[r1(W(q)) for q in D.HERO_TREES])


# ---------------------------------------------------------------- dessin
def font(size, bold=False):
    try: return ImageFont.truetype('C:/Windows/Fonts/%s.ttf' % ('arialbd' if bold else 'arial'), size)
    except OSError: return ImageFont.load_default()


ROOF = {'rouge': (196, 62, 52), 'bleu': (72, 108, 178), 'jaune': (226, 190, 70), 'brun': (150, 96, 56),
        'gris ardoise': (84, 88, 104), 'toile blanche': (236, 232, 220), 'tole bleue': (92, 96, 120), 'terre': (112, 78, 50)}
ELT = {'Forge': (92, 96, 120), 'Etal': (176, 122, 70), 'Feu': (240, 140, 30), 'Potager': (112, 78, 50)}


def tag(d, xy, text, f, fill=(255, 255, 255), ink=(20, 20, 20)):
    x0, y0, x1, y1 = d.textbbox((0, 0), text, font=f)
    w, h = x1 - x0, y1 - y0
    d.rounded_rectangle([xy[0] - w / 2 - 4, xy[1] - h / 2 - 3, xy[0] + w / 2 + 4, xy[1] + h / 2 + 4], 4, fill=fill, outline=ink)
    d.text((xy[0] - w / 2 - x0, xy[1] - h / 2 - y0), text, font=f, fill=ink)


def draw_plan(L):
    ppm, half, mx, my, legend = 5, 212, 120, 110, 880
    size = 2 * half * ppm
    img = Image.new('RGB', (mx + size + legend + 30, my + size + 70), (250, 248, 242))
    d = ImageDraw.Draw(img, 'RGBA')
    def P(x, z): return (mx + (x + half) * ppm, my + (half - z) * ppm)
    def poly(pts, **k): d.polygon([P(*q) for q in pts], **k)
    d.rectangle([P(-half, half), P(half, -half)], fill=(140, 176, 92))
    # forêt existante (moitié est + coin nord)
    poly(L['foret']['polygone'], fill=(96, 138, 78))
    rnd = random.Random(7)
    for _ in range(420):
        x, z = rnd.uniform(-196, 196), rnd.uniform(-196, 196)
        inside = (x > 22) or (x > -20 and z > 125) or (x > 0 and z < -125)
        if inside and all(math.hypot(x - c[0], z - c[1]) > c[2] + 3 for c in L['foret']['clairieres']):
            r = rnd.uniform(4, 6.5)
            d.ellipse([P(x - r, z + r), P(x + r, z - r)], fill=(62, 104, 62, 150))
    for cx, cz, cr in L['foret']['clairieres']:
        d.ellipse([P(cx - cr, cz + cr), P(cx + cr, cz - cr)], fill=(140, 176, 92))
    # chemins et place
    dirt, dirt2 = (222, 188, 128), (232, 208, 160)
    for s in L['chemins']:
        col = dirt2 if s['ajout_hors_concept'] else dirt
        pts = [P(*q) for q in s['points']]; w = int(s['largeur_m'] * ppm)
        d.line(pts, fill=col, width=w, joint='curve')
        for q in pts: d.ellipse([q[0] - w / 2, q[1] - w / 2, q[0] + w / 2, q[1] + w / 2], fill=col)
    poly(L['place']['polygone'], fill=dirt)
    # grille
    f12, f14, f16, f18, f22, f30 = font(13), font(15), font(17), font(19, True), font(24, True), font(34, True)
    for g in range(-200, 201, 10):
        a = 70 if g % 50 == 0 else 22
        d.line([P(g, -200), P(g, 200)], fill=(255, 255, 255, a), width=2 if g % 50 == 0 else 1)
        d.line([P(-200, g), P(200, g)], fill=(255, 255, 255, a), width=2 if g % 50 == 0 else 1)
    for g in range(-200, 201, 50):
        d.text((P(g, 0)[0] - 16, my + size + 8), '%+d' % g if g else '0', font=f16, fill=(60, 60, 60))
        d.text((mx - 58, P(0, g)[1] - 10), '%+d' % g if g else '0', font=f16, fill=(60, 60, 60))
    d.text((mx + size / 2 - 150, my + size + 34), 'X (m)  →  Est', font=f18, fill=(60, 60, 60))
    d.text((8, my + 6), 'Z (m)', font=f18, fill=(60, 60, 60)); d.text((8, my + 28), 'Nord en haut', font=f14, fill=(60, 60, 60))
    # murs, piliers, arches
    stone, gate = (128, 126, 122), (206, 150, 60)
    for s in (-200, 200):
        d.rectangle([P(-203.5, s + 3.5), P(203.5, s - 3.5)], fill=stone)
        d.rectangle([P(s - 3.5, 203.5), P(s + 3.5, -203.5)], fill=stone)
    for nm, (gx, gz) in L['meta']['portes'].items():
        if abs(gz) == 200: d.rectangle([P(gx - 13, gz + 5.5), P(gx + 13, gz - 5.5)], fill=gate)
        else: d.rectangle([P(gx - 5.5, gz + 13), P(gx + 5.5, gz - 13)], fill=gate)
    for a in range(-200, 201, 50):
        for b in (-200, 200):
            for (x, z) in ((a, b), (b, a)): d.rectangle([P(x - 6.4, z + 6.4), P(x + 6.4, z - 6.4)], fill=(96, 94, 92))
    for nm, (gx, gz) in L['meta']['portes'].items():
        off = {'Nord': (0, -22), 'Sud': (0, 22), 'Est': (-70, 0), 'Ouest': (72, 0)}[nm]
        q = P(gx, gz); tag(d, (q[0] + off[0], q[1] - off[1]), 'Porte ' + nm, f18, fill=(255, 236, 190))
    # clôtures
    for c in L['clotures']:
        pts = [P(*q) for q in c['points']]
        if c['style'] == 'blanc':
            d.line(pts, fill=(70, 60, 50), width=6); d.line(pts, fill=(250, 250, 245), width=3)
        else: d.line(pts, fill=(92, 58, 30), width=4)
        for q in pts: d.ellipse([q[0] - 4, q[1] - 4, q[0] + 4, q[1] + 4], fill=(60, 40, 22))
    # arbres héros
    for (x, z) in L['arbres_heros']:
        d.ellipse([P(x - 6, z + 6), P(x + 6, z - 6)], fill=(46, 96, 58, 215), outline=(20, 50, 30))
        d.ellipse([P(x - 1, z + 1), P(x + 1, z - 1)], fill=(90, 56, 30))
    # éléments et bâtiments
    def block(e, fill):
        c = [P(*q) for q in e['coins']]
        d.polygon(c, fill=fill, outline=(30, 24, 20)); d.line(c + [c[0]], fill=(30, 24, 20), width=3)
        n = (math.sin(math.radians(e['yaw_deg'])), math.cos(math.radians(e['yaw_deg'])))
        fx, fz = e['x'] + n[0] * e['profondeur_m'] / 2, e['z'] + n[1] * e['profondeur_m'] / 2
        a = (-n[1], n[0]); s = 2.4
        d.polygon([P(fx + n[0] * s * 1.8, fz + n[1] * s * 1.8), P(fx + a[0] * s, fz + a[1] * s), P(fx - a[0] * s, fz - a[1] * s)],
                  fill=(255, 255, 255), outline=(20, 20, 20))
    for e in L['elements']:
        key = next(k for k in ELT if e['type'].startswith(k))
        if e['type'] == 'Feu_De_Camp':
            d.ellipse([P(e['x'] - 3.5, e['z'] + 3.5), P(e['x'] + 3.5, e['z'] - 3.5)], fill=ELT['Feu'], outline=(120, 40, 10), width=3)
        else: block(e, ELT[key])
    for b in L['batiments']:
        block(b, ROOF[b['toit']])
        if b['type'].startswith('Maison') or b['type'] == 'Grange':        # faîtage
            n = np.array([math.sin(math.radians(b['yaw_deg'])), math.cos(math.radians(b['yaw_deg']))]); a = perp(n)
            ax, ln = (n, b['profondeur_m']) if b['pignon_en_facade'] else (a, b['largeur_facade_m'])
            c0 = np.array([b['x'], b['z']])
            d.line([P(*(c0 - ax * ln / 2)), P(*(c0 + ax * ln / 2))], fill=(255, 255, 255, 190), width=3)
    # accessoires et villageois
    for a in L['accessoires']:
        q = P(a['x'], a['z']); d.rectangle([q[0] - 5, q[1] - 5, q[0] + 5, q[1] + 5], fill=(120, 84, 48), outline=(30, 20, 10))
        d.text((q[0] + 7, q[1] - 9), a['id'], font=f12, fill=(30, 20, 10))
    for (x, z) in L['villageois']:
        q = P(x, z); d.ellipse([q[0] - 5, q[1] - 5, q[0] + 5, q[1] + 5], fill=(60, 70, 150), outline=(255, 255, 255))
    for e in L['elements'] + L['batiments']:
        q = P(e['x'], e['z']); tag(d, (q[0], q[1] - (16 if e['type'] == 'Feu_De_Camp' else 0)), e['id'], f18)
    for s in L['chemins']:
        m = s['points'][len(s['points']) // 2]; q = P(*m); tag(d, q, s['id'], f14, fill=(255, 244, 214), ink=(110, 70, 20))
    for c in L['clotures']:
        m = np.mean(np.array(c['points'][:2]), 0); q = P(*m); tag(d, (q[0], q[1] + 14), c['id'], f12, fill=(236, 220, 200), ink=(70, 40, 16))
    q = P(L['place']['centre'][0], L['place']['centre'][1] - 16); d.text((q[0] - 28, q[1]), 'PLACE', font=f18, fill=(120, 84, 40))
    d.text(P(70, 150), 'FORÊT EXISTANTE', font=f22, fill=(30, 66, 36)); d.text(P(70, 138), '(sapins P_Tree_Pine_01 à 05 du terrain)', font=f16, fill=(30, 66, 36))
    # titre, rose des vents, échelle
    d.text((mx, 22), 'Lobby — plan de placement vu de dessus', font=f30, fill=(30, 30, 30))
    d.text((mx, 64), "Relevé du concept art Lobby_Vue.jpg · centre du Lobby = (0, 0) · 1 carreau = 10 m, trait fort = 50 m · source chiffrée : ArtSource/Lobby/lobby_placement.json",
           font=f16, fill=(70, 70, 70))
    cx, cy = P(168, 168)
    d.polygon([(cx, cy - 46), (cx - 13, cy + 8), (cx, cy - 2), (cx + 13, cy + 8)], fill=(30, 30, 30)); d.text((cx - 9, cy - 76), 'N', font=f22, fill=(30, 30, 30))
    sx, sy = P(-190, -186); d.rectangle([sx, sy, sx + 50 * ppm, sy + 8], fill=(30, 30, 30)); d.text((sx, sy + 12), '50 m', font=f16, fill=(30, 30, 30))
    # légende
    lx, ly = mx + size + 30, my
    d.text((lx, ly), 'Légende', font=f22, fill=(30, 30, 30)); ly += 38
    for name, col in [('Toit rouge', ROOF['rouge']), ('Toit bleu', ROOF['bleu']), ('Toit jaune', ROOF['jaune']), ('Toit brun (bardeaux bois)', ROOF['brun']),
                      ('Grange (toit ardoise)', ROOF['gris ardoise']), ('Tente du médecin', ROOF['toile blanche']), ('Forge', ELT['Forge']), ('Potager', ROOF['terre']),
                      ('Étal', ELT['Etal']), ('Chemin de terre / place', dirt), ('Chemin ajouté (absent du concept)', dirt2),
                      ('Mur · pilier', stone), ('Arche de porte', gate)]:
        d.rectangle([lx, ly, lx + 30, ly + 18], fill=col, outline=(30, 30, 30)); d.text((lx + 40, ly), name, font=f16, fill=(30, 30, 30)); ly += 25
    d.text((lx, ly), 'Triangle blanc = côté de la porte / façade.   Rond bleu = villageois (indicatif).', font=f14, fill=(60, 60, 60)); ly += 22
    d.text((lx, ly), 'Rond vert foncé = sapin « héros » à placer à la main.   Carré brun = accessoire (P..).', font=f14, fill=(60, 60, 60)); ly += 34
    d.text((lx, ly), 'Bâtiments et éléments', font=f22, fill=(30, 30, 30)); ly += 34
    d.text((lx, ly), 'ID     x       z     yaw   L×P×H (m)     nom', font=f14, fill=(90, 90, 90)); ly += 22
    for e in L['batiments'] + L['elements']:
        line = '%-4s %6.0f %6.0f %5d°   %d×%d×%d' % (e['id'], e['x'], e['z'], e['yaw_deg'], e['largeur_facade_m'], e['profondeur_m'], e['hauteur_m'])
        d.text((lx, ly), e['id'], font=font(15, True), fill=(20, 20, 20))
        d.text((lx + 48, ly), '%5.0f' % e['x'], font=f14, fill=(20, 20, 20)); d.text((lx + 100, ly), '%5.0f' % e['z'], font=f14, fill=(20, 20, 20))
        d.text((lx + 152, ly), '%3d°' % e['yaw_deg'], font=f14, fill=(20, 20, 20))
        d.text((lx + 202, ly), '%d×%d×%d' % (e['largeur_facade_m'], e['profondeur_m'], e['hauteur_m']), font=f14, fill=(20, 20, 20))
        d.text((lx + 300, ly), e['nom'], font=f14, fill=(20, 20, 20)); ly += 21
    ly += 14; d.text((lx, ly), 'Chemins', font=f22, fill=(30, 30, 30)); ly += 32
    for s in L['chemins']:
        d.text((lx, ly), s['id'], font=font(15, True), fill=(20, 20, 20)); d.text((lx + 48, ly), '%s — %d m de large' % (s['nom'], s['largeur_m']), font=f14, fill=(20, 20, 20)); ly += 21
    ly += 14; d.text((lx, ly), 'Clôtures', font=f22, fill=(30, 30, 30)); ly += 32
    for c in L['clotures']:
        d.text((lx, ly), c['id'], font=font(15, True), fill=(20, 20, 20)); d.text((lx + 48, ly), '%s — %s, %d m' % (c['nom'], c['style'], c['longueur_m']), font=f14, fill=(20, 20, 20)); ly += 21
    ly += 14; d.text((lx, ly), 'Accessoires', font=f22, fill=(30, 30, 30)); ly += 32
    half_n = (len(L['accessoires']) + 1) // 2
    for i, a in enumerate(L['accessoires']):
        col, row = divmod(i, half_n)
        d.text((lx + col * 430, ly + row * 20), '%s  %s (%.0f, %.0f)' % (a['id'], a['type'].replace('_', ' '), a['x'], a['z']), font=f14, fill=(20, 20, 20))
    img.save(os.path.join(OUT, 'Lobby_Plan_Dessus.png'))


def draw_reperes(L):
    k = 2
    img = Image.open(SRC).convert('RGB'); img = img.resize((img.width * k, img.height * k), Image.LANCZOS)
    d = ImageDraw.Draw(img, 'RGBA'); f = font(26, True); fs = font(18, True)
    def PX(x, z, y=0.0): q = project([x, y, z])[0]; return (q[0] * k, q[1] * k)
    for s in L['chemins']:
        d.line([PX(*q) for q in s['points']], fill=(255, 240, 150, 150), width=4)
    for c in L['clotures']:
        d.line([PX(*q) for q in c['points']], fill=(255, 255, 255, 200), width=3)
    for e in L['batiments'] + L['elements']:
        c = [PX(*q) for q in e['coins']]; d.line(c + [c[0]], fill=(0, 255, 255, 230), width=3)
        top = [PX(q[0], q[1], e['hauteur_m']) for q in e['coins']]
        if e['hauteur_m'] > 3: d.line(top + [top[0]], fill=(0, 255, 255, 90), width=2)
    for e in L['batiments'] + L['elements']:
        tag(d, PX(e['x'], e['z'], e['hauteur_m'] * 0.55), e['id'], f, fill=(255, 255, 255, 235))
    for a in L['accessoires']:
        q = PX(a['x'], a['z']); d.ellipse([q[0] - 5, q[1] - 5, q[0] + 5, q[1] + 5], fill=(255, 220, 0), outline=(0, 0, 0))
        tag(d, (q[0], q[1] - 20), a['id'], fs, fill=(255, 235, 120, 235))
    for s in L['chemins']:
        m = s['points'][len(s['points']) // 2]; tag(d, PX(*m), s['id'], fs, fill=(255, 244, 214, 235), ink=(110, 70, 20))
    for nm, (gx, gz) in L['meta']['portes'].items():
        q = PX(gx * 0.98, gz * 0.98, 20)
        if 0 < q[0] < img.width and 0 < q[1] < img.height: tag(d, q, 'Porte ' + nm, f, fill=(255, 210, 120, 240))
    img.save(os.path.join(OUT, 'Lobby_Vue_Reperes.jpg'), quality=88)


def draw_rectified():
    im = np.asarray(Image.open(SRC).convert('RGB')).astype(np.float32)
    ppm, half = 4, 210; n = 2 * half * ppm
    xs = (np.arange(n) + 0.5) / ppm - half; zs = half - (np.arange(n) + 0.5) / ppm
    X, Z = np.meshgrid(xs, zs)
    uv = project(np.stack([X, np.zeros_like(X), Z], -1).reshape(-1, 3)).reshape(n, n, 2)
    u, v = uv[..., 0], uv[..., 1]
    ok = (u >= 0) & (u < im.shape[1] - 1) & (v >= 0) & (v < im.shape[0] - 1)
    u0 = np.clip(np.floor(u).astype(int), 0, im.shape[1] - 2); v0 = np.clip(np.floor(v).astype(int), 0, im.shape[0] - 2)
    fu, fv = (u - u0)[..., None], (v - v0)[..., None]
    out = im[v0, u0] * (1 - fu) * (1 - fv) + im[v0, u0 + 1] * fu * (1 - fv) + im[v0 + 1, u0] * (1 - fu) * fv + im[v0 + 1, u0 + 1] * fu * fv
    out[~ok] = (40, 40, 40)
    img = Image.fromarray(out.astype(np.uint8)); d = ImageDraw.Draw(img); f = font(15, True)
    def P(x, z): return ((x + half) * ppm, (half - z) * ppm)
    for g in range(-200, 201, 50):
        d.line([P(g, -200), P(g, 200)], fill=(255, 255, 255), width=1); d.line([P(-200, g), P(200, g)], fill=(255, 255, 255), width=1)
        d.text((P(g, -200)[0] + 3, P(g, -200)[1] + 4), str(g), font=f, fill=(255, 255, 0)); d.text((4, P(-200, g)[1] + 3), str(g), font=f, fill=(255, 255, 0))
    img.save(os.path.join(OUT, 'Lobby_Vue_Redressee.jpg'), quality=90)


def draw_mask(L):
    n = 1024; img = Image.new('L', (n, n), 0); d = ImageDraw.Draw(img)
    def P(x, z): return ((x + 200) / 400 * n, (200 - z) / 400 * n)
    for s in L['chemins']:
        pts = [P(*q) for q in s['points']]; w = int(s['largeur_m'] / 400 * n)
        d.line(pts, fill=255, width=w, joint='curve')
        for q in pts: d.ellipse([q[0] - w / 2, q[1] - w / 2, q[0] + w / 2, q[1] + w / 2], fill=255)
    for zone in L['zones_terre']:
        d.polygon([P(*q) for q in zone['polygone']], fill=255)
    img.save(os.path.join(OUT, 'Lobby_Chemins_Masque.png'))


def make_crops():
    im = Image.open(SRC).convert('RGB')
    for name, box, k in D.CROPS:
        c = im.crop(box); c = c.resize((c.width * k, c.height * k), Image.LANCZOS)
        c.save(os.path.join(OUT, 'Refs', name + '.jpg'), quality=93)


if __name__ == '__main__':
    L = convert()
    json.dump(L, open(os.path.join(HERE, 'lobby_placement.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    draw_plan(L); draw_reperes(L); draw_rectified(); draw_mask(L); make_crops()
    for e in L['batiments'] + L['elements']:
        print('%-4s %-10s x=%7.1f z=%7.1f yaw=%3d  %2d x %2d x %2d' % (e['id'], e['type'], e['x'], e['z'], e['yaw_deg'], e['largeur_facade_m'], e['profondeur_m'], e['hauteur_m']))
