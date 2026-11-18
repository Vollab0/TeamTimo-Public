// Configuration Unity des bâtiments du village, à rejouer après un nouvel export (build_houses.py, build_barn.py, build_tent.py) :
//   unity command script eval --file ArtSource/Lobby/unity_setup_village.cs --project-path <projet>
// puis LobbyVillageBuilder.ReplaceBlockouts(true) pour reposer les bâtiments dans la scène.
// Règle l'import des textures et des FBX du village, crée les matériaux partagés et les prefabs
// P_House_S/M/L, P_Barn, P_Tent_Medic attendus par LobbyVillageBuilder.
// Colliders : les meshes « *_Col<n> » du FBX deviennent des MeshCollider convexes sur le calque Wall
// (enfant « Colliders ») : la caméra (CinemachineCollider), la vue des ennemis et le taureau s'y arrêtent.
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

var log = new System.Text.StringBuilder();
const string modelDir = "Assets/Art/Models/Environment/Village/";
const string texDir = "Assets/Art/Textures/Environment/Village/";
const string matDir = "Assets/Art/Materials/Environment/Village/";
const string prefabDir = "Assets/Prefabs/Environment/Village";
int wallLayer = LayerMask.NameToLayer("Wall");

AssetDatabase.Refresh();

// ------------------------------------------------------------------ Textures
foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { texDir.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath))
{
    var ti = (TextureImporter)AssetImporter.GetAtPath(path);
    bool normal = path.EndsWith("_Normal.png");
    ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
    ti.sRGBTexture = !normal;
    ti.maxTextureSize = 1024;
    ti.mipmapEnabled = true;
    ti.wrapMode = TextureWrapMode.Repeat;
    ti.anisoLevel = 4;
    ti.textureCompression = TextureImporterCompression.CompressedHQ;
    ti.SaveAndReimport();
}

// ------------------------------------------------------------------ Matériaux
if (!AssetDatabase.IsValidFolder(matDir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Art/Materials/Environment", "Village");
Shader lit = Shader.Find("Universal Render Pipeline/Lit");
Material MakeMat(string name, string albedo, string normal, float smooth)
{
    string p = matDir + name + ".mat";
    var m = AssetDatabase.LoadAssetAtPath<Material>(p);
    if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, p); }
    m.shader = lit;
    m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + albedo + ".png"));
    m.SetColor("_BaseColor", Color.white);
    if (normal != null)
    {
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + normal + ".png"));
        m.SetFloat("_BumpScale", 1f);
        m.EnableKeyword("_NORMALMAP");
    }
    m.SetFloat("_Smoothness", smooth);
    m.SetFloat("_Metallic", 0f);
    EditorUtility.SetDirty(m);
    return m;
}
// Noms des matériaux dans Blender (village_lib.py) -> matériaux du projet.
var remap = new System.Collections.Generic.Dictionary<string, Material>
{
    { "M_Village_Wood", MakeMat("M_Village_Wood", "T_Village_Wood_Albedo", "T_Village_Wood_Normal", 0.15f) },
    { "M_Village_WoodWhite", MakeMat("M_Village_WoodWhite", "T_Village_WoodWhite_Albedo", "T_Village_Wood_Normal", 0.15f) },
    { "M_Village_Plaster", MakeMat("M_Village_Plaster", "T_Village_Plaster_Albedo", "T_Village_Plaster_Normal", 0.05f) },
    { "M_Village_Planks", MakeMat("M_Village_Planks", "T_Village_Planks_Albedo", "T_Village_Planks_Normal", 0.1f) },
    { "M_Village_RedPlanks", MakeMat("M_Village_RedPlanks", "T_Village_RedPlanks_Albedo", "T_Village_Planks_Normal", 0.12f) },
    { "M_Village_Canvas", MakeMat("M_Village_Canvas", "T_Village_Canvas_Albedo", "T_Village_Canvas_Normal", 0.05f) },
    { "M_Village_PaintRed", MakeMat("M_Village_PaintRed", "T_Village_PaintRed_Albedo", null, 0.1f) },
    { "M_WallStone", AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Environment/M_WallStone.mat") },
};
foreach (string c in new[] { "Red", "Blue", "Yellow", "Wood", "Slate" })
    remap["M_Roof_" + c] = MakeMat("M_Roof_" + c, $"T_Village_RoofTile_{c}_Albedo", "T_Village_RoofTile_Normal", 0.12f);
// Forge et accessoires (build_forge.py, build_props.py…)
remap["M_Village_Brick"] = MakeMat("M_Village_Brick", "T_Village_Brick_Albedo", "T_Village_Brick_Normal", 0.08f);
remap["M_Village_BrickDark"] = MakeMat("M_Village_BrickDark", "T_Village_BrickDark_Albedo", "T_Village_Brick_Normal", 0.08f);
var metal = MakeMat("M_Village_Metal", "T_Village_Metal_Albedo", "T_Village_Metal_Normal", 0.45f);
metal.SetFloat("_Metallic", 0.35f);
remap["M_Village_Metal"] = metal;
var iron = MakeMat("M_Village_Iron", "T_Village_Iron_Albedo", "T_Village_Iron_Normal", 0.35f);
iron.SetFloat("_Metallic", 0.45f);
remap["M_Village_Iron"] = iron;
// Bois sombre (plancher de la forge) : la texture du bois, teintée.
var woodDark = MakeMat("M_Village_WoodDark", "T_Village_Wood_Albedo", "T_Village_Wood_Normal", 0.1f);
woodDark.SetColor("_BaseColor", new Color(0.5f, 0.42f, 0.38f));
remap["M_Village_WoodDark"] = woodDark;
// Braises : la texture sert aussi d'émission (lueur orange sans lumière dynamique).
var embers = MakeMat("M_Village_Embers", "T_Village_Embers_Albedo", null, 0.2f);
embers.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "T_Village_Embers_Albedo.png"));
embers.SetColor("_EmissionColor", new Color(1.5f, 1.1f, 0.8f));
embers.EnableKeyword("_EMISSION");
embers.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
remap["M_Village_Embers"] = embers;
// Étals et accessoires (build_market.py, build_props.py)
remap["M_Village_Stripes"] = MakeMat("M_Village_Stripes", "T_Village_Stripes_Albedo", null, 0.05f);
remap["M_Village_Clay"] = MakeMat("M_Village_Clay", "T_Village_Clay_Albedo", null, 0.2f);
remap["M_Village_Burlap"] = MakeMat("M_Village_Burlap", "T_Village_Burlap_Albedo", "T_Village_Burlap_Normal", 0.05f);
remap["M_Village_Hay"] = MakeMat("M_Village_Hay", "T_Village_Hay_Albedo", "T_Village_Hay_Normal", 0.05f);
remap["M_Village_Soil"] = MakeMat("M_Village_Soil", "T_Village_Soil_Albedo", "T_Village_Soil_Normal", 0.05f);
remap["M_Village_Water"] = MakeMat("M_Village_Water", "T_Village_Water_Albedo", null, 0.9f);
remap["M_Village_Leaf"] = MakeMat("M_Village_Leaf", "T_Village_Leaf_Albedo", null, 0.15f);
remap["M_Village_EndGrain"] = MakeMat("M_Village_EndGrain", "T_Village_EndGrain_Albedo", null, 0.1f);
var lamp = MakeMat("M_Village_LampGlass", "T_Village_LampGlass_Albedo", null, 0.6f);
lamp.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "T_Village_LampGlass_Albedo.png"));
lamp.SetColor("_EmissionColor", new Color(1.6f, 1.3f, 0.8f));
lamp.EnableKeyword("_EMISSION");
lamp.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
remap["M_Village_LampGlass"] = lamp;
// Clôture blanche (LobbyVillageBuilder.PlaceFence) : bois peint en blanc.
remap["M_Fence_White"] = MakeMat("M_Fence_White", "T_Village_WoodWhite_Albedo", "T_Village_Wood_Normal", 0.12f);
AssetDatabase.SaveAssets();

// ------------------------------------------------------------------ Modèles et prefabs
if (!AssetDatabase.IsValidFolder(prefabDir)) AssetDatabase.CreateFolder("Assets/Prefabs/Environment", "Village");
// (fbx, prefab, préfixe des objets, parties visibles [nom dans le prefab = suffixe dans le FBX], zone non praticable, calque Wall)
// nav : "full" = plein, volume « Not Walkable » de navSize (de -3 m jusqu'au-dessus) ; "above" = seulement au-dessus
// de navAbove (toit, intérieur praticable) ; "auto" = plein, volume calculé d'après le mesh (accessoires) ; "none" = sol praticable.
// wall = vrai : colliders sur le calque Wall (bâtiments : la caméra, la vue des ennemis et le taureau s'y arrêtent) ;
// faux : calque Default (petits accessoires : la caméra ne s'y accroche pas).
var body = new[] { ("Body", "Body") };
var houseParts = new[]
{
    ("Body", "Body"), ("Roof", "Roof"), ("Options/cheminee", "Chimney"),
    ("Options/lucarne/Body", "Dormer"), ("Options/lucarne/Roof", "DormerRoof"),
    ("Options/appentis/Body", "LeanTo"), ("Options/auvent_toile", "Awning"),
    ("Options/perron/Body", "Porch"),
    ("Options/pignon_croise/Body", "CrossGable"), ("Options/pignon_croise/Roof", "CrossGableRoof"),
};
var models = new (string fbx, string prefab, string prefix, (string child, string part)[] parts, Vector3 navSize, string nav, float navAbove, bool wall)[]
{
    ("SM_House_S", "P_House_S", "House_S", houseParts, new Vector3(12f, 23f, 12f), "full", 0f, true),
    ("SM_House_M", "P_House_M", "House_M", houseParts, new Vector3(12f, 24f, 16f), "full", 0f, true),
    ("SM_House_L", "P_House_L", "House_L", houseParts, new Vector3(13f, 26f, 24f), "full", 0f, true),
    ("SM_Barn", "P_Barn", "Barn", new[] { ("Body", "Body"), ("Roof", "Roof") }, new Vector3(44f, 26f, 46f), "above", 16f, true),
    ("SM_Tent_Medic", "P_Tent_Medic", "Tent", new[] { ("Canvas", "Canvas"), ("Frame", "Frame") }, new Vector3(23f, 12f, 28f), "above", 7.5f, true),
    // Forge (build_forge.py)
    ("SM_Forge_Kiln", "P_Forge_Kiln", "Kiln", body, new Vector3(19f, 26f, 26f), "full", 0f, true),
    ("SM_Forge_Furnace", "P_Forge_Furnace", "Furnace", body, new Vector3(17f, 23f, 20f), "full", 0f, true),
    ("SM_Forge_Deck", "P_Forge_Deck", "ForgeDeck", body, Vector3.zero, "none", 0f, false),
    ("SM_Anvil", "P_Anvil", "Anvil", body, Vector3.zero, "auto", 0f, false),
    ("SM_ToolRack", "P_ToolRack", "ToolRack", body, Vector3.zero, "auto", 0f, false),
    ("SM_Sawhorse", "P_Sawhorse", "Sawhorse", body, Vector3.zero, "auto", 0f, false),
    ("SM_Barrel_Stack", "P_Barrel_Stack", "BarrelStack", body, Vector3.zero, "auto", 0f, false),
    ("SM_Signpost", "P_Signpost", "Signpost", body, Vector3.zero, "auto", 0f, false),
};
// Accessoires des autres scripts (clôtures, étals, petits accessoires) : listés dans village_props.txt
// (une ligne « fbx;prefab;prefix;nav;wall » par asset, écrite par les scripts Blender), s'il existe.
string propsList = "ArtSource/Lobby/village_props.txt";
var modelList = models.ToList();
if (System.IO.File.Exists(propsList))
    foreach (string line in System.IO.File.ReadAllLines(propsList))
    {
        string[] f = line.Trim().Split(';');
        if (f.Length < 5 || line.StartsWith("#")) continue;
        modelList.Add((f[0], f[1], f[2], body, Vector3.zero, f[3], 0f, f[4] == "wall"));
    }
foreach (var mod in modelList)
{
    if (AssetImporter.GetAtPath(modelDir + mod.fbx + ".fbx") == null) { log.AppendLine("FBX absent (ignoré) : " + mod.fbx); continue; }
    string fbx = modelDir + mod.fbx + ".fbx";
    var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
    if (mi == null) { log.AppendLine("FBX manquant : " + fbx); continue; }
    // Les sous-meshes du FBX suivent l'ordre d'apparition des faces, pas celui des emplacements Blender :
    // on importe les noms de matériaux, on les remappe, et on relit l'ordre sur les renderers du FBX.
    mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
    foreach (var pair in remap)
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
    mi.importNormals = ModelImporterNormals.Import;
    mi.importTangents = ModelImporterTangents.CalculateMikk;
    mi.bakeAxisConversion = false;
    mi.importAnimation = false;
    mi.animationType = ModelImporterAnimationType.None;
    mi.isReadable = true;      // le NavMesh est calculé au lancement (MazeNavMeshBuilder) : il doit lire les meshes des MeshCollider
    mi.SaveAndReimport();

    var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
    var meshes = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().ToDictionary(m => m.name);
    var renderers = model.GetComponentsInChildren<MeshRenderer>(true).ToDictionary(r => r.name);
    // Un FBX à un seul objet est importé comme racine (le renderer porte alors le nom du fichier).
    MeshRenderer Rend(string n) => renderers.TryGetValue(n, out var r) ? r : model.GetComponentsInChildren<MeshFilter>(true).First(f => f.sharedMesh.name == n).GetComponent<MeshRenderer>();

    var root = new GameObject(mod.prefab);
    int tris = 0;
    foreach (var (child, part) in mod.parts)
    {
        string name = $"{mod.prefix}_{part}";
        if (!meshes.TryGetValue(name, out Mesh mesh)) { log.AppendLine("mesh manquant : " + name); continue; }
        Transform parent = root.transform;
        string[] path = child.Split('/');
        for (int i = 0; i < path.Length - 1; i++)
        {
            Transform t = parent.Find(path[i]);
            if (t == null) { t = new GameObject(path[i]).transform; t.SetParent(parent, false); }
            parent = t;
        }
        var go = new GameObject(path[path.Length - 1]);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = Rend(name).sharedMaterials;
        for (int s = 0; s < mesh.subMeshCount; s++) tris += (int)mesh.GetIndexCount(s) / 3;
    }
    int colLayer = mod.wall ? wallLayer : 0;
    var cols = new GameObject("Colliders");
    cols.transform.SetParent(root.transform, false);
    cols.layer = colLayer;
    int nCol = 0;
    foreach (var pair in meshes.Where(p => p.Key.StartsWith(mod.prefix + "_Col")).OrderBy(p => p.Key))
    {
        var c = new GameObject(pair.Key.Substring(mod.prefix.Length + 1)) { layer = colLayer };
        c.transform.SetParent(cols.transform, false);
        var mc = c.AddComponent<MeshCollider>();
        mc.sharedMesh = pair.Value;
        mc.convex = true;
        nCol++;
    }
    var optColTargets = new[] { ("LeanToCol", "Options/appentis", wallLayer), ("PorchCol", "Options/perron", 0), ("CrossGableCol", "Options/pignon_croise", wallLayer) };
    foreach (var (colPrefix, optPath, layer) in optColTargets)
    {
        Transform optT = root.transform.Find(optPath);
        if (optT == null) continue;
        foreach (var pair in meshes.Where(p => p.Key.StartsWith(mod.prefix + "_" + colPrefix)).OrderBy(p => p.Key))
        {
            var c = new GameObject(pair.Key.Substring(mod.prefix.Length + 1)) { layer = layer };
            c.transform.SetParent(optT, false);
            var mc = c.AddComponent<MeshCollider>();
            mc.sharedMesh = pair.Value;
            mc.convex = true;
            nCol++;
        }
    }
    if (mod.nav != "none")
    {
        var vol = root.AddComponent<NavMeshModifierVolume>();
        vol.area = 1;      // Not Walkable
        if (mod.nav == "full")
        {
            vol.size = mod.navSize;
            vol.center = new Vector3(0f, mod.navSize.y / 2f - 3f, 0f);       // de -3 m (terrain en pente) au-dessus du faîtage
        }
        else if (mod.nav == "above")
        {
            vol.size = mod.navSize;
            vol.center = new Vector3(0f, mod.navAbove + mod.navSize.y / 2f, 0f);   // seulement le toit : l'intérieur reste praticable
        }
        else
        {
            // Accessoire : emprise des meshes visibles, de -1 m à 2 m au-dessus (pas d'îlot dessus ni dessous).
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (first) { b = mf.sharedMesh.bounds; first = false; } else b.Encapsulate(mf.sharedMesh.bounds);
            }
            vol.size = new Vector3(b.size.x, b.max.y + 3f, b.size.z);
            vol.center = new Vector3(b.center.x, (b.max.y + 3f) / 2f - 1f, b.center.z);
        }
    }
    // Grand four : lueur orange de la gueule (lumière ponctuelle sans ombres).
    if (mod.prefab == "P_Forge_Kiln")
    {
        var glow = new GameObject("Lueur");
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = new Vector3(0f, 2.5f, 11.5f - 1.2f);
        var light = glow.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.55f, 0.2f);
        light.intensity = 30f;
        light.range = 16f;
        light.shadows = LightShadows.None;
    }
    // Potence : petite lumière chaude sous la lanterne (côté du bras, lu sur l'emprise du mesh).
    if (mod.prefab == "P_LanternPost")
    {
        Bounds b = root.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
        float lx = b.max.x > -b.min.x ? b.max.x - 1.1f : b.min.x + 1.1f;
        var glow = new GameObject("Lueur");
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = new Vector3(lx, 6.3f, 0f);
        var light = glow.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.78f, 0.45f);
        light.intensity = 8f;
        light.range = 12f;
        light.shadows = LightShadows.None;
    }

    string layerName = mod.wall ? "Wall" : "Default";
    string prefabPath = $"{prefabDir}/{mod.prefab}.prefab";
    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
    Object.DestroyImmediate(root);
    log.AppendLine($"{prefabPath} : {tris} triangles, {nCol} colliders ({layerName}), matériaux : " +
        string.Join(" | ", mod.parts.Select(p => p.child + "=" + string.Join(",", Rend($"{mod.prefix}_{p.part}").sharedMaterials.Select(m => m ? m.name : "AUCUN")))));
}
AssetDatabase.SaveAssets();
return log.ToString();
