# Disposition de l'atlas partagé des objets (T_Items_Atlas, 1024²), commune au générateur de texture
# (gen_items_atlas.py, Python + numpy + Pillow) et aux scripts Blender (items_lib.py). Aucun import de bpy ici.
#
# Moitié haute (v 0,5 -> 1) : 8 BANDES de 64 px, fil / motif le long de u (raccord en u : u peut dépasser 1).
#   Dans chaque bande, t = 0 -> 0,78 : matière ; t = 0,82 -> 1 : « arête usée » (plus claire) pour les bords biseautés.
# Moitié basse (v 0 -> 0,5) : grille de 4 rangées × 8 CASES de 128 px (aplats, motifs, faces peintes).
#
# Pour ajouter une matière : prendre une case libre de CELLS (None), la peindre dans gen_items_atlas.py.

ATLAS_SIZE = 1024
BAND_PX = 64
CELL_PX = 128

BANDS = [
    "wood_honey",    # 0 bois miel (manches de pelle, fourche)
    "bark",          # 1 écorce brune (bâton)
    "wood_chest",    # 2 planches du coffre (bois orangé, fil marqué)
    "iron_blue",     # 3 fer gris-bleu (lame de la pelle)
    "iron_dark",     # 4 fer sombre (ferrures du coffre, douille)
    "steel_light",   # 5 métal clair (dents de la fourche)
    "leather",       # 6 cuir brun (bourse)
    "string_red",    # 7 ficelle rouge enroulée en croisillons (poignée du bâton)
]

CELLS = [
    # rangée 0
    ["gold", "brass", "cloth_red", "rope", "black", "endgrain", "endgrain_bark", "coin_paw"],
    # rangée 1 (aplats prévus pour les planches suivantes)
    ["tomato", "leaf", "canvas", "cardboard", "glass", "chalk", "mustard", "lock_face"],
    # rangées 2 et 3 : libres
    [None] * 8,
    [None] * 8,
]

EDGE_T0 = 0.82     # début de la zone « arête usée » dans une bande


def band_rect(name):
    """(u0, v0, u1, v1) de la bande, v vers le haut comme dans Blender."""
    k = BANDS.index(name)
    v1 = 1.0 - k * BAND_PX / ATLAS_SIZE
    return 0.0, v1 - BAND_PX / ATLAS_SIZE, 1.0, v1


def cell_rect(name):
    for r, row in enumerate(CELLS):
        if name in row:
            c = row.index(name)
            u0 = c * CELL_PX / ATLAS_SIZE
            v1 = 0.5 - r * CELL_PX / ATLAS_SIZE
            return u0, v1 - CELL_PX / ATLAS_SIZE, u0 + CELL_PX / ATLAS_SIZE, v1
    raise KeyError(name)


def band_px(name):
    """(ligne_haut, ligne_bas) en pixels image (ligne 0 = haut de l'image)."""
    k = BANDS.index(name)
    return k * BAND_PX, (k + 1) * BAND_PX


def cell_px(name):
    for r, row in enumerate(CELLS):
        if name in row:
            c = row.index(name)
            y0 = ATLAS_SIZE // 2 + r * CELL_PX
            return y0, c * CELL_PX
    raise KeyError(name)
