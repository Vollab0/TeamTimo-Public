# -*- coding: utf-8 -*-
"""Relevé du concept art du Lobby (GameDesign/99_Attachments/Brainstorming/Lobby_Vue.jpg).

Tous les points sont notés en PIXELS de l'image d'origine (2000 x 1116) : c'est la source de vérité.
lobby_layout.py les convertit en mètres (repère du jeu : centre du Lobby = origine, +X = Est, +Z = Nord)
grâce à la caméra calibrée par calibrate_concept_camera.py.
Un point ('w', x, z) est déjà en mètres (parties cachées dans l'image, ex. derrière un mur).
"""

# --- Bâtiments : coins de BASE (au sol) G = gauche, P = le plus proche de la caméra, D = droite.
# porte : arête qui porte la porte ('GP' ou 'PD'). faitage : pixel du milieu du faîtage (pour la hauteur).
BUILDINGS = [
    dict(id='G1', nom='Grange', type='Grange', toit='gris ardoise', options=[],
         G=(920, 323), P=None, D=(1003, 318), profondeur=40, faitage=(960, 225),
         note="Façade rouge à croisillons blancs, toit à la Mansart ; point d'apparition des joueurs."),
    dict(id='M01', nom="Maison rouge de l'enclos", type='Maison_S', toit='rouge', options=['cheminee'],
         G=(736.7, 500), P=(776.7, 503), D=(788, 483), porte='GP', faitage=(773, 443)),
    dict(id='M02', nom='Maison brune nord-ouest', type='Maison_S', toit='brun', options=['cheminee'],
         G=(558, 587), P=(595, 605), D=(638, 583), porte='PD', faitage=(587, 523)),
    dict(id='M03', nom='Grande maison bleue', type='Maison_L', toit='bleu', options=['perron', 'caisses'],
         G=(721.7, 586.7), P=(781.7, 616.7), D=(816.7, 586.7), porte='GP', faitage=(773, 540)),
    dict(id='M04', nom='Maison bleue de la place', type='Maison_S', toit='bleu', options=[],
         G=(826, 617), P=(860, 648), D=(885, 630), porte='PD', faitage=(863, 563)),
    dict(id='M05', nom="Maison bleue de l'ouest", type='Maison_S', toit='bleu', options=['cloture'],
         G=(505, 705), P=(548, 727), D=(587, 700), porte='PD', faitage=(528, 638)),
    dict(id='M06', nom='Petite maison jaune', type='Maison_S', toit='jaune', options=['cheminee', 'tas_de_bois'],
         G=(623, 767), P=(663, 808), D=(702, 787), porte='PD', faitage=(654, 717)),
    dict(id='M07', nom='Maison rouge du mur ouest', type='Maison_S', toit='rouge', options=['auvent'],
         G=(742, 800), P=(790, 840), D=(850, 812), porte='PD', faitage=(800, 745),
         note="Aux trois quarts cachée par le mur dans le concept : position approximative."),
    dict(id='M08', nom='Maison rouge à lucarne', type='Maison_M', toit='rouge', options=['lucarne', 'cheminee', 'appentis'],
         G=(998, 660), P=(1050, 698), D=(1098, 688), porte='PD', faitage=(1053, 603)),
    dict(id='M09', nom='Maison rouge à auvent', type='Maison_M', toit='rouge', options=['lucarne', 'cheminee', 'auvent_toile'],
         G=(1108, 760), P=(1165, 793), D=(1213, 773), porte='GP', faitage=(1160, 692)),
    dict(id='M10', nom='Grande maison jaune', type='Maison_L', toit='jaune', options=['cheminee', 'cloture'],
         G=(1208, 683), P=(1250, 703), D=(1330, 660), porte='GP', faitage=(1285, 590)),
    dict(id='M11', nom='Maison bleue en croix', type='Maison_S', toit='bleu', options=['pignon_croise'],
         G=(1413, 747), P=(1453, 780), D=(1482, 757), porte='GP', faitage=(1457, 690)),
    dict(id='M12', nom='Maison brune sud', type='Maison_M', toit='brun', options=['cheminee', 'cloture'],
         G=(1342, 830), P=(1392, 877), D=(1433, 833), porte='GP', faitage=(1398, 770)),
    dict(id='F1', nom='Grand four de forge', type='Forge_Four', toit='tole bleue', options=['gueule_de_four'],
         G=(397, 412), P=(428, 428), D=(480, 410), porte='PD', faitage=(423, 357)),
    dict(id='J1', nom='Potager (2 planches de terre)', type='Potager', toit='terre', options=['cloture', 'panonceau'],
         G=(635, 640), P=(727, 693), D=(783, 658), porte='PD', faitage=None),
    dict(id='T1', nom='Tente du médecin', type='Tente_Medecin', toit='toile blanche', options=['croix_rouge'],
         G=(330, 560), P=(403, 575), D=(468, 540), porte='PD', faitage=(384, 485)),
]

# --- Éléments posés par leur centre (pixel au sol), taille en mètres (largeur de façade, profondeur, hauteur).
# face : 'feu' = tourné vers le feu de camp ; sinon pixel vers lequel la façade regarde ; None = sans orientation.
CENTERED = [
    dict(id='F2', nom='Petit fourneau à cheminée', type='Forge_Fourneau', px=(527, 387), taille=(16, 18, 17), face=(550, 410)),
    dict(id='F3', nom='Plancher + enclume', type='Forge_Plancher', px=(480, 432), taille=(16, 14, 1), face=(500, 460)),
    dict(id='E1', nom='Étal double (2 travées)', type='Etal_Double', px=(937, 452), taille=(22, 8, 10), face='feu'),
    dict(id='E2', nom='Étal appentis', type='Etal_Appentis', px=(880, 490), taille=(12, 10, 9), face='feu'),
    dict(id='E3', nom='Étal à étagères', type='Etal_Simple', px=(1122, 465), taille=(11, 9, 10), face='feu'),
    dict(id='E4', nom='Étal aux caisses', type='Etal_Simple', px=(1073, 550), taille=(12, 10, 10), face='feu'),
    dict(id='E5', nom='Étal à auvent rayé', type='Etal_Raye', px=(978, 745), taille=(13, 6, 9), face=(985, 790)),
    dict(id='C1', nom='Feu de camp', type='Feu_De_Camp', px=(1015, 498), taille=(5, 5, 2), face=None),
]

# --- Clôtures (polylignes au sol). style : 'bois' (brun) ou 'blanc' (forge).
FENCES = [
    dict(id='K1', nom='Cour de la forge (ouest)', style='blanc', pts=[('w', -104, 196), (323, 430), (413, 467), (448, 455)]),
    dict(id='K2', nom='Cour de la forge (est)', style='blanc', pts=[(545, 428), (593, 403), ('w', -42, 196)]),
    dict(id='K3', nom='Enclos des bêtes', style='bois', pts=[(713, 473), (627, 497), (640, 537), (713, 540), (793, 523), (805, 480)]),
    dict(id='K4', nom='Potager', style='bois', pts=[(635, 640), (688, 610), (783, 658), (727, 693), (635, 640)]),
    dict(id='K5', nom='Jardin de la maison bleue ouest', style='bois', pts=[(530, 747), (617, 700), (590, 672)]),
    dict(id='K6', nom='Jardin de la maison rouge à auvent', style='bois', pts=[(1073, 757), (1113, 717)]),
    dict(id='K7', nom='Entre M09 et M10', style='bois', pts=[(1200, 710), (1250, 723)]),
    dict(id='K8', nom='Jardin de la grande maison jaune', style='bois', pts=[(1278, 773), (1360, 687), (1333, 650)]),
    dict(id='K9', nom='Bord du chemin sud (1)', style='bois', pts=[(1300, 753), (1390, 677)]),
    dict(id='K10', nom='Bord du chemin sud (2)', style='bois', pts=[(1417, 657), (1482, 595)]),
    dict(id='K11', nom='Jardin de la maison brune sud', style='bois', pts=[(1397, 877), (1438, 827)]),
]

# --- Chemins de terre (polylignes au sol, largeur en mètres).
PATHS = [
    dict(id='S1', nom='Place → Grange', largeur=13, pts=[(1000, 470), (1010, 440), (1000, 400), (975, 370), (962, 335)]),
    dict(id='S2', nom='Place → Forge → porte Nord', largeur=10,
         pts=[(860, 470), (800, 468), (720, 455), (660, 425), (630, 395), (615, 357), (605, 330), ('w', 25, 196)]),
    dict(id='S3', nom='Entrée de la forge', largeur=8, pts=[(630, 400), (595, 412)]),
    dict(id='S4', nom='Anneau ouest (forge → tente → maisons)', largeur=9,
         pts=[(625, 400), (585, 455), (530, 490), (500, 520), (497, 555), (510, 590), (550, 620), (595, 648),
              (673, 700), (727, 730), (793, 750), (900, 797)]),
    dict(id='S5', nom='Anneau sud', largeur=9,
         pts=[(900, 797), (1020, 823), (1120, 823), (1203, 813), (1270, 793), (1320, 760), (1360, 737),
              (1410, 697), ('w', -58.0, -169.5), ('w', -43.0, -174.8), ('w', -25.0, -180.0)]),
    dict(id='S6', nom='Place → porte Sud', largeur=11,
         pts=[('w', -45.0, -72.0), ('w', -47.5, -83.0), ('w', -48.0, -96.0), ('w', -48.5, -112.0),
              (1327, 570), (1400, 562), ('w', -24.5, -166.5), ('w', -25.0, -176.5),
              ('w', -25.0, -186.5), ('w', -25.0, -198.0)]),
    dict(id='S7', nom='Anneau ouest → porte Ouest', largeur=9, pts=[(673, 700), (620, 737), (570, 760), ('w', -196, 25)]),
    dict(id='S8', nom='Grange → porte Est (AJOUT, absent du concept)', largeur=9, ajout=True,
         pts=[('w', 100, 22), ('w', 130, -5), ('w', 165, -22), ('w', 196, -25)]),
]
PLAZA = [(845, 475), (850, 510), (848, 547), (920, 577), (1007, 588), (1060, 575), (1113, 557), (1160, 540),
         (1150, 490), (1100, 440), (1050, 412), (1000, 405), (980, 450), (895, 470)]

# --- Petits accessoires (pixel au sol).
PROPS = [
    ('Tonneaux_Couches', (403, 427), 'Tas de tonneaux couchés (forge)'),
    ('Ratelier', (383, 407), "Râtelier d'outils (forge)"),
    ('Enclume', (490, 422), 'Enclume / coffre de forge sur le plancher'),
    ('Treteau', (448, 467), "Tréteau à l'entrée de la cour de la forge"),
    ('Panneau', (563, 420), 'Panneau indicateur (forge)'),
    ('Potence_Lanterne', (625, 613), 'Potence à lanterne (coin du potager)'),
    ('Caisse', (707, 597), 'Caisses contre la grande maison bleue'),
    ('Caisse', (737, 607), 'Caisse / perron de la grande maison bleue'),
    ('Auge', ('w', -82.0, 40.5), "Auge / mangeoire de l'enclos"),
    ('Tas_De_Bois', (613, 783), 'Tas de bois contre la petite maison jaune'),
    ('Caisse', (1015, 575), 'Caisse au bord de la place'),
    ('Tonneau', (980, 675), 'Tonneau près de la maison rouge à lucarne'),
    ('Caisse', (1030, 688), "Caisse sous l'appentis de la maison rouge"),
    ('Caisse', ('w', -140.5, -77.0), "Caisse près de l'étal rayé"),
    ('Souche', ('w', -142.5, -72.5), 'Souche / billot'),
    ('Souche', ('w', -140.0, -67.0), 'Souche / billot'),
    ('Brouette', (1023, 757), "Caisse à outils / brouette près de l'étal rayé"),
    ('Caisse', (1113, 777), 'Caisse devant la maison rouge à auvent'),
    ('Panier', (1177, 783), "Paniers sous l'auvent de toile"),
    ('Souche', ('w', -116.5, -131.5), 'Souche près du chemin sud'),
    ('Rondin', ('w', -43.5, -183.0), 'Rondin (pas japonais) 1'),
    ('Rondin', ('w', -51.5, -180.0), 'Rondin 2'),
    ('Rondin', ('w', -59.5, -177.0), 'Rondin 3'),
    ('Rondin', ('w', -67.5, -174.0), 'Rondin 4'),
    ('Plate_Bande', ('w', -59.0, -188.0), 'Plate-bande de terre retournée'),
]

# --- Villageois visibles (figurants, hors périmètre décor : positions indicatives).
VILLAGERS = [(503, 560), (613, 353), (655, 687), (730, 727), (880, 797), (1278, 807), (1433, 668), (1290, 567)]

# --- Arbres « héros » dans le village (pied du tronc). Le reste = forêt existante du terrain.
HERO_TREES = [(927, 603), (1153, 563), (580, 375), (550, 533), (725, 427), (852, 447), (1272, 768),
              (822, 730), (1533, 773), (1000, 850), (1075, 880), (1190, 880), (1290, 950)]

# --- Recadrages de référence pour Gemini : (fichier, boîte (x0, y0, x1, y1) en pixels, agrandissement).
CROPS = [
    ('Ref_Lobby_01_Grange', (860, 165, 1060, 350), 5),
    ('Ref_Lobby_02_Maison_Rouge_Lucarne', (975, 580, 1125, 715), 6),
    ('Ref_Lobby_03_Maison_Rouge_Auvent', (1075, 665, 1235, 805), 6),
    ('Ref_Lobby_04_Maison_Bleue_Grande', (690, 505, 835, 635), 6),
    ('Ref_Lobby_05_Maison_Jaune_Grande', (1185, 555, 1375, 715), 5),
    ('Ref_Lobby_06_Maison_Jaune_Petite', (595, 685, 720, 815), 6),
    ('Ref_Lobby_07_Maison_Brune', (540, 500, 660, 620), 6),
    ('Ref_Lobby_08_Maison_Rouge_Haute', (725, 410, 815, 515), 8),
    ('Ref_Lobby_09_Maison_Bleue_Croix', (1395, 665, 1505, 795), 7),
    ('Ref_Lobby_10_Forge', (300, 325, 620, 490), 4),
    ('Ref_Lobby_11_Tente_Medecin', (305, 460, 490, 600), 5),
    ('Ref_Lobby_12_Etals_Place', (835, 400, 1175, 600), 4),
    ('Ref_Lobby_13_Etal_Raye', (935, 685, 1045, 785), 8),
    ('Ref_Lobby_14_Feu_De_Camp', (975, 465, 1055, 525), 10),
    ('Ref_Lobby_15_Enclos', (615, 455, 815, 550), 5),
    ('Ref_Lobby_16_Potager_Lanterne', (610, 595, 800, 705), 5),
    ('Ref_Lobby_17_Clotures_Chemin', (1180, 640, 1500, 800), 4),
    ('Ref_Lobby_18_Accessoires_Sud', (850, 730, 1050, 800), 6),
    ('Ref_Lobby_19_Rondins', (1460, 600, 1590, 710), 7),
    ('Ref_Lobby_20_Village_Ensemble', (480, 400, 1500, 900), 2),
]
