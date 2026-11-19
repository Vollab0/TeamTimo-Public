# Planches d'apercu des animations du renard (EEVEE) : une ligne par clip, quelques images par ligne.
# Utilisation (depuis le MCP Blender) : definir PREVIEW = [("Marche", [0, 4, 8, 12]), ...], VIEW = "34" | "side" | "front",
# OUT = chemin du PNG, puis exec() de ce fichier.
import bpy, math, os
import numpy as np
from mathutils import Vector

TILE = globals().get("TILE", 300)
VIEW = globals().get("VIEW", "34")
OUT = globals()["OUT"]
PREVIEW = globals()["PREVIEW"]
scene = bpy.context.scene
rig = bpy.data.objects["Armature"]

scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = TILE
scene.render.resolution_y = TILE
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
if scene.world is None:
    scene.world = bpy.data.worlds.new("Preview_World")
scene.world.use_nodes = True
bg = scene.world.node_tree.nodes.get("Background") or next(n for n in scene.world.node_tree.nodes if n.bl_idname == 'ShaderNodeBackground')
NIGHT = globals().get("NIGHT", False)        # eclairage de nuit (lune bleutee) pour juger les couleurs
bg.inputs[0].default_value = (0.06, 0.08, 0.16, 1) if NIGHT else (0.55, 0.62, 0.70, 1)
bg.inputs[1].default_value = 0.6


def get_obj(name, make):
    o = bpy.data.objects.get(name)
    if o is None:
        o = bpy.data.objects.new(name, make())
        scene.collection.objects.link(o)
    return o


cam = get_obj("Preview_Cam", lambda: bpy.data.cameras.new("Preview_Cam"))
cam.data.lens = 50
scene.camera = cam
sun = get_obj("Preview_Sun", lambda: bpy.data.lights.new("Preview_Sun", 'SUN'))
sun.data.energy = 0.7 if NIGHT else 3.5
sun.data.color = (0.55, 0.66, 1.0) if NIGHT else (1.0, 1.0, 1.0)
sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
fill = get_obj("Preview_Fill", lambda: bpy.data.lights.new("Preview_Fill", 'SUN'))
fill.data.energy = 0.15 if NIGHT else 1.0
fill.rotation_euler = (math.radians(70), 0, math.radians(140))
floor = bpy.data.objects.get("Preview_Floor")
if floor is None:
    me = bpy.data.meshes.new("Preview_Floor")
    me.from_pydata([(-20, -20, 0), (20, -20, 0), (20, 20, 0), (-20, 20, 0)], [], [(0, 1, 2, 3)])
    floor = bpy.data.objects.new("Preview_Floor", me)
    scene.collection.objects.link(floor)
    mat = bpy.data.materials.new("Preview_Floor")
    mat.diffuse_color = (0.35, 0.45, 0.30, 1)
    me.materials.append(mat)

target = Vector((0, 0, 2.1))
dirs = {"34": Vector((0.55, -1.0, 0.18)), "side": Vector((1.0, 0.0, 0.10)), "front": Vector((0.0, -1.0, 0.15)),
        "back34": Vector((0.7, 0.9, 0.25))}
d = dirs[VIEW].normalized()
cam.location = target + d * globals().get("DIST", 9.0)
cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()

rows = []
tmp = os.path.join(os.path.dirname(OUT), "_tile.png")
for name, frames in PREVIEW:
    act = bpy.data.actions[name]
    rig.animation_data.action = act
    rig.animation_data.action_slot = act.slots[0]
    row = []
    for f in frames:
        scene.frame_set(f)
        scene.render.filepath = tmp
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(tmp, check_existing=False)
        px = np.array(img.pixels[:], dtype=np.float32).reshape(TILE, TILE, 4)
        bpy.data.images.remove(img)
        row.append(px)
    rows.append(np.concatenate(row, axis=1))
w = max(r.shape[1] for r in rows)
rows = [np.pad(r, ((0, 0), (0, w - r.shape[1]), (0, 0)), constant_values=1.0) for r in rows]
sheet = np.concatenate(rows[::-1], axis=0)  # les pixels Blender partent du bas
H, W = sheet.shape[:2]
out = bpy.data.images.new("_sheet", W, H, alpha=True)
out.pixels[:] = sheet.ravel()
out.filepath_raw = OUT
out.file_format = 'PNG'
out.save()
bpy.data.images.remove(out)
os.remove(tmp)
result = {"out": OUT, "size": [W, H]}
