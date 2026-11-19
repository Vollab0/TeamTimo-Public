# Prepare la scene Blender du renard a partir de Assets/Art/Models/Characters/Renard.fbx (modele de Gabin).
# Le .blend d'origine n'est pas dans le depot : on repart du FBX (squelette + Idle "Repos" cuits).
#  1. importe le FBX dans une scene vide ;
#  2. releve dans l'Idle la pose de base (bras baisses, doigts plies), la paupiere fermee (clignement)
#     et le petit coup d'oreille -> renard_base_pose.json (reutilise par renard_anim.py) ;
#  3. supprime les actions importees (elles restent dans Renard.fbx, cote Unity) ;
#  4. remet les contraintes du rig d'origine : IK des jambes (IK_Pied -> Tibias, la cheville suit IK_Pied)
#     et regard (Oeil.L/R visent Controlleur_Oeil.L/R, deplaces par Controlleur_Yeux, comme dans l'Idle).
import bpy, json, os

SRC = r"C:\Users\gabin\Documents\Projets Unity\TeamTimo"
FBX = os.path.join(SRC, r"Assets\Art\Models\Characters\Renard.fbx")
OUT = os.path.join(SRC, r"ArtSource\Renard\renard_base_pose.json")

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)
win = bpy.context.window_manager.windows[0]
area = next((a for a in win.screen.areas if a.type == 'VIEW_3D'), win.screen.areas[0])
with bpy.context.temp_override(window=win, area=area):
    bpy.ops.import_scene.fbx(filepath=FBX)

rig = bpy.data.objects["Armature"]
repos = next(a for a in bpy.data.actions if a.name.endswith("Repos"))
rig.animation_data.action = repos
rig.animation_data.action_slot = repos.slots[0]

def snap(frame):
    bpy.context.scene.frame_set(frame)
    return {pb.name: dict(q=list(pb.rotation_quaternion), loc=list(pb.location), scale=list(pb.scale))
            for pb in rig.pose.bones}

base = snap(1)
blink = snap(11)     # paupieres fermees
flick = snap(53)     # oreille droite / gauche relevee
data = dict(
    base={k: v for k, v in base.items()},
    lid_closed={b: blink[b]["q"] for b in ("Paupière.L", "Paupière.R")},
    ear_flick={b: flick[b]["q"] for b in ("Oreille_Haut.L", "Oreille_Haut.R")},
)
with open(OUT, "w", encoding="utf-8") as f:
    json.dump(data, f, ensure_ascii=False, indent=0)

rig.animation_data.action = None
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)
for pb in rig.pose.bones:
    pb.rotation_mode = 'QUATERNION'
    pb.rotation_quaternion = (1, 0, 0, 0)
    pb.location = (0, 0, 0)
    pb.scale = (1, 1, 1)
    for c in list(pb.constraints):
        pb.constraints.remove(c)

for side in ("L", "R"):
    ik = rig.pose.bones["Tibias." + side].constraints.new('IK')
    ik.target = rig
    ik.subtarget = "IK_Pied." + side
    ik.chain_count = 2
    cr = rig.pose.bones["Cheville." + side].constraints.new('COPY_ROTATION')
    cr.target = rig
    cr.subtarget = "IK_Pied." + side
    dt = rig.pose.bones["Oeil." + side].constraints.new('DAMPED_TRACK')
    dt.target = rig
    dt.subtarget = "Controlleur_Oeil." + side
    dt.track_axis = 'TRACK_Y'

bpy.context.scene.render.fps = 30
result = {"bones": len(rig.pose.bones), "json": OUT}
