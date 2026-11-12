# Enregistre Mouse.blend et exporte Mouse.fbx (maillages + squelette + toutes les animations) vers Unity.
import bpy
import os

SRC_DIR = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo\ArtSource\Souris"
FBX_PATH = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo\Assets\Art\Models\Characters\Mouse.fbx"

names = ["Mouse_Rig", "Mouse_Body", "Mouse_Eyes_Day", "Mouse_Eyes_Night"]
objs = [bpy.data.objects[n] for n in names]
rig = objs[0]
night = bpy.data.objects["Mouse_Eyes_Night"]

if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')

# Etat neutre : Idle, image 0, materiau de jour
idle = bpy.data.actions["Idle"]
rig.animation_data.action = idle
rig.animation_data.action_slot = idle.slots[0]
bpy.context.scene.frame_set(0)
bpy.data.objects["Mouse_Body"].data.materials[0] = bpy.data.materials["Mouse_Day"]

night.hide_set(False)          # le visage de nuit doit etre visible pour etre exporte
for o in bpy.context.view_layer.objects:
    o.select_set(False)
for o in objs:
    o.select_set(True)
bpy.context.view_layer.objects.active = rig

bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
    use_selection=True,
    object_types={'ARMATURE', 'MESH'},
    apply_scale_options='FBX_SCALE_UNITS',
    axis_forward='-Z',
    axis_up='Y',
    bake_space_transform=False,
    use_mesh_modifiers=True,
    mesh_smooth_type='OFF',
    add_leaf_bones=False,
    use_armature_deform_only=False,
    primary_bone_axis='Y',
    secondary_bone_axis='X',
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True,
    bake_anim_step=1.0,
    bake_anim_simplify_factor=0.0,
    path_mode='AUTO',
    embed_textures=False,
)

night.hide_set(True)
for o in objs:
    o.select_set(False)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC_DIR, "Mouse.blend"))
result = {"fbx": FBX_PATH, "fbx_bytes": os.path.getsize(FBX_PATH), "blend": bpy.data.filepath,
          "actions": [a.name for a in bpy.data.actions]}
