# Enregistre Bull.blend et exporte Bull.fbx (maillages + squelette + toutes les animations) vers Unity.
# Seuls les os de deformation sont exportes ; l'IK des jambes et le regard sont cuits image par image.
# Cote Unity : echelle d'import x4, "Bake Axis Conversion" decoche (le taureau regarde vers -Y dans Blender).
import bpy
import os

SRC_DIR = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo\ArtSource\Taureau"
FBX_PATH = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo\Assets\Art\Models\Characters\Bull.fbx"

names = ["Bull_Rig", "Bull_Body", "Bull_Eye_L", "Bull_Eye_R", "Bull_Lid_L", "Bull_Lid_R"]
objs = [bpy.data.objects[n] for n in names]
rig = objs[0]

if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')

# Etat neutre : Idle, image 0, materiau de jour
idle = bpy.data.actions["Idle"]
rig.animation_data.action = idle
rig.animation_data.action_slot = idle.slots[0]
bpy.context.scene.frame_set(0)
for o in objs[1:]:
    o.data.materials[0] = bpy.data.materials["Bull_Day"]

for o in bpy.context.view_layer.objects:
    o.select_set(False)
for o in objs:
    o.hide_set(False)
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
    use_armature_deform_only=True,
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

for o in objs:
    o.select_set(False)
rig.animation_data.action = idle
rig.animation_data.action_slot = idle.slots[0]
bpy.context.scene.frame_set(0)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC_DIR, "Bull.blend"))
result = {"fbx": FBX_PATH, "fbx_bytes": os.path.getsize(FBX_PATH), "blend": bpy.data.filepath,
          "actions": [a.name for a in bpy.data.actions]}
