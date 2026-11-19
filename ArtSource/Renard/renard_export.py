# Exporte les animations du renard (squelette seul, sans maillage) vers Assets/Art/Models/Characters/Renard_Animations.fbx.
# Le modele, l'Idle et la marche restent dans Renard.fbx : ce FBX ne sert qu'a fournir des clips. Tous les os sont exportes
# (pas seulement ceux de deformation) pour garder exactement la hierarchie de Renard.fbx : les chemins des courbes
# ("Armature/Bassin/...") correspondent alors a ceux du prefab Player. Un objet vide est ajoute a la racine pour
# qu'Unity garde le noeud "Armature" (un FBX a une seule racine est replie).
# L'IK des jambes et le regard (contraintes) sont cuits image par image par l'exporteur.
import bpy

FBX_PATH = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo\Assets\Art\Models\Characters\Renard_Animations.fbx"
CLIPS = ["Course", "Saut", "Chute", "Atterrissage", "Roulade", "Attaque", "Poussee", "Degats",
         "Effondrement", "AuSol", "Ramper", "Mort", "Relever"]

rig = bpy.data.objects["Armature"]
anchor = bpy.data.objects.get("Renard_Animations_Ancre")
if anchor is None:
    anchor = bpy.data.objects.new("Renard_Animations_Ancre", None)
    bpy.context.scene.collection.objects.link(anchor)

# Seules les actions du renard partent dans le FBX
for a in bpy.data.actions:
    if a.name not in CLIPS:
        bpy.data.actions.remove(a)

if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
for o in bpy.context.view_layer.objects:
    o.select_set(False)
rig.select_set(True)
anchor.select_set(True)
bpy.context.view_layer.objects.active = rig
first = bpy.data.actions["Atterrissage"]     # 1re par ordre alphabetique ; sa derniere image = pose de repos
rig.animation_data.action = first
rig.animation_data.action_slot = first.slots[0]

bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
    use_selection=True,
    object_types={'ARMATURE', 'EMPTY'},
    apply_scale_options='FBX_SCALE_NONE',
    global_scale=1.0,
    axis_forward='-Z',
    axis_up='Y',
    bake_space_transform=False,
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
)
rig.select_set(False)
anchor.select_set(False)
result = {"fbx": FBX_PATH, "clips": [a.name for a in bpy.data.actions]}
