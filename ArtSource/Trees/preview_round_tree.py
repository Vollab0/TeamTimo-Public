# Rendu d'aperçu d'un arbre (sapin : TREE_PREVIEW_PALETTE=pine, à lancer après build_pine_tree.py, dans le même fichier).
# Le matériau du feuillage imite le shader TeamTimo/StylizedFoliage (couleur de sommet).
import math
import os

import bpy
from mathutils import Vector

PROJECT = os.environ.get("TEAMTIMO_PROJECT", r"C:\Users\gabin\Documents\Projets Unity\TeamTimo")
TEX = os.path.join(PROJECT, "Assets", "Art", "Textures", "Environment")
OUT = os.environ.get("TREE_PREVIEW_OUT", os.path.join(PROJECT, "ArtSource", "Trees", "preview.png"))

# Couleurs (sRGB) proches de l'herbe et du sol
INNER = (0.13, 0.30, 0.16)
BASE = (0.42, 0.62, 0.20)
TOP = (0.70, 0.82, 0.32)
if os.environ.get("TREE_PREVIEW_PALETTE") == "pine":  # couleurs de M_PineFoliage_01
    INNER, BASE, TOP = (0.04, 0.16, 0.10), (0.13, 0.36, 0.17), (0.42, 0.62, 0.2)


def srgb_to_lin(c):
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


def foliage_material():
    m = bpy.data.materials["Tree_Foliage"]
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfDiffuse")
    attr = nt.nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "Col"
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(attr.outputs["Color"], sep.inputs[0])
    grad = nt.nodes.new("ShaderNodeMix")
    grad.data_type = "RGBA"
    grad.inputs[6].default_value = srgb_to_lin(BASE)
    grad.inputs[7].default_value = srgb_to_lin(TOP)
    nt.links.new(sep.outputs[1], grad.inputs[0])
    occ = nt.nodes.new("ShaderNodeMix")
    occ.data_type = "RGBA"
    occ.inputs[6].default_value = srgb_to_lin(INNER)
    nt.links.new(grad.outputs[2], occ.inputs[7])
    nt.links.new(sep.outputs[0], occ.inputs[0])
    alpha_tex = os.environ.get("TREE_PREVIEW_ALPHA")  # texture d'aiguilles du sapin (découpe)
    if not alpha_tex:
        nt.links.new(occ.outputs[2], bsdf.inputs["Color"])
        nt.links.new(bsdf.outputs[0], out.inputs[0])
        return
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(alpha_tex, check_existing=True)
    shade = nt.nodes.new("ShaderNodeMix")
    shade.data_type = "RGBA"
    shade.blend_type = "MULTIPLY"
    shade.inputs[0].default_value = 1.0
    nt.links.new(occ.outputs[2], shade.inputs[6])
    nt.links.new(tex.outputs["Color"], shade.inputs[7])
    nt.links.new(shade.outputs[2], bsdf.inputs["Color"])
    cut = nt.nodes.new("ShaderNodeMath")
    cut.operation = "GREATER_THAN"
    cut.inputs[1].default_value = 0.5
    nt.links.new(tex.outputs["Alpha"], cut.inputs[0])
    transp = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(cut.outputs[0], mix.inputs[0])
    nt.links.new(transp.outputs[0], mix.inputs[1])
    nt.links.new(bsdf.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])
    m.surface_render_method = "DITHERED"


def bark_material():
    m = bpy.data.materials["Tree_01"]
    m.use_nodes = True
    nt = m.node_tree
    tex = [n for n in nt.nodes if n.bl_idname == "ShaderNodeTexImage"]
    bsdf = [n for n in nt.nodes if n.bl_idname == "ShaderNodeBsdfPrincipled"]
    if not tex:  # matériau créé par script (sapin) : ajoute la texture d'écorce
        tex = [nt.nodes.new("ShaderNodeTexImage")]
    if tex and bsdf:
        tex[0].image = bpy.data.images.load(os.path.join(TEX, "Trees", "T_PineBark_Albedo.png"), check_existing=True)
        for link in list(bsdf[0].inputs["Normal"].links):
            nt.links.remove(link)
        nt.links.new(tex[0].outputs["Color"], bsdf[0].inputs["Base Color"])
        bsdf[0].inputs["Roughness"].default_value = 0.9


def setup_scene():
    sc = bpy.context.scene
    cam = bpy.data.objects.get("PreviewCam")
    if not cam:
        cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
        sc.collection.objects.link(cam)
    trees = [o for o in sc.objects if o.type == "MESH" and o.name.startswith("SM_Tree")]
    cx = sum(o.location.x for o in trees) / max(len(trees), 1)
    cam.location = (cx + 26, -40, 10)
    cam.rotation_euler = (Vector((cx, 0, 7.0)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 42
    sc.camera = cam
    sun = bpy.data.objects.get("PreviewSun")
    if not sun:
        sun = bpy.data.objects.new("PreviewSun", bpy.data.lights.new("PreviewSun", "SUN"))
        sc.collection.objects.link(sun)
    sun.data.energy = 3.5
    sun.data.angle = math.radians(3)
    sun.rotation_euler = (math.radians(50), 0, math.radians(35))
    ground = bpy.data.objects.get("PreviewGround")
    if not ground:
        me = bpy.data.meshes.new("PreviewGround")
        me.from_pydata([(-60, -60, 0), (80, -60, 0), (80, 60, 0), (-60, 60, 0)], [], [(0, 1, 2, 3)])
        ground = bpy.data.objects.new("PreviewGround", me)
        sc.collection.objects.link(ground)
        gm = bpy.data.materials.new("PreviewGround")
        gm.use_nodes = True
        b = [n for n in gm.node_tree.nodes if n.bl_idname == "ShaderNodeBsdfPrincipled"][0]
        b.inputs["Base Color"].default_value = srgb_to_lin((0.46, 0.63, 0.23))
        b.inputs["Roughness"].default_value = 1
        me.materials.append(gm)
    if not sc.world:
        sc.world = bpy.data.worlds.new("PreviewWorld")
    sc.world.use_nodes = True
    bg = [n for n in sc.world.node_tree.nodes if n.bl_idname == "ShaderNodeBackground"][0]
    bg.inputs[0].default_value = srgb_to_lin((0.62, 0.78, 0.95))
    bg.inputs[1].default_value = 0.9
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = 1400
    sc.render.resolution_y = 700
    sc.view_settings.view_transform = "Standard"
    sc.render.filepath = OUT


foliage_material()
bark_material()
setup_scene()
bpy.ops.render.render(write_still=True)
result = {"preview": OUT}
