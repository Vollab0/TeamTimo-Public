# Rendus de controle de la souris (jour / nuit, plusieurs angles). Ne sert pas a l'export.
# Variables attendues avant exec : OUT_DIR (dossier), VIEWS (liste de noms), NIGHT (bool), TAG (str), FRAME (int ou None)
import bpy
import math
import os
from mathutils import Vector

scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 560
scene.render.resolution_y = 560
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
cam.data.lens = 60
scene.camera = cam
sun = get_obj("Preview_Sun", lambda: bpy.data.lights.new("Preview_Sun", 'SUN'))
sun.data.energy = 2.2
sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
fill = get_obj("Preview_Fill", lambda: bpy.data.lights.new("Preview_Fill", 'SUN'))
fill.data.energy = 0.6
fill.rotation_euler = (math.radians(65), 0, math.radians(140))
for nm in ("Light", "Camera"):
    o = bpy.data.objects.get(nm)
    if o:
        o.hide_render = True

world = scene.world or bpy.data.worlds.new("World")
scene.world = world
bg = next((n for n in world.node_tree.nodes if n.bl_idname == "ShaderNodeBackground"), None)
if bg:
    bg.inputs[0].default_value = (0.78, 0.78, 0.80, 1.0)
    bg.inputs[1].default_value = 0.9

# Sol clair pour les ombres
floor = bpy.data.objects.get("Preview_Floor")
if floor is None:
    me = bpy.data.meshes.new("Preview_Floor")
    me.from_pydata([(-4, -4, 0), (4, -4, 0), (4, 4, 0), (-4, 4, 0)], [], [(0, 1, 2, 3)])
    floor = bpy.data.objects.new("Preview_Floor", me)
    pcol.objects.link(floor)
    fm = bpy.data.materials.new("Preview_Floor")
    b = next(n for n in fm.node_tree.nodes if n.bl_idname == "ShaderNodeBsdfPrincipled")
    b.inputs["Base Color"].default_value = (0.8, 0.8, 0.82, 1)
    b.inputs["Roughness"].default_value = 0.9
    me.materials.append(fm)

body = bpy.data.objects["Mouse_Body"]
eyes_day = bpy.data.objects["Mouse_Eyes_Day"]
eyes_night = bpy.data.objects["Mouse_Eyes_Night"]
body.data.materials[0] = bpy.data.materials["Mouse_Night" if NIGHT else "Mouse_Day"]
eyes_day.hide_render = NIGHT
eyes_night.hide_render = not NIGHT

if FRAME is not None:
    scene.frame_set(FRAME)

target = Vector((0, 0.05, 0.22))
ANGLES = {   # nom : (azimut en degres depuis -Y, elevation, distance)
    "front34": (-35, 14, 2.6),
    "front": (0, 8, 2.6),
    "side": (-90, 6, 2.8),
    "back34": (145, 18, 2.8),
    "top": (-20, 60, 2.8),
    "face": (-20, 8, 1.5),
}
outs = []
for view in VIEWS:
    az, el, dist = ANGLES[view]
    a, e = math.radians(az), math.radians(el)
    pos = target + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * dist
    cam.location = pos
    cam.rotation_euler = (target - pos).to_track_quat('-Z', 'Y').to_euler()
    path = os.path.join(OUT_DIR, f"{TAG}_{view}.png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    outs.append(path)

# Retour a l'etat par defaut (jour)
body.data.materials[0] = bpy.data.materials["Mouse_Day"]
eyes_day.hide_render = False
eyes_night.hide_render = True
result = {"files": outs}
