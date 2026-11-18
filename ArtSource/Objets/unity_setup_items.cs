// Import Unity des objets (étape 2 -> 3 du plan GameDesign/03_Contenu/Objets/Objets_Plan_De_Production.md),
// à rejouer après un nouvel export Blender (build_items_*.py) ou un nouvel atlas (gen_items_atlas.py) :
//   unity command script eval --file ArtSource/Objets/unity_setup_items.cs --project-path <projet>
// Fait : réglages d'import de T_Items_Atlas / T_Items_Emission, matériau URP Lit partagé M_Items,
// réglages d'import des FBX de Assets/Art/Models/Objects/Items (matériau « M_Items » remappé, échelle 1,
// Bake Axis Conversion décoché). Renvoie un rapport (taille, pivot, sous-objets) pour contrôle.
// Puis, pour les objets de la liste « items » : prefab ramassable Assets/Prefabs/Items/P_Item_<Code>.prefab,
// données Assets/Data item/Data_<Code>.asset (ItemData / WeaponItem) et icône rendue
// Assets/UI/Sprites/Items/Icon_<Code>.png (étapes 3 et 4 du plan). Relançable : les assets existants sont mis à jour.
// Coffre, pièce et bourse : pas encore de prefab (décor interactif / butin, script d'ouverture à écrire).
using System.Linq;
using UnityEditor;
using UnityEngine;

var log = new System.Text.StringBuilder();
const string modelDir = "Assets/Art/Models/Objects/Items";
const string texDir = "Assets/Art/Textures/Objects/";
const string matPath = "Assets/Art/Materials/Objects/M_Items.mat";

AssetDatabase.Refresh();

// ------------------------------------------------------------------ Textures
foreach (string name in new[] { "T_Items_Atlas", "T_Items_Emission" })
{
    var ti = (TextureImporter)AssetImporter.GetAtPath(texDir + name + ".png");
    if (ti == null) { log.AppendLine("Texture absente : " + name); continue; }
    ti.textureType = TextureImporterType.Default;
    ti.sRGBTexture = true;
    ti.maxTextureSize = 1024;
    ti.mipmapEnabled = true;
    ti.wrapMode = TextureWrapMode.Repeat;      // les bandes de l'atlas se raccordent en u
    ti.filterMode = FilterMode.Bilinear;
    ti.anisoLevel = 4;
    ti.textureCompression = TextureImporterCompression.CompressedHQ;
    ti.SaveAndReimport();
}

// ------------------------------------------------------------------ Matériau partagé
var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
Shader lit = Shader.Find("Universal Render Pipeline/Lit");
if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, matPath); }
mat.shader = lit;
mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "T_Items_Atlas.png"));
mat.SetColor("_BaseColor", Color.white);
mat.SetFloat("_Smoothness", 0.25f);
mat.SetFloat("_Metallic", 0f);
// Masque d'émission (noir pour l'instant : braises, potions, runes viendront avec les planches suivantes).
mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "T_Items_Emission.png"));
mat.SetColor("_EmissionColor", Color.white);
mat.EnableKeyword("_EMISSION");
mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
EditorUtility.SetDirty(mat);
AssetDatabase.SaveAssets();

// ------------------------------------------------------------------ FBX
foreach (string fbx in AssetDatabase.FindAssets("t:Model", new[] { modelDir }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
{
    var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
    if (mi == null) continue;
    mi.globalScale = 1f;
    mi.useFileScale = true;
    mi.bakeAxisConversion = false;
    mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_Items"), mat);
    mi.importNormals = ModelImporterNormals.Import;
    mi.importTangents = ModelImporterTangents.CalculateMikk;
    mi.importAnimation = false;
    mi.animationType = ModelImporterAnimationType.None;
    mi.importCameras = false;
    mi.importLights = false;
    mi.isReadable = false;
    mi.SaveAndReimport();

    var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
    var inst = (GameObject)Object.Instantiate(model);
    inst.transform.position = Vector3.zero;
    var b = new Bounds();
    bool first = true;
    int tris = 0;
    foreach (var r in inst.GetComponentsInChildren<MeshRenderer>())
    {
        if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        tris += r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
    }
    log.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(fbx)} : {tris} tris, taille {b.size.x:0.00} × {b.size.y:0.00} × {b.size.z:0.00}, min {b.min}, max {b.max}");
    foreach (var t in inst.GetComponentsInChildren<Transform>())
    {
        var mr = t.GetComponent<MeshRenderer>();
        string mats = mr ? string.Join(",", mr.sharedMaterials.Select(m => m ? m.name : "null")) : "-";
        log.AppendLine($"   {t.name} pos {t.localPosition} rot {t.localEulerAngles} scale {t.localScale} mats [{mats}]");
    }
    Object.DestroyImmediate(inst);
}

// ------------------------------------------------------------------ Prefabs ramassables + données (étape 3 du plan)
// (code, nom affiché, classe, dégâts, masse, collider "box" | "capsule")
// Dégâts provisoires (plan §3, Combat_Attaque) : Bâton 1, Pelle 2, Fourche 3.
var items = new (string code, string nom, string kind, int damage, float mass, string col)[]
{
    ("Shovel", "Pelle", "weapon", 2, 3.5f, "box"),
    ("Stick", "Bâton", "weapon", 1, 2f, "capsule"),
    ("Pitchfork", "Fourche", "weapon", 3, 4f, "box"),
};
const string prefabDir = "Assets/Prefabs/Items/";
const string dataDir = "Assets/Data item/";
const string iconDir = "Assets/UI/Sprites/Items";
if (!AssetDatabase.IsValidFolder(iconDir)) AssetDatabase.CreateFolder("Assets/UI/Sprites", "Items");

foreach (var it in items)
{
    string fbx = $"{modelDir}/SM_Item_{it.code}.fbx";
    var mesh = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault();
    if (mesh == null) { log.AppendLine("Mesh absent : " + fbx); continue; }

    // Données (créées une fois, mises à jour ensuite : les références vers l'asset restent valides).
    string dataPath = $"{dataDir}Data_{it.code}.asset";
    var data = AssetDatabase.LoadAssetAtPath<ItemData>(dataPath);
    if (data == null)
    {
        data = it.kind == "weapon" ? ScriptableObject.CreateInstance<WeaponItem>() : ScriptableObject.CreateInstance<ItemData>();
        AssetDatabase.CreateAsset(data, dataPath);
    }
    data.nameObject = it.nom;
    if (data is WeaponItem w) w.damage = it.damage;

    // Prefab : la racine porte le mesh (le contour de GrabObject ne surligne que le renderer racine).
    var go = new GameObject($"P_Item_{it.code}");
    go.tag = "Ramassable";
    go.layer = 0;
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    var mb = mesh.bounds;
    if (it.col == "capsule")
    {
        var cc = go.AddComponent<CapsuleCollider>();
        cc.direction = 1;
        cc.center = mb.center;
        cc.height = mb.size.y;
        cc.radius = Mathf.Min(mb.size.x, mb.size.z) * 0.4f;
    }
    else
    {
        var bc = go.AddComponent<BoxCollider>();
        bc.center = mb.center;
        bc.size = mb.size;
    }
    var rb = go.AddComponent<Rigidbody>();
    rb.mass = it.mass;
    rb.interpolation = RigidbodyInterpolation.Interpolate;
    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // objets longs et fins : pas de traversée du sol au lâcher
    rb.angularDamping = it.col == "capsule" ? 4f : 0.5f;   // un bâton (capsule) roulait sur plus de 20 m une fois lâché
    go.AddComponent<ItemWorld>().donnees = data;
    var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{prefabDir}P_Item_{it.code}.prefab");
    Object.DestroyImmediate(go);
    data.prefab = prefab;

    // Icône (étape 4) : rendu orthographique du prefab, en diagonale, fond transparent, contour sombre.
    string iconPath = $"{iconDir}/Icon_{it.code}.png";
    System.IO.File.WriteAllBytes(iconPath, RenderIcon(prefab, 256));
    AssetDatabase.ImportAsset(iconPath);
    var ti = (TextureImporter)AssetImporter.GetAtPath(iconPath);
    ti.textureType = TextureImporterType.Sprite;
    ti.spriteImportMode = SpriteImportMode.Single;
    ti.alphaIsTransparency = true;
    ti.mipmapEnabled = false;
    ti.textureCompression = TextureImporterCompression.Uncompressed;
    ti.SaveAndReimport();
    data.icone = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
    EditorUtility.SetDirty(data);
    log.AppendLine($"P_Item_{it.code} + Data_{it.code} ({data.GetType().Name}, « {it.nom} ») + Icon_{it.code}");
}
AssetDatabase.SaveAssets();
return log.ToString();

// Rendu d'une icône : objet tourné en diagonale (bas-gauche -> haut-droite), vu en 3/4 depuis l'avant.
byte[] RenderIcon(GameObject prefab, int size)
{
    var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
    var obj = (GameObject)Object.Instantiate(prefab);
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, scene);
    obj.transform.rotation = Quaternion.Euler(0, 0, 45) * Quaternion.Euler(12, 28, 0);
    var r = obj.GetComponent<Renderer>();
    var bb = r.bounds;
    var lgo = new GameObject("Sun");
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lgo, scene);
    var l = lgo.AddComponent<Light>();
    l.type = LightType.Directional;
    l.intensity = 1.9f;
    lgo.transform.rotation = Quaternion.Euler(35, 160, 0);
    var fill = new GameObject("Fill");
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fill, scene);
    var l2 = fill.AddComponent<Light>();
    l2.type = LightType.Directional;
    l2.intensity = 0.8f;
    fill.transform.rotation = Quaternion.Euler(-20, 220, 0);
    var cgo = new GameObject("Cam");
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cgo, scene);
    var cam = cgo.AddComponent<Camera>();
    cam.scene = scene;
    cam.clearFlags = CameraClearFlags.SolidColor;
    cam.orthographic = true;
    cam.orthographicSize = Mathf.Max(bb.size.x, bb.size.y) * 0.5f / 0.86f;
    cam.nearClipPlane = 0.1f;
    cam.farClipPlane = 50f;
    cam.transform.position = bb.center + Vector3.forward * 20f;
    cam.transform.rotation = Quaternion.Euler(0, 180, 0);
    // L'URP ne garde pas l'alpha du fond : on rend sur fond noir puis sur fond blanc et on en déduit la transparence.
    Color32[] Shot(Color bg)
    {
        cam.backgroundColor = bg;
        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        t.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        var res = t.GetPixels32();
        Object.DestroyImmediate(t);
        return res;
    }
    var onBlack = Shot(Color.black);
    var onWhite = Shot(Color.white);
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
    Color bB = onBlack[0], bW = onWhite[0];          // fonds réellement obtenus (coin de l'image)
    float span = Mathf.Max(0.05f, ((bW.r - bB.r) + (bW.g - bB.g) + (bW.b - bB.b)) / 3f);
    var px = new Color32[size * size];
    for (int i = 0; i < px.Length; i++)
    {
        Color cb = onBlack[i], cw = onWhite[i];
        float a = Mathf.Clamp01(1f - ((cw.r - cb.r) + (cw.g - cb.g) + (cw.b - cb.b)) / 3f / span);
        if (a < 0.06f) a = 0f;                         // bruit du rendu sur le fond
        Color c = a > 0.004f ? new Color((cb.r - (1 - a) * bB.r) / a, (cb.g - (1 - a) * bB.g) / a, (cb.b - (1 - a) * bB.b) / a, a) : new Color(0, 0, 0, 0);
        px[i] = c;
    }
    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

    // Contour sombre épais autour de la silhouette (lisibilité dans un emplacement de la barre).
    var outPx = (Color32[])px.Clone();
    const int rad = 4;
    var ink = new Color32(38, 26, 20, 255);
    for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            if (px[y * size + x].a > 128) continue;
            float best = 0f;
            for (int dy = -rad; dy <= rad; dy++)
                for (int dx = -rad; dx <= rad; dx++)
                {
                    int xx = x + dx, yy = y + dy;
                    if (xx < 0 || yy < 0 || xx >= size || yy >= size || dx * dx + dy * dy > rad * rad) continue;
                    best = Mathf.Max(best, px[yy * size + xx].a / 255f);
                }
            if (best > 0f)
            {
                var c = px[y * size + x];
                float a = Mathf.Clamp01(best * 1.5f);
                outPx[y * size + x] = new Color32(ink.r, ink.g, ink.b, (byte)Mathf.Max(c.a, a * 255));
            }
        }
    tex.SetPixels32(outPx);
    var png = tex.EncodeToPNG();
    Object.DestroyImmediate(tex);
    return png;
}
