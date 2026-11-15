# Etape 1 : formes du taureau en champs de distance fusionnes -> maillages bruts tres denses (objets Bull_Raw_*).
# A lancer dans Blender :  VOXEL = 0.012 ; exec(open(r"...\bull_01_shape.py", encoding="utf-8").read())
# Les maillages bruts sont ensuite allege, depliees et peints par bull_02_mesh.py.
import bpy
import time
import numpy as np

COMMON = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo\ArtSource\Taureau\bull_common.py"
exec(open(COMMON, encoding="utf-8").read())

VOXEL = globals().get("VOXEL", 0.012)
t0 = time.time()


def sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


# ======================================================================================
# CORPS (une seule masse lisse : tronc, bras, jambes, tete, museau, oreilles)
# ======================================================================================
B = Field((-1.36, -0.62, -0.02), (1.36, 0.50, 2.06), VOXEL)

# --- Tronc : colonne qui s'affine des epaules vers le bassin (silhouette "tronc d'arbre" de la reference)
B.add(squash(cone((0, 0.0, 1.20), (0, 0.0, 0.78), 0.49, 0.375), (0, 0, 0.95), (1, 0.64, 1)))
B.add(ell((0, -0.10, 0.93), (0.31, 0.24, 0.32)), k=0.12)                                # ventre
B.add(ell((0.30, 0.12, 1.15), (0.22, 0.19, 0.30), rot=(0, -10, 0)), k=0.10, mirror=True)  # dorsaux
B.add(ell((0.20, -0.20, 1.25), (0.25, 0.15, 0.19), rot=(0, 10, 0)), k=0.05, mirror=True)  # pectoraux
B.add(cone((0, 0.06, 1.42), (0, -0.02, 1.72), 0.27, 0.24), k=0.08)                      # cou
B.add(cone((0.20, 0.03, 1.45), (0.55, 0.02, 1.34), 0.16, 0.15), k=0.10, mirror=True)    # trapezes
B.add(ell((0.17, 0.12, 0.62), (0.18, 0.16, 0.18)), k=0.13, mirror=True)                 # fessiers

# --- Bras (pose en A a ~30 degres sous l'horizontale) : tres epais a l'epaule, fins au poignet
SH, EL, WR = (0.57, 0.0, 1.25), (0.80, 0.01, 1.125), (0.985, -0.01, 1.01)
B.add(ell((0.56, 0.0, 1.27), (0.24, 0.21, 0.23), rot=(0, 20, 0)), k=0.09, mirror=True)      # deltoide
B.add(cone(SH, EL, 0.205, 0.150), k=0.06, mirror=True)                                      # bras
B.add(ell((0.69, -0.07, 1.22), (0.13, 0.10, 0.12), rot=(0, 30, 0)), k=0.05, mirror=True)    # biceps
B.add(cone(EL, WR, 0.150, 0.096), k=0.03, mirror=True)                                      # avant-bras
B.add(ell((1.118, -0.015, 0.925), (0.165, 0.082, 0.112), rot=(0, 30, 0)), k=0.03, mirror=True)  # main (moufle)
B.add(cone((1.0, -0.05, 0.94), (0.992, -0.075, 0.835), 0.054, 0.048), k=0.02, mirror=True)    # pouce

# --- Jambes
HIP, KN, AN = (0.205, 0.01, 0.55), (0.21, -0.01, 0.36), (0.20, 0.01, 0.17)
B.add(cone(HIP, KN, 0.155, 0.143), k=0.05, mirror=True)                                 # cuisse
B.add(cone(KN, AN, 0.143, 0.108), k=0.04, mirror=True)                                  # mollet

# --- Tete : boite arrondie, plus etroite en haut (trapeze de la reference)
def head_taper(lz):
    return 1.0 - 0.26 * sstep(0.03, 0.25, lz) - 0.10 * sstep(-0.05, -0.25, lz)

B.add(rbox((0, -0.10, 1.73), (0.30, 0.22, 0.25), 0.12, taper=head_taper), k=0.05)
B.add(ell((0, -0.08, 1.80), (0.245, 0.215, 0.185)), k=0.06)                              # crane
B.add(ell((0.16, -0.290, 1.810), (0.13, 0.05, 0.040), rot=(0, -8, 0)), k=0.04, mirror=True)  # arcade sourciliere
B.add(rbox(MUZZLE_C, MUZZLE_HALF, MUZZLE_RAD), k=0.018)                                      # museau
# toupet entre les cornes : quelques meches bombees qui retombent un peu vers l'avant (reference)
B.add(ell((0.0, -0.12, 1.978), (0.080, 0.062, 0.046)), k=0.035)
B.add(ell((0.068, -0.11, 1.962), (0.056, 0.050, 0.040), rot=(0, -18, 0)), k=0.03, mirror=True)
B.add(ell((0.0, -0.19, 1.952), (0.052, 0.045, 0.036), rot=(-25, 0, 0)), k=0.03)
# narines et bouche : legers creux (peints en brun a l'etape texture)
B.cut(ell((0.105, -0.505, 1.535), (0.034, 0.030, 0.030)), k=0.012, mirror=True)
B.cut(cone((0.0, -0.489, 1.390), (0.088, -0.478, 1.376), 0.011, 0.009), k=0.008, mirror=True)

pts, quads = B.to_arrays()
raw_body = mesh_from_arrays("Bull_Raw_Body", pts, quads)

# ======================================================================================
# SABOT (cote gauche ; le droit est obtenu par symetrie a l'etape 2)
# ======================================================================================
def hoof_taper(lz):
    return 1.0 + 0.28 * sstep(0.088, -0.088, lz)          # plus large en bas

def cleft_taper(lz):
    return np.clip(1.0 - (lz + 0.09) / 0.17, 0.04, 1.0)   # fente en V inverse

Hf = Field((0.02, -0.30, -0.02), (0.42, 0.20, 0.21), VOXEL * 0.7)
Hf.add(rbox((HOOF_X, -0.04, HOOF_TOP / 2), (0.126, 0.175, HOOF_TOP / 2), 0.028, taper=hoof_taper))
Hf.cut(rbox((HOOF_X, -0.23, 0.05), (0.046, 0.085, 0.09), 0.008, taper=cleft_taper), k=0.006)
pts, quads = Hf.to_arrays()
raw_hoof = mesh_from_arrays("Bull_Raw_Hoof", pts, quads)

# ======================================================================================
# OREILLE (cote gauche) : feuille plate et pointue, creusee devant
# ======================================================================================
ear_mid = EAR_ROOT + (EAR_TIP - EAR_ROOT) * 0.40
ear_c = (EAR_ROOT + EAR_TIP) / 2
Ef = Field((0.20, -0.16, 1.66), (0.55, 0.04, 1.90), VOXEL * 0.5)
Ef.add(squash(cone(EAR_ROOT, ear_mid, 0.044, 0.066), ear_c, (1, 0.42, 1)))
Ef.add(squash(cone(ear_mid, EAR_TIP, 0.066, 0.010), ear_c, (1, 0.42, 1)), k=0.02)
Ef.cut(squash(cone(EAR_ROOT + np.array((0.07, -0.034, 0.004)), EAR_TIP + np.array((-0.045, -0.030, -0.003)), 0.046, 0.010),
              ear_c + np.array((0, -0.034, 0)), (1, 0.55, 1)), k=0.012)
pts, quads = Ef.to_arrays()
raw_ear = mesh_from_arrays("Bull_Raw_Ear", pts, quads)

# ======================================================================================
# CORNE (cote gauche)
# ======================================================================================
Hn = Field((0.08, -0.22, 1.78), (0.48, 0.04, 2.24), VOXEL * 0.6)
hp, hr = spline(HORN_PTS, HORN_RAD, 5)
for i in range(len(hp) - 1):
    Hn.add(cone(hp[i], hp[i + 1], hr[i], hr[i + 1]), k=0.02 if i else 0.0)
pts, quads = Hn.to_arrays()
raw_horn = mesh_from_arrays("Bull_Raw_Horn", pts, quads)

result = {
    "voxel": VOXEL,
    "seconds": round(time.time() - t0, 2),
    "body_quads": len(raw_body.data.polygons),
    "hoof_quads": len(raw_hoof.data.polygons),
    "horn_quads": len(raw_horn.data.polygons),
    "ear_quads": len(raw_ear.data.polygons),
    "body_bbox": [[round(float(v), 3) for v in np.min(np.array([v.co for v in raw_body.data.vertices]), axis=0)],
                  [round(float(v), 3) for v in np.max(np.array([v.co for v in raw_body.data.vertices]), axis=0)]],
}
