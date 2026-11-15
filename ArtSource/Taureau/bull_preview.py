# Rendus de controle du taureau : plusieurs vues assemblees en une planche. Ne sert pas a l'export.
# Variables attendues avant exec :
#   OUT   : chemin du PNG de la planche
#   VIEWS : liste de vues, chacune = (azimut en degres depuis -Y, elevation, mode[, image]) avec mode "ortho" ou "persp"
#   FRAME : image a afficher (ou None) ; TILE : taille d'une vignette en pixels (defaut 560)
#   TARGET / SPAN : point vise et hauteur cadree (defauts : (0, 0, 1.1) et 2.5 m)
import bpy
import math
import os
import numpy as np
from mathutils import Vector

TILE = globals().get("TILE", 560)
TARGET = Vector(globals().get("TARGET", (0.0, 0.0, 1.1)))
SPAN = globals().get("SPAN", 2.5)
FRAME = globals().get("FRAME", None)

scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = TILE
scene.render.resolution_y = TILE
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
scene.view_settings.view_transform = 'Standard'

pcol = bpy.data.collections.get("Preview")
if pcol is None:
    pcol = bpy.data.collections.new("Preview")
    scene.collection.children.link(pcol)


def get_obj(name, data_factory):
    ob = bpy.data.objects.get(name)
    if ob is None:
        ob = bpy.data.objects.new(name, data_factory())
        pcol.objects.link(ob)
    return ob


cam = get_obj("Preview_Cam", lambda: bpy.data.cameras.new("Preview_Cam"))
scene.camera = cam
sun = get_obj("Preview_Sun", lambda: bpy.data.lights.new("Preview_Sun", 'SUN'))
sun.data.energy = 2.4
sun.data.angle = math.radians(12)
fill = get_obj("Preview_Fill", lambda: bpy.data.lights.new("Preview_Fill", 'SUN'))
fill.data.energy = 0.7
for nm in ("Light", "Camera", "Cube"):
    o = bpy.data.objects.get(nm)
    if o:
        o.hide_render = True

world = scene.world or bpy.data.worlds.new("World")
scene.world = world
bg = next((n for n in world.node_tree.nodes if n.bl_idname == "ShaderNodeBackground"), None)
if bg:
    bg.inputs[0].default_value = (0.80, 0.80, 0.80, 1.0)
    bg.inputs[1].default_value = 0.85

floor = bpy.data.objects.get("Preview_Floor")
if floor is None:
    me = bpy.data.meshes.new("Preview_Floor")
    me.from_pydata([(-12, -12, 0), (12, -12, 0), (12, 12, 0), (-12, 12, 0)], [], [(0, 1, 2, 3)])
    floor = bpy.data.objects.new("Preview_Floor", me)
    pcol.objects.link(floor)
    fm = bpy.data.materials.new("Preview_Floor")
    b = next(n for n in fm.node_tree.nodes if n.bl_idname == "ShaderNodeBsdfPrincipled")
    b.inputs["Base Color"].default_value = (0.8, 0.8, 0.8, 1)
    b.inputs["Roughness"].default_value = 0.9
    me.materials.append(fm)

if FRAME is not None:
    scene.frame_set(FRAME)

tiles = []
tmp = os.path.join(os.path.dirname(OUT), "_tile.png")
for view in VIEWS:
    az, el, mode = view[:3]
    if len(view) > 3:
        scene.frame_set(view[3])
    a, e = math.radians(az), math.radians(el)
    direction = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
    if mode == "ortho":
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = SPAN
        dist = 8.0
    else:
        cam.data.type = 'PERSP'
        cam.data.lens = 70
        dist = SPAN * 2.15
    cam.location = TARGET + direction * dist
    cam.rotation_euler = (TARGET - cam.location).to_track_quat('-Z', 'Y').to_euler()
    # eclairage attache a la camera (cle en haut a gauche, debouchage a droite)
    sun.rotation_euler = (math.radians(52), 0, a + math.radians(-38))
    fill.rotation_euler = (math.radians(70), 0, a + math.radians(120))
    scene.render.filepath = tmp
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(tmp)
    px = np.empty(TILE * TILE * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    tiles.append(px.reshape(TILE, TILE, 4).copy())
    bpy.data.images.remove(img)

cols = min(len(tiles), globals().get("COLS", 3))
rows = (len(tiles) + cols - 1) // cols
sheet = np.ones((rows * TILE, cols * TILE, 4), dtype=np.float32)
for i, t in enumerate(tiles):
    r, c = i // cols, i % cols
    y0 = (rows - 1 - r) * TILE
    sheet[y0:y0 + TILE, c * TILE:(c + 1) * TILE] = t
out_img = bpy.data.images.new("_sheet", cols * TILE, rows * TILE, alpha=False)
out_img.pixels.foreach_set(sheet.ravel())
out_img.filepath_raw = OUT
out_img.file_format = 'PNG'
out_img.save()
bpy.data.images.remove(out_img)
if os.path.exists(tmp):
    os.remove(tmp)
result = {"file": OUT, "tiles": len(tiles)}
