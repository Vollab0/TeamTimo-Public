# Extrait les contours des lettres N, E, S, W (police serif de Windows) pour les roses des vents des arches.
# Exécuter dans Blender (n'importe quel .blend) :
#   exec(open(r"<dépôt>\ArtSource\Murs\gen_compass_letters.py", encoding="utf-8").read(), {})
# Sortie : compass_letters.json (lu par gen_gate_textures.py) — segments de contour, hauteur de capitale = 1,
# lettre centrée sur sa boîte englobante. Le remplissage se fait par la règle pair-impair.
import bpy, bmesh, json, os

HERE = os.path.dirname(os.path.abspath(globals().get("__file__", bpy.data.filepath)))
FONT = r"C:\Windows\Fonts\palab.ttf"     # Palatino Linotype Bold (capitales romaines)
OUT = os.path.join(r"C:\Users\gabin\Documents\Projets Unity\TeamTimo\ArtSource\Murs", "compass_letters.json")

font = bpy.data.fonts.load(FONT, check_existing=True)
letters = {}
for ch in "NESW":
    cu = bpy.data.curves.new("tmp_letter", 'FONT')
    cu.body = ch; cu.font = font; cu.resolution_u = 8; cu.fill_mode = 'BOTH'
    ob = bpy.data.objects.new("tmp_letter", cu)
    bpy.context.scene.collection.objects.link(ob)
    dg = bpy.context.evaluated_depsgraph_get()
    me = ob.evaluated_get(dg).to_mesh()
    bm = bmesh.new(); bm.from_mesh(me)
    segs = [[e.verts[0].co.x, e.verts[0].co.y, e.verts[1].co.x, e.verts[1].co.y] for e in bm.edges if len(e.link_faces) == 1]
    bm.free(); ob.evaluated_get(dg).to_mesh_clear()
    bpy.data.objects.remove(ob); bpy.data.curves.remove(cu)
    xs = [s[0] for s in segs] + [s[2] for s in segs]; ys = [s[1] for s in segs] + [s[3] for s in segs]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    h = max(ys) - min(ys)
    letters[ch] = [[round((s[0] - cx) / h, 5), round((s[1] - cy) / h, 5), round((s[2] - cx) / h, 5), round((s[3] - cy) / h, 5)] for s in segs]
bpy.data.fonts.remove(font)
with open(OUT, "w") as f:
    json.dump({"font": os.path.basename(FONT), "letters": letters}, f)
RESULT = {ch: len(v) for ch, v in letters.items()}
