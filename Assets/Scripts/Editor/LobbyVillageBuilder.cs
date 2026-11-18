using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Outil éditeur : assemble le village du Lobby d'après ArtSource/Lobby/lobby_placement.json
/// (relevé du concept art, voir GameDesign/03_Contenu/Niveaux/Lobby_Plan_De_Production.md).
/// - « Poser le village » : crée l'objet racine LobbyVillage. Chaque élément utilise son prefab
///   (Assets/Prefabs/Environment/Village/) s'il existe, sinon une maquette en cubes aux dimensions du relevé.
/// - « Peindre les chemins » : repeint la couche de terre du terrain du Lobby (chemins, place, cour de la forge).
/// - « Éclaircir les arbres » : retire les sapins du terrain qui gênent, ajoute les sapins « héros » et densifie la forêt de l'Est.
/// - « Photo de contrôle » : capture la scène depuis la caméra du concept art, pour comparer.
/// Repère du JSON : centre du Lobby = origine, +X = Est, +Z = Nord, mètres.
/// </summary>
public static class LobbyVillageBuilder
{
    private const string JsonPath = "ArtSource/Lobby/lobby_placement.json";
    private const string PrefabFolder = "Assets/Prefabs/Environment/Village";
    private const string MaterialFolder = "Assets/Art/Materials/Environment/Village";
    private const string BlockoutFolder = MaterialFolder + "/Blockout";
    private const string ControlPhotoPath = "GameDesign/99_Attachments/Lobby/Lobby_Controle_Unity.jpg";
    private const string RootName = "LobbyVillage";

    // Marges de l'éclaircie des arbres (mètres).
    private const float TreeMarginBuilding = 7f;
    private const float TreeMarginPath = 3f;
    private const float TreeMarginFence = 4f;
    /// <summary>Part du rayon du houppier gardée entre le tronc et le bord d'un chemin (0,75 : les branches frôlent le bord).</summary>
    private const float TreeCanopyOverPath = 0.75f;
    /// <summary>Distance maximale (m) dont un arbre est déplacé pour dégager un chemin ; au-delà, il est retiré.</summary>
    private const float TreeRelocateMax = 14f;
    /// <summary>Écart minimal (m) entre un arbre déplacé et ses voisins.</summary>
    private const float TreeMinSpacing = 6f;
    // Largeur du fondu terre / herbe au bord des chemins (mètres).
    private const float PathEdgeSoftness = 2.5f;

    /// <summary>Type du relevé -> nom du prefab attendu dans PrefabFolder.</summary>
    private static readonly Dictionary<string, string> PrefabByType = new Dictionary<string, string>
    {
        { "Maison_S", "P_House_S" }, { "Maison_M", "P_House_M" }, { "Maison_L", "P_House_L" },
        { "Grange", "P_Barn" }, { "Tente_Medecin", "P_Tent_Medic" }, { "Potager", "P_Garden_Bed" },
        { "Forge_Four", "P_Forge_Kiln" }, { "Forge_Fourneau", "P_Forge_Furnace" }, { "Forge_Plancher", "P_Forge_Deck" },
        { "Etal_Simple", "P_Stall_Simple" }, { "Etal_Double", "P_Stall_Double" }, { "Etal_Appentis", "P_Stall_LeanTo" },
        { "Etal_Raye", "P_Stall_Striped" }, { "Feu_De_Camp", "P_Campfire" },
        { "Caisse", "P_Crate_01" }, { "Tonneau", "P_Barrel" }, { "Tonneaux_Couches", "P_Barrel_Stack" },
        { "Tas_De_Bois", "P_Woodpile" }, { "Souche", "P_Stump" }, { "Billot", "P_ChoppingBlock" }, { "Rondin", "P_LogRound" },
        { "Potence_Lanterne", "P_LanternPost" }, { "Panneau", "P_Signpost" }, { "Panonceau", "P_Sign_Small" },
        { "Auge", "P_Trough" }, { "Mangeoire", "P_HayRack" }, { "Botte_De_Foin", "P_HayBale" }, { "Tas_De_Foin", "P_HayPile" },
        { "Treteau", "P_Sawhorse" }, { "Ratelier", "P_ToolRack" }, { "Enclume", "P_Anvil" },
        { "Brouette", "P_Wheelbarrow" }, { "Charrette", "P_Cart" }, { "Panier", "P_Basket" },
        { "Sac", "P_Sack" }, { "Pot", "P_Pot" }, { "Plate_Bande", "P_Garden_Bed" },
    };

    /// <summary>Couleur de toit du relevé -> matériau de toit (kit maison) et couleur de maquette.</summary>
    private static readonly Dictionary<string, (string material, Color color)> Roofs = new Dictionary<string, (string, Color)>
    {
        { "rouge", ("M_Roof_Red", new Color(0.77f, 0.24f, 0.2f)) },
        { "bleu", ("M_Roof_Blue", new Color(0.28f, 0.42f, 0.7f)) },
        { "jaune", ("M_Roof_Yellow", new Color(0.89f, 0.75f, 0.27f)) },
        { "brun", ("M_Roof_Wood", new Color(0.59f, 0.38f, 0.22f)) },
        { "gris ardoise", ("M_Roof_Slate", new Color(0.33f, 0.35f, 0.41f)) },
        { "toile blanche", ("M_Canvas_White", new Color(0.93f, 0.91f, 0.86f)) },
        { "tole bleue", ("M_Forge_Metal", new Color(0.36f, 0.38f, 0.47f)) },
        { "terre", ("M_Soil", new Color(0.44f, 0.31f, 0.2f)) },
    };

    /// <summary>Taille (largeur, hauteur, profondeur) et forme des accessoires en maquette.</summary>
    private static readonly Dictionary<string, (Vector3 size, bool round)> PropBlockout = new Dictionary<string, (Vector3, bool)>
    {
        { "Caisse", (new Vector3(3f, 3f, 3f), false) }, { "Tonneau", (new Vector3(2.5f, 3.5f, 2.5f), true) },
        { "Tonneaux_Couches", (new Vector3(8f, 4f, 5f), false) }, { "Tas_De_Bois", (new Vector3(6f, 3f, 3f), false) },
        { "Souche", (new Vector3(2.5f, 2f, 2.5f), true) }, { "Billot", (new Vector3(2.5f, 2.6f, 2.5f), true) },
        { "Rondin", (new Vector3(4f, 1f, 4f), true) }, { "Potence_Lanterne", (new Vector3(1f, 9f, 1f), false) },
        { "Panneau", (new Vector3(1f, 6f, 1f), false) }, { "Panonceau", (new Vector3(2f, 2.5f, 0.5f), false) },
        { "Auge", (new Vector3(6f, 2f, 2f), false) }, { "Mangeoire", (new Vector3(5f, 3f, 2.2f), false) },
        { "Botte_De_Foin", (new Vector3(3f, 1.7f, 1.7f), false) }, { "Tas_De_Foin", (new Vector3(4f, 2.2f, 4f), true) },
        { "Treteau", (new Vector3(5f, 3f, 2f), false) }, { "Ratelier", (new Vector3(6f, 6f, 2f), false) },
        { "Enclume", (new Vector3(3f, 3f, 2f), false) }, { "Brouette", (new Vector3(5f, 3f, 2.5f), false) },
        { "Charrette", (new Vector3(3.2f, 3.8f, 5.7f), false) }, { "Panier", (new Vector3(2f, 2f, 2f), true) },
        { "Sac", (new Vector3(1.8f, 2f, 1.8f), true) }, { "Pot", (new Vector3(1.8f, 1.6f, 1.8f), true) },
        { "Plate_Bande", (new Vector3(8f, 0.4f, 12f), false) },
    };

    #region --- MENUS ---

    [MenuItem("TeamTimo/Lobby/Village/1. Poser le village (prefabs ou maquette)")]
    public static void BuildMenu()
    {
        if (GameObject.Find(RootName) != null &&
            !EditorUtility.DisplayDialog("LobbyVillage existe déjà",
                "Recréer LobbyVillage effacera les modifications faites à la main dans cet objet. Continuer ?", "Recréer", "Annuler"))
            return;
        Debug.Log(Build());
    }

    [MenuItem("TeamTimo/Lobby/Village/2. Peindre les chemins du Lobby")]
    public static void PaintMenu()
    {
        if (!EditorUtility.DisplayDialog("Peindre les chemins",
                "La couche de terre du terrain du Lobby sera entièrement repeinte d'après le relevé. Continuer ?", "Peindre", "Annuler"))
            return;
        Debug.Log(PaintPaths());
    }

    [MenuItem("TeamTimo/Lobby/Village/3. Éclaircir le village et densifier la forêt")]
    public static void TreesMenu()
    {
        Debug.Log(ThinTrees());
    }

    [MenuItem("TeamTimo/Lobby/Village/3 bis. Aplanir le terrain sous la grange et la tente")]
    public static void FlattenMenu()
    {
        Debug.Log(FlattenInteriors());
    }

    [MenuItem("TeamTimo/Lobby/Village/4. Photo de contrôle (caméra du concept)")]
    public static void PhotoMenu()
    {
        Debug.Log(CaptureControlPhoto(ControlPhotoPath));
    }

    #endregion

    #region --- VILLAGE ---

    /// <summary>Crée (ou recrée) LobbyVillage dans la scène active. Retourne un résumé.</summary>
    public static string Build()
    {
        JObject data = LoadLayout(out string error);
        if (data == null) return error;
        Terrain terrain = FindLobbyTerrain();

        GameObject old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        GameObject root = new GameObject(RootName);
        Transform buildings = NewChild(root.transform, "Buildings");
        Transform stalls = NewChild(root.transform, "Stalls");
        Transform fences = NewChild(root.transform, "Fences");
        Transform props = NewChild(root.transform, "Props");

        int prefabCount = 0, blockoutCount = 0;

        foreach (JToken b in data["batiments"])
        {
            if (PlaceStructure(b, buildings, terrain)) prefabCount++; else blockoutCount++;
        }
        foreach (JToken e in data["elements"])
        {
            Transform parent = ((string)e["type"]).StartsWith("Etal") || (string)e["type"] == "Feu_De_Camp" ? stalls : buildings;
            if (PlaceStructure(e, parent, terrain)) prefabCount++; else blockoutCount++;
        }

        float fenceLength = 0f;
        foreach (JToken f in data["clotures"]) fenceLength += PlaceFence(f, fences, terrain);

        foreach (JToken p in data["accessoires"])
        {
            if (PlaceProp(p, props, terrain, data)) prefabCount++; else blockoutCount++;
        }

        DecoratePlaza(root.transform, terrain);
        DecorateVillage(root.transform, terrain, data);
        ClearDetailObstacles(terrain, data);
        EditorSceneManager.MarkSceneDirty(root.scene);
        return $"LobbyVillage : {prefabCount} prefabs, {blockoutCount} maquettes en cubes, {fenceLength:0} m de clôtures.";
    }

    [MenuItem("TeamTimo/Lobby/Village/1 bis. Remplacer les maquettes par les prefabs disponibles")]
    public static void ReplaceMenu()
    {
        Debug.Log(ReplaceBlockouts());
    }

    /// <summary>
    /// Remplace uniquement les maquettes de bâtiments / étals dont le prefab existe maintenant, sans toucher
    /// au reste de LobbyVillage (retouches à la main conservées). Crée le village s'il n'existe pas.
    /// refreshPrefabs : repose aussi les éléments déjà en prefab (après avoir recréé un prefab).
    /// </summary>
    public static string ReplaceBlockouts(bool refreshPrefabs = false)
    {
        JObject data = LoadLayout(out string error);
        if (data == null) return error;
        GameObject root = GameObject.Find(RootName);
        if (root == null) return Build();
        Terrain terrain = FindLobbyTerrain();

        var blockouts = new Dictionary<string, Transform>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.EndsWith(" (maquette)") || (refreshPrefabs && (PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) ||
                                                                        t.name.EndsWith("_Potager"))))
                blockouts[t.name] = t;

        int replaced = 0;
        foreach (JToken e in data["batiments"].Concat(data["elements"]))
        {
            string id = (string)e["id"], type = (string)e["type"];
            if (LoadPrefab(type) == null) continue;
            if (!blockouts.TryGetValue($"{id}_{type} (maquette)", out Transform old) &&
                !blockouts.TryGetValue($"{id}_{type}", out old)) continue;
            Transform parent = old.parent;
            int index = old.GetSiblingIndex();
            Object.DestroyImmediate(old.gameObject);
            PlaceStructure(e, parent, terrain);
            parent.GetChild(parent.childCount - 1).SetSiblingIndex(index);
            replaced++;
        }
        // Clôtures : reposées en entier si le prefab de travée existe (les maquettes, ou toutes si refreshPrefabs).
        if (AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/P_Fence_Wood.prefab") != null)
        {
            Transform fencesRoot = root.transform.Find("Fences");
            foreach (JToken f in data["clotures"])
            {
                string id = (string)f["id"];
                Transform old = fencesRoot != null ? fencesRoot.Find(id + " (maquette)") : null;
                if (old == null && refreshPrefabs && fencesRoot != null) old = fencesRoot.Find(id);
                if (old == null) continue;
                int index = old.GetSiblingIndex();
                Object.DestroyImmediate(old.gameObject);
                PlaceFence(f, fencesRoot, terrain);
                fencesRoot.GetChild(fencesRoot.childCount - 1).SetSiblingIndex(index);
                replaced++;
            }
        }
        // Accessoires (après les bâtiments : certains se posent sur le plancher de la forge).
        foreach (JToken p in data["accessoires"])
        {
            string id = (string)p["id"], type = (string)p["type"];
            if (LoadPrefab(type) == null) continue;
            if (!blockouts.TryGetValue($"{id}_{type} (maquette)", out Transform old) &&
                !blockouts.TryGetValue($"{id}_{type}", out old)) continue;
            Transform parent = old.parent;
            int index = old.GetSiblingIndex();
            Object.DestroyImmediate(old.gameObject);
            PlaceProp(p, parent, terrain, data);
            parent.GetChild(parent.childCount - 1).SetSiblingIndex(index);
            replaced++;
        }
        DecoratePlaza(root.transform, terrain);
        DecorateVillage(root.transform, terrain, data);
        ClearDetailObstacles(terrain, data);
        EditorSceneManager.MarkSceneDirty(root.scene);
        return $"LobbyVillage : {replaced} éléments (re)posés avec leur prefab.";
    }

    /// <summary>Pose un bâtiment / étal. Retourne vrai si un prefab a été utilisé, faux si c'est une maquette.</summary>
    private static bool PlaceStructure(JToken e, Transform parent, Terrain terrain)
    {
        string id = (string)e["id"], type = (string)e["type"];
        Vector3 pos = Ground(terrain, (float)e["x"], (float)e["z"]);
        Quaternion rot = Quaternion.Euler(0f, (float)e["yaw_deg"], 0f);
        string roof = (string)e["toit"];

        GameObject prefab = LoadPrefab(type);
        if (prefab != null && type == "Potager")
        {
            // Potager : planches de culture de 12 × 12 m mises bout à bout sur la longueur du relevé.
            GameObject garden = new GameObject($"{id}_{type}");
            garden.transform.SetParent(parent, false);
            garden.transform.SetPositionAndRotation(pos, rot);
            float gw = (float)e["largeur_facade_m"], gd = (float)e["profondeur_m"];
            int n = Mathf.Max(1, Mathf.RoundToInt(gd / 12f));
            float len = gd / n;
            for (int j = 0; j < n; j++)
            {
                GameObject bed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, garden.transform);
                float z = -gd / 2f + len * (j + 0.5f);
                Vector3 world = garden.transform.TransformPoint(new Vector3(0f, 0f, z));
                bed.transform.SetPositionAndRotation(Ground(terrain, world.x, world.z), rot);
                bed.transform.localScale = new Vector3(gw / 12f, 1f, (len - 1f) / 12f);
            }
            if (e["options"] != null && e["options"].Any(o => (string)o == "panonceau"))
            {
                GameObject signPf = LoadPrefab("Panonceau");
                if (signPf != null)
                {
                    float signZ = n > 1 ? -gd / 2f + len : 0f;
                    Vector3 signPos = garden.transform.TransformPoint(new Vector3(0f, 0f, signZ));
                    GameObject sign = (GameObject)PrefabUtility.InstantiatePrefab(signPf, garden.transform);
                    sign.name = $"{id}_Panonceau";
                    sign.transform.SetPositionAndRotation(Ground(terrain, signPos.x, signPos.z), Quaternion.Euler(0f, 260f, 0f));
                    sign.transform.localScale = Vector3.one * 1.25f;
                }
            }
            return true;
        }
        if (prefab != null)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = $"{id}_{type}";
            if (type.StartsWith("Maison") || type == "Grange" || type == "Tente_Medecin") rot = DoorRotation(e, pos, rot, prefab);
            go.transform.SetPositionAndRotation(pos, rot);
            ApplyHouseKit(go, roof, e["options"]);
            return true;
        }

        GameObject holder = new GameObject($"{id}_{type} (maquette)");
        holder.transform.SetParent(parent, false);
        holder.transform.SetPositionAndRotation(pos, rot);
        float w = (float)e["largeur_facade_m"], d = (float)e["profondeur_m"], h = (float)e["hauteur_m"];
        Color roofColor = roof != null && Roofs.TryGetValue(roof, out var r) ? r.color : new Color(0.69f, 0.48f, 0.27f);

        if (type.StartsWith("Maison") || type == "Grange" || type == "Tente_Medecin")
        {
            bool gableFront = (bool?)e["pignon_en_facade"] ?? true;
            Color wall = type == "Grange" ? new Color(0.72f, 0.2f, 0.17f) : type == "Tente_Medecin" ? roofColor : new Color(0.93f, 0.86f, 0.7f);
            BlockoutGabled(holder.transform, w, d, h, gableFront, wall, roofColor);
            BlockNavMesh(holder, w, d, h);
        }
        else if (type == "Forge_Four" || type == "Forge_Fourneau")
        {
            // Voûte en berceau : un socle + un cylindre couché le long de la profondeur.
            float radius = w / 2f, baseH = Mathf.Max(1f, h - radius);
            Box(holder.transform, "Socle", new Vector3(0f, baseH / 2f - 1f, 0f), new Vector3(w, baseH + 2f, d), new Color(0.5f, 0.3f, 0.24f));
            GameObject vault = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vault.name = "Voute";
            vault.transform.SetParent(holder.transform, false);
            vault.transform.localPosition = new Vector3(0f, baseH, 0f);
            vault.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            vault.transform.localScale = new Vector3(w, d / 2f, w);
            Object.DestroyImmediate(vault.GetComponent<Collider>());
            vault.GetComponent<MeshRenderer>().sharedMaterial = BlockoutMaterial(type == "Forge_Four" ? new Color(0.36f, 0.38f, 0.47f) : new Color(0.42f, 0.27f, 0.22f));
            BlockNavMesh(holder, w, d, h);
        }
        else if (type.StartsWith("Etal"))
        {
            // 4 poteaux, un toit, un comptoir en façade (on peut passer dessous).
            Color wood = new Color(0.62f, 0.42f, 0.24f);
            Color top = type == "Etal_Raye" ? new Color(0.8f, 0.25f, 0.22f) : new Color(0.5f, 0.32f, 0.18f);
            // Toit sans collider : sinon le NavMesh crée un îlot praticable dessus.
            Box(holder.transform, "Toit", new Vector3(0f, h - 0.4f, 0f), new Vector3(w + 1f, 0.8f, d + 1f), top, false);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Box(holder.transform, "Poteau", new Vector3(sx * (w / 2f - 0.5f), h / 2f - 1f, sz * (d / 2f - 0.5f)), new Vector3(0.9f, h + 2f, 0.9f), wood);
            if (type != "Etal_Appentis")
                Box(holder.transform, "Comptoir", new Vector3(0f, 1.3f, d / 2f - 1f), new Vector3(w - 1.5f, 3.4f, 1.6f), wood);
        }
        else if (type == "Feu_De_Camp")
        {
            Cylinder(holder.transform, "Foyer", w, 1f, new Color(0.95f, 0.5f, 0.12f));
        }
        else
        {
            // Plancher, potager : simple dalle.
            Color c = type == "Potager" ? new Color(0.44f, 0.31f, 0.2f) : new Color(0.36f, 0.3f, 0.3f);
            Box(holder.transform, "Dalle", new Vector3(0f, h / 2f - 0.5f, 0f), new Vector3(w, h + 1f, d), c);
        }
        return false;
    }

    /// <summary>Maquette d'un bâtiment à toit à deux pans : murs + toit (cube tourné de 45° le long du faîtage).</summary>
    private static void BlockoutGabled(Transform parent, float w, float d, float h, bool gableFront, Color wallColor, Color roofColor)
    {
        const float overhang = 1.5f;
        float span = gableFront ? w : d;                    // portée du toit (perpendiculaire au faîtage)
        float ridge = (gableFront ? d : w) + overhang;      // longueur du faîtage
        float wallH = Mathf.Max(h * 0.3f, h - (span + overhang) / 2f);
        Box(parent, "Murs", new Vector3(0f, wallH / 2f - 1f, 0f), new Vector3(w, wallH + 2f, d), wallColor);

        float side = (span + overhang) / Mathf.Sqrt(2f);
        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "Toit";
        roof.transform.SetParent(parent, false);
        roof.transform.localPosition = new Vector3(0f, wallH, 0f);
        roof.transform.localRotation = gableFront ? Quaternion.Euler(0f, 0f, 45f) : Quaternion.Euler(45f, 0f, 0f);
        roof.transform.localScale = gableFront ? new Vector3(side, side, ridge) : new Vector3(ridge, side, side);
        Object.DestroyImmediate(roof.GetComponent<Collider>());
        roof.GetComponent<MeshRenderer>().sharedMaterial = BlockoutMaterial(roofColor);
    }

    /// <summary>
    /// Bâtiment plein : volume « Not Walkable » sur toute l'emprise. Sans lui, le NavMesh garde un îlot
    /// praticable sous la boîte (elle est vue comme creuse) et un autre sur son sommet.
    /// </summary>
    private static void BlockNavMesh(GameObject holder, float w, float d, float h)
    {
        NavMeshModifierVolume volume = holder.AddComponent<NavMeshModifierVolume>();
        volume.center = new Vector3(0f, h / 2f, 0f);
        volume.size = new Vector3(w, h + 6f, d);
        volume.area = 1;    // zone intégrée « Not Walkable »
    }

    /// <summary>Pénalité (m) quand la porte impose de tourner le faîtage par rapport au concept.</summary>
    private const float RoofTurnPenalty = 6f;

    /// <summary>
    /// Maisons, grange, tente : la porte est sur le pignon avant (+Z du prefab). Parmi les 4 quarts de tour,
    /// garde celui dont la porte est la plus proche d'un chemin ou de la place ; tourner le faîtage par
    /// rapport au concept (pignon_en_facade) coûte RoofTurnPenalty mètres.
    /// </summary>
    private static Quaternion DoorRotation(JToken e, Vector3 pos, Quaternion rot, GameObject prefab)
    {
        JToken data = e.Root;
        var paths = new List<(List<Vector2> pts, float halfWidth)>();
        foreach (JToken s in data["chemins"]) paths.Add((ReadPoints(s["points"]), (float)s["largeur_m"] / 2f));
        var zones = new List<List<Vector2>>();
        foreach (JToken z in data["zones_terre"]) zones.Add(ReadPoints(z["polygone"]));
        // Distance du pivot au devant du bâtiment (corps et toit uniquement : les options comme perron ne décalent pas le test).
        float halfDepth = 6f;
        foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null && (mf.name == "Body" || mf.name == "Roof"))
                halfDepth = Mathf.Max(halfDepth, mf.sharedMesh.bounds.max.z);
        bool gableFront = (bool?)e["pignon_en_facade"] ?? true;

        float DistanceToDirt(Vector2 p)
        {
            float best = float.MaxValue;
            foreach (var path in paths) best = Mathf.Min(best, DistanceToPolyline(p, path.pts) - path.halfWidth);
            foreach (List<Vector2> zone in zones)
                best = Mathf.Min(best, InsidePolygon(p, zone) ? 0f : DistanceToPolyline(p, zone, true));
            return Mathf.Max(0f, best);
        }

        Quaternion bestRot = rot;
        float bestCost = float.MaxValue;
        for (int k = 0; k < 4; k++)
        {
            Quaternion q = rot * Quaternion.Euler(0f, 90f * k, 0f);
            Vector3 door = pos + q * Vector3.forward * (halfDepth + 3f);
            bool ridgeLikeConcept = (k % 2 == 0) == gableFront;
            float cost = DistanceToDirt(new Vector2(door.x, door.z)) + (ridgeLikeConcept ? 0f : RoofTurnPenalty);
            if (cost < bestCost - 0.01f) { bestCost = cost; bestRot = q; }
        }
        return bestRot;
    }

    /// <summary>Kit maison : matériau du toit et options (enfants du prefab nommés comme l'option, activés ou non).</summary>
    private static void ApplyHouseKit(GameObject house, string roof, JToken options)
    {
        if (roof != null && Roofs.TryGetValue(roof, out var r))
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{r.material}.mat");
            if (mat != null)
            {
                foreach (Transform t in house.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Roof" && t.TryGetComponent(out MeshRenderer renderer))
                        renderer.sharedMaterial = mat;
            }
        }
        Transform optionsT = house.transform.Find("Options");
        if (optionsT == null) return;
        var wanted = new HashSet<string>();
        if (options != null)
        {
            foreach (JToken o in options)
            {
                string opt = (string)o;
                wanted.Add(opt == "auvent" ? "auvent_toile" : opt);
            }
        }
        foreach (Transform child in optionsT) child.gameObject.SetActive(wanted.Contains(child.name));
    }

    /// <summary>Pose une clôture le long d'une polyligne. Retourne sa longueur.</summary>
    private static float PlaceFence(JToken f, Transform parent, Terrain terrain)
    {
        string id = (string)f["id"];
        bool white = (string)f["style"] == "blanc";
        GameObject segmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/P_Fence_Wood.prefab");
        GameObject brokenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/P_Fence_Broken.prefab");
        GameObject postPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/P_Fence_Post.prefab");
        GameObject gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/P_Fence_Gate.prefab");
        Material whiteMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_Fence_White.mat");
        int bayIndex = 0;
        Color color = white ? new Color(0.95f, 0.95f, 0.92f) : new Color(0.4f, 0.26f, 0.14f);
        Transform holder = NewChild(parent, segmentPrefab != null ? id : id + " (maquette)");
        List<Vector2> pts = ReadPoints(f["points"]);
        float total = 0f;
        const float bay = 8f, height = 3f;

        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 a = pts[i], b = pts[i + 1];
            float len = Vector2.Distance(a, b);
            if (len < 0.5f) continue;
            total += len;
            Vector2 dir = (b - a) / len;
            Quaternion rot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
            if (segmentPrefab != null && gatePrefab != null && id == "K3" && i == 0 && len > 10f)
            {
                const float gateLen = 5.2f;
                Vector2 gMid = a + dir * (gateLen * 0.5f);
                GameObject gate = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab, holder);
                gate.transform.SetPositionAndRotation(Ground(terrain, gMid.x, gMid.y), rot);
                gate.transform.localScale = new Vector3(1f, 1f, gateLen / 5f);
                a += dir * gateLen;
                len -= gateLen;
            }
            int bays = Mathf.Max(1, Mathf.RoundToInt(len / bay));
            float step = len / bays;
            for (int k = 0; k < bays; k++)
            {
                Vector2 mid = a + dir * (step * (k + 0.5f));
                Vector3 pos = Ground(terrain, mid.x, mid.y);
                if (segmentPrefab != null)
                {
                    // Une travée sur FenceBrokenEvery est cassée (choix stable, tiré de l'identifiant).
                    bool broken = brokenPrefab != null && !white &&
                                  Mathf.Abs(Animator.StringToHash($"{id}_{bayIndex}")) % FenceBrokenEvery == 0;
                    bayIndex++;
                    GameObject seg = (GameObject)PrefabUtility.InstantiatePrefab(broken ? brokenPrefab : segmentPrefab, holder);
                    seg.transform.SetPositionAndRotation(pos, rot);
                    seg.transform.localScale = new Vector3(1f, 1f, step / bay);
                    if (white && whiteMat != null) PaintWhite(seg, whiteMat);
                    // Le poteau de la travée est à son début : poteau seul au bout de la clôture.
                    if (i == pts.Count - 2 && k == bays - 1 && postPrefab != null)
                    {
                        GameObject post = (GameObject)PrefabUtility.InstantiatePrefab(postPrefab, holder);
                        post.transform.SetPositionAndRotation(Ground(terrain, b.x, b.y), rot);
                        if (white && whiteMat != null) PaintWhite(post, whiteMat);
                    }
                    continue;
                }
                GameObject bayGo = new GameObject("Travee");
                bayGo.transform.SetParent(holder, false);
                bayGo.transform.SetPositionAndRotation(pos, rot);
                BoxCollider col = bayGo.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, height / 2f, 0f);
                col.size = new Vector3(0.6f, height + 2f, step);
                Box(bayGo.transform, "Lisse", new Vector3(0f, 1.2f, 0f), new Vector3(0.3f, 0.5f, step), color, false);
                Box(bayGo.transform, "Lisse", new Vector3(0f, 2.4f, 0f), new Vector3(0.3f, 0.5f, step), color, false);
                Box(bayGo.transform, "Poteau", new Vector3(0f, height / 2f - 0.5f, -step / 2f), new Vector3(0.6f, height + 1f, 0.6f), color, false);
                if (k == bays - 1)
                    Box(bayGo.transform, "Poteau", new Vector3(0f, height / 2f - 0.5f, step / 2f), new Vector3(0.6f, height + 1f, 0.6f), color, false);
            }
        }
        return total;
    }

    /// <summary>Une travée de clôture en bois sur N est cassée (P_Fence_Broken).</summary>
    private const int FenceBrokenEvery = 9;

    /// <summary>Clôture blanche : le matériau blanc remplace le bois (le bois de bout des poteaux reste).</summary>
    private static void PaintWhite(GameObject go, Material white)
    {
        foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>())
        {
            Material[] mats = mr.sharedMaterials;
            for (int m = 0; m < mats.Length; m++)
                if (mats[m] != null && mats[m].name == "M_Village_Wood") mats[m] = white;
            mr.sharedMaterials = mats;
        }
    }

    /// <summary>Pose un accessoire. Retourne vrai si un prefab a été utilisé.</summary>
    private static bool PlaceProp(JToken p, Transform parent, Terrain terrain, JObject data = null)
    {
        string id = (string)p["id"], type = (string)p["type"];
        float px = (float)p["x"], pz = (float)p["z"];
        if (data != null && type != "Rondin")
        {
            Vector2 pushed = PushOutOfPaths(data, new Vector2(px, pz), 2.2f);
            px = pushed.x;
            pz = pushed.y;
        }
        Vector3 pos = OnSupport(parent.root, Ground(terrain, px, pz));
        // Rotation stable d'un lancement à l'autre (tirée de l'identifiant), sauf Plate_Bande alignée avec les rondins.
        float yaw = type == "Plate_Bande" ? 290.6f : Mathf.Abs(Animator.StringToHash(id)) % 360;
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        GameObject prefab = LoadPrefab(type);
        if (type == "Caisse")
        {
            // 3 modèles de caisses (fermée, cagette haute, cagette plate), choix stable tiré de l'identifiant.
            GameObject variant = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabFolder}/P_Crate_0{1 + Mathf.Abs(Animator.StringToHash(id + "_modele")) % 3}.prefab");
            if (variant != null) prefab = variant;
        }
        else if (id == "P16")
        {
            // P16 est noté « Souche / billot » : on y pose le billot à hache à côté de la souche P15.
            GameObject block = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/P_ChoppingBlock.prefab");
            if (block != null) prefab = block;
        }
        if (prefab != null)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = $"{id}_{type}";
            go.transform.SetPositionAndRotation(pos, rot);
            if (type == "Plate_Bande") go.transform.localScale = new Vector3(7f / 12f, 1f, 1f);
            return true;
        }
        GameObject holder = new GameObject($"{id}_{type} (maquette)");
        holder.transform.SetParent(parent, false);
        holder.transform.SetPositionAndRotation(pos, rot);
        var shape = PropBlockout.TryGetValue(type, out var s) ? s : (new Vector3(3f, 3f, 3f), false);
        Color color = type == "Plate_Bande" ? new Color(0.44f, 0.31f, 0.2f) : new Color(0.55f, 0.38f, 0.22f);
        if (shape.Item2) Cylinder(holder.transform, "Volume", shape.Item1.x, shape.Item1.y, color);
        else Box(holder.transform, "Volume", new Vector3(0f, shape.Item1.y / 2f - 0.25f, 0f), shape.Item1 + new Vector3(0f, 0.5f, 0f), color);
        return false;
    }

    private static Vector2 PushOutOfPaths(JObject data, Vector2 p, float minClearance)
    {
        var paths = new List<(List<Vector2> pts, float halfWidth)>();
        foreach (JToken s in data["chemins"]) paths.Add((ReadPoints(s["points"]), (float)s["largeur_m"] / 2f));
        float Dist(Vector2 q)
        {
            float best = float.MaxValue;
            foreach (var path in paths) best = Mathf.Min(best, DistanceToPolyline(q, path.pts) - path.halfWidth);
            return best;
        }
        for (int iter = 0; iter < 12; iter++)
        {
            float d = Dist(p);
            if (d >= minClearance) break;
            const float h = 0.5f;
            var grad = new Vector2(Dist(p + Vector2.right * h) - Dist(p - Vector2.right * h),
                                   Dist(p + Vector2.up * h) - Dist(p - Vector2.up * h));
            if (grad.sqrMagnitude < 1e-6f) break;
            p += grad.normalized * Mathf.Min(1.5f, minClearance - d + 0.2f);
        }
        return p;
    }

    /// <summary>Types du relevé sur lesquels un accessoire se pose (au lieu du terrain) : le plancher de la forge.</summary>
    private static readonly string[] SupportTypes = { "Forge_Plancher" };

    /// <summary>
    /// Pose pos sur le dessus d'un support (plancher de la forge) s'il est dessus ou à moins de 4 m de son bord :
    /// le point est ramené à 2 m à l'intérieur du plateau (le relevé est précis à ±3 m), puis posé sur son collider.
    /// </summary>
    private static Vector3 OnSupport(Transform root, Vector3 pos)
    {
        Physics.SyncTransforms();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!SupportTypes.Any(s => t.name.EndsWith("_" + s))) continue;
            MeshFilter mf = t.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Bounds b = mf.sharedMesh.bounds;
            Vector3 local = t.InverseTransformPoint(pos);
            if (Mathf.Abs(local.x - b.center.x) > b.extents.x + 4f || Mathf.Abs(local.z - b.center.z) > b.extents.z + 4f) continue;
            local.x = Mathf.Clamp(local.x, b.min.x + 2f, b.max.x - 2f);
            local.z = Mathf.Clamp(local.z, b.min.z + 2f, b.max.z - 2f);
            pos = t.TransformPoint(local);
            var ray = new Ray(pos + Vector3.up * 30f, Vector3.down);
            foreach (Collider c in t.GetComponentsInChildren<Collider>())
                if (c.Raycast(ray, out RaycastHit hit, 60f)) pos.y = hit.point.y;
        }
        return pos;
    }

    #endregion

    #region --- TERRAIN : CHEMINS ---

    /// <summary>Repeint la couche de terre du terrain du Lobby d'après les chemins et zones de terre du relevé.</summary>
    public static string PaintPaths()
    {
        JObject data = LoadLayout(out string error);
        if (data == null) return error;
        Terrain terrain = FindLobbyTerrain();
        if (terrain == null) return "Terrain du Lobby introuvable (TerrainGenerator.manualCenterData).";
        TerrainGenerator generator = Object.FindFirstObjectByType<TerrainGenerator>();
        var so = new SerializedObject(generator);
        var grassLayer = so.FindProperty("grassLayer").objectReferenceValue as TerrainLayer;
        var dirtLayer = so.FindProperty("dirtLayer").objectReferenceValue as TerrainLayer;
        TerrainData td = terrain.terrainData;
        int grass = System.Array.IndexOf(td.terrainLayers, grassLayer);
        int dirt = System.Array.IndexOf(td.terrainLayers, dirtLayer);
        if (grass < 0 || dirt < 0) return "Les couches d'herbe et de terre du TerrainGenerator ne sont pas sur le terrain du Lobby.";

        var paths = new List<(List<Vector2> pts, float halfWidth)>();
        foreach (JToken s in data["chemins"]) paths.Add((ReadPoints(s["points"]), (float)s["largeur_m"] / 2f));
        var zones = new List<List<Vector2>>();
        foreach (JToken z in data["zones_terre"]) zones.Add(ReadPoints(z["polygone"]));

        Undo.RegisterCompleteObjectUndo(td, "Peindre les chemins du Lobby");
        int res = td.alphamapResolution, layers = td.alphamapLayers;
        float[,,] maps = new float[res, res, layers];
        Vector3 origin = terrain.transform.position, size = td.size;
        float painted = 0f;
        for (int iz = 0; iz < res; iz++)
        {
            for (int ix = 0; ix < res; ix++)
            {
                var p = new Vector2(origin.x + (ix + 0.5f) / res * size.x, origin.z + (iz + 0.5f) / res * size.z);
                // Distance signée au bord de la terre (négative = dans la terre).
                float dist = float.MaxValue;
                foreach (var path in paths) dist = Mathf.Min(dist, DistanceToPolyline(p, path.pts) - path.halfWidth);
                foreach (List<Vector2> zone in zones)
                {
                    float edge = DistanceToPolyline(p, zone, true);
                    dist = Mathf.Min(dist, InsidePolygon(p, zone) ? -edge : edge);
                }
                // Bord irrégulier : un bruit décale la limite de ±1,2 m.
                dist += (Mathf.PerlinNoise(p.x * 0.09f + 31.7f, p.y * 0.09f + 12.3f) - 0.5f) * 2.4f;
                float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-PathEdgeSoftness / 2f, PathEdgeSoftness / 2f, dist));
                maps[iz, ix, dirt] = w;
                maps[iz, ix, grass] = 1f - w;
                painted += w;
            }
        }
        td.SetAlphamaps(0, 0, maps);
        EditorUtility.SetDirty(td);

        // L'herbe suit la couche d'herbe : on la repeint pour dégager les chemins.
        var settings = so.FindProperty("grassSettings").objectReferenceValue as TerrainGrassSettings;
        if (settings != null) TerrainGrassTools.Paint(terrain, settings, grassLayer);
        ClearDetailObstacles(terrain, data);
        EditorUtility.SetDirty(terrain);
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        return $"Chemins peints sur « {terrain.name} » : {painted / (res * res) * 100f:0.0} % de terre ({paths.Count} chemins, {zones.Count} zones).";
    }

    #endregion

    #region --- TERRAIN : ARBRES ---

    /// <summary>Retire les sapins du terrain qui gênent (bâtiments, chemins, place, clôtures) et ajoute les sapins « héros ».</summary>
    public static string ThinTrees()
    {
        JObject data = LoadLayout(out string error);
        if (data == null) return error;
        Terrain terrain = FindLobbyTerrain();
        if (terrain == null) return "Terrain du Lobby introuvable (TerrainGenerator.manualCenterData).";
        TerrainData td = terrain.terrainData;
        Vector3 origin = terrain.transform.position, size = td.size;

        var footprints = new List<List<Vector2>>();
        foreach (JToken b in data["batiments"]) footprints.Add(ReadPoints(b["coins"]));
        foreach (JToken e in data["elements"]) footprints.Add(ReadPoints(e["coins"]));
        var paths = new List<(List<Vector2> pts, float halfWidth)>();
        foreach (JToken s in data["chemins"]) paths.Add((ReadPoints(s["points"]), (float)s["largeur_m"] / 2f));
        var zones = new List<List<Vector2>>();
        foreach (JToken z in data["zones_terre"]) zones.Add(ReadPoints(z["polygone"]));
        var fences = new List<List<Vector2>>();
        foreach (JToken f in data["clotures"]) fences.Add(ReadPoints(f["points"]));
        List<Vector2> heroes = ReadPoints(data["arbres_heros"]);

        // Clairières de la forêt (x, z, rayon) : aucun arbre dedans.
        var clearings = new List<Vector3>();
        if (data["foret"] != null)
            foreach (JToken c in data["foret"]["clairieres"]) clearings.Add(new Vector3((float)c[0], (float)c[1], (float)c[2]));

        // Rayon du houppier de chaque modèle (échelle 1), pour l'écart aux chemins.
        var canopy = new float[td.treePrototypes.Length];
        for (int i = 0; i < canopy.Length; i++)
        {
            float r = 4f;
            GameObject prefab = td.treePrototypes[i].prefab;
            if (prefab != null)
                foreach (Renderer rd in prefab.GetComponentsInChildren<Renderer>())
                    r = Mathf.Max(r, Mathf.Max(rd.bounds.extents.x, rd.bounds.extents.z));
            canopy[i] = r;
        }
        float PathMargin(int prototype, float scale) =>
            Mathf.Max(TreeMarginPath, canopy[Mathf.Clamp(prototype, 0, canopy.Length - 1)] * scale * TreeCanopyOverPath);

        // Distance au bord de terre le plus proche (chemins, place, cour de la forge), négative dedans.
        float DirtClearance(Vector2 p)
        {
            float best = float.MaxValue;
            foreach (var path in paths) best = Mathf.Min(best, DistanceToPolyline(p, path.pts) - path.halfWidth);
            foreach (List<Vector2> zone in zones)
            {
                float d = DistanceToPolyline(p, zone, true);
                best = Mathf.Min(best, InsidePolygon(p, zone) ? -d : d);
            }
            return best;
        }

        bool Blocked(Vector2 p)
        {
            if (Mathf.Abs(p.x) > 190f || Mathf.Abs(p.y) > 190f) return true;   // contre les murs du Lobby
            foreach (Vector3 c in clearings)
                if (Vector2.Distance(p, new Vector2(c.x, c.y)) < c.z) return true;
            foreach (List<Vector2> fp in footprints)
                if (InsidePolygon(p, fp) || DistanceToPolyline(p, fp, true) < TreeMarginBuilding) return true;
            if (DirtClearance(p) < TreeMarginPath) return true;
            foreach (List<Vector2> fence in fences)
                if (DistanceToPolyline(p, fence) < TreeMarginFence) return true;
            return false;
        }

        // Arbre dont le houppier déborde sur un chemin : on l'éloigne du chemin (direction opposée au bord
        // le plus proche), sinon on essaie quelques directions voisines. Tirage déterministe par position.
        bool TryRelocate(Vector2 p, float margin, List<Vector2> others, out Vector2 moved)
        {
            moved = p;
            const float h = 0.5f;
            var grad = new Vector2(DirtClearance(p + Vector2.right * h) - DirtClearance(p - Vector2.right * h),
                                   DirtClearance(p + Vector2.up * h) - DirtClearance(p - Vector2.up * h));
            if (grad.sqrMagnitude < 1e-6f) return false;
            grad.Normalize();
            var rng = new System.Random(Mathf.RoundToInt(p.x * 10f) * 73856093 ^ Mathf.RoundToInt(p.y * 10f) * 19349663);
            float[] angles = { 0f, 25f, -25f, 50f, -50f };
            foreach (float a in angles)
            {
                Vector2 dir = Quaternion.Euler(0f, 0f, a) * grad;
                for (float step = 1f; step <= TreeRelocateMax; step += 1f)
                {
                    Vector2 q = p + dir * step;
                    if (DirtClearance(q) < margin + 0.5f + (float)rng.NextDouble()) continue;
                    if (Blocked(q)) break;
                    bool free = true;
                    foreach (Vector2 o in others) if (Vector2.Distance(q, o) < TreeMinSpacing) { free = false; break; }
                    if (!free) continue;
                    moved = q;
                    return true;
                }
            }
            return false;
        }

        Vector2 ToWorld(Vector3 pos) => new Vector2(origin.x + pos.x * size.x, origin.z + pos.z * size.z);
        Vector3 ToTerrain(Vector2 p) => new Vector3((p.x - origin.x) / size.x, 0f, (p.y - origin.z) / size.z);

        Undo.RegisterCompleteObjectUndo(td, "Éclaircir les arbres du village");
        float scaleSum = 0f;
        foreach (TreeInstance tree in td.treeInstances) scaleSum += tree.heightScale;
        float avgScale = td.treeInstanceCount > 0 ? scaleSum / td.treeInstanceCount : 1.8f;

        // Sapins « héros » : même tirage à chaque passage (graine 1234), éloignés du chemin si besoin.
        var random = new System.Random(1234);
        var heroTrees = new List<TreeInstance>();
        var heroSpots = new List<Vector2>();
        foreach (Vector2 hero in heroes)
        {
            if (td.treePrototypes.Length == 0) break;
            float scale = avgScale * (0.9f + (float)random.NextDouble() * 0.2f);
            int proto = random.Next(td.treePrototypes.Length);
            float rot = (float)random.NextDouble() * Mathf.PI * 2f;
            Vector2 spot = hero;
            if (DirtClearance(hero) < PathMargin(proto, scale) && !TryRelocate(hero, PathMargin(proto, scale), heroSpots, out spot))
                continue;   // aucune place : sapin héros abandonné
            heroSpots.Add(spot);
            heroTrees.Add(new TreeInstance
            {
                position = ToTerrain(spot), prototypeIndex = proto, widthScale = scale, heightScale = scale,
                rotation = rot, color = Color.white, lightmapColor = Color.white,
            });
        }

        var kept = new List<TreeInstance>();
        var keptSpots = new List<Vector2>(heroSpots);
        int removed = 0, moved = 0;
        foreach (TreeInstance tree in td.treeInstances)
        {
            Vector2 p = ToWorld(tree.position);
            // Un sapin déjà posé sur un emplacement « héros » (d'origine ou déplacé) est retiré puis reposé.
            bool isHero = false;
            foreach (Vector2 hero in heroes) if (Vector2.Distance(p, hero) < 1f) isHero = true;
            foreach (Vector2 spot in heroSpots) if (Vector2.Distance(p, spot) < 1f) isHero = true;
            if (isHero || Blocked(p)) { removed++; continue; }
            float margin = PathMargin(tree.prototypeIndex, tree.widthScale);
            if (DirtClearance(p) < margin)
            {
                if (!TryRelocate(p, margin, keptSpots, out Vector2 q)) { removed++; continue; }
                TreeInstance t2 = tree;
                t2.position = ToTerrain(q);
                kept.Add(t2);
                keptSpots.Add(q);
                moved++;
                continue;
            }
            kept.Add(tree);
            keptSpots.Add(p);
        }
        kept.AddRange(heroTrees);
        int added = heroTrees.Count;

        // Forêt dense : on complète la zone « foret » du relevé jusqu'à l'espacement voulu (tirage reproductible).
        int forestAdded = 0;
        JToken forest = data["foret"];
        if (forest != null && td.treePrototypes.Length > 0)
        {
            List<Vector2> zone = ReadPoints(forest["polygone"]);
            float spacing = (float)forest["espacement_m"];
            var taken = new List<Vector2>(keptSpots);
            Vector2 min = zone[0], max = zone[0];
            foreach (Vector2 v in zone) { min = Vector2.Min(min, v); max = Vector2.Max(max, v); }
            int attempts = Mathf.CeilToInt((max.x - min.x) * (max.y - min.y) / (spacing * spacing) * 30f);
            for (int i = 0; i < attempts; i++)
            {
                var p = new Vector2(Mathf.Lerp(min.x, max.x, (float)random.NextDouble()), Mathf.Lerp(min.y, max.y, (float)random.NextDouble()));
                float scale = avgScale * (0.85f + (float)random.NextDouble() * 0.3f);
                int proto = random.Next(td.treePrototypes.Length);
                if (!InsidePolygon(p, zone) || Blocked(p) || DirtClearance(p) < PathMargin(proto, scale)) continue;
                bool free = true;
                foreach (Vector2 t in taken) if (Vector2.Distance(p, t) < spacing) { free = false; break; }
                if (!free) continue;
                kept.Add(new TreeInstance
                {
                    position = ToTerrain(p), prototypeIndex = proto, widthScale = scale, heightScale = scale,
                    rotation = (float)random.NextDouble() * Mathf.PI * 2f,
                    color = Color.white, lightmapColor = Color.white,
                });
                taken.Add(p);
                forestAdded++;
            }
        }

        td.SetTreeInstances(kept.ToArray(), true);
        EditorUtility.SetDirty(td);
        terrain.Flush();
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        return $"Arbres du Lobby : {moved} éloignés des chemins, {removed} retirés, {added} sapins « héros » posés, {forestAdded} ajoutés dans la forêt, {kept.Count} au total.";
    }

    #endregion

    #region --- TERRAIN SOUS LES BÂTIMENTS VISITABLES ---

    /// <summary>Bâtiments visitables : le terrain ne doit pas traverser leur plancher.</summary>
    private static readonly string[] InteriorTypes = { "Grange", "Tente_Medecin" };
    /// <summary>Marge (m) aplanie autour de l'emprise, puis largeur (m) du raccord en douceur.</summary>
    private const float FlattenMargin = 1.5f, FlattenBlend = 8f;

    /// <summary>
    /// Aplanit le terrain du Lobby à la hauteur du pivot sous la grange et la tente (posées dans la scène,
    /// rotation comprise), avec un raccord en douceur, et retire l'herbe à l'intérieur. Relançable : le
    /// pivot est posé sur le terrain, qui reste à sa hauteur.
    /// </summary>
    public static string FlattenInteriors()
    {
        JObject data = LoadLayout(out string error);
        if (data == null) return error;
        Terrain terrain = FindLobbyTerrain();
        if (terrain == null) return "Terrain du Lobby introuvable.";
        TerrainData td = terrain.terrainData;
        Vector3 origin = terrain.transform.position, size = td.size;

        var zones = new List<(Vector3 pos, Quaternion rot, float hw, float hd)>();
        foreach (JToken b in data["batiments"])
        {
            string id = (string)b["id"], type = (string)b["type"];
            if (System.Array.IndexOf(InteriorTypes, type) < 0) continue;
            GameObject go = GameObject.Find($"{RootName}/Buildings/{id}_{type}");
            if (go == null) continue;
            zones.Add((go.transform.position, go.transform.rotation,
                (float)b["largeur_facade_m"] / 2f + FlattenMargin, (float)b["profondeur_m"] / 2f + FlattenMargin));
        }
        if (zones.Count == 0) return "Ni grange ni tente posées dans LobbyVillage.";

        // Distance au rectangle de l'emprise (0 dedans).
        float Outside((Vector3 pos, Quaternion rot, float hw, float hd) z, Vector3 p)
        {
            Vector3 l = Quaternion.Inverse(z.rot) * (p - z.pos);
            float dx = Mathf.Max(Mathf.Abs(l.x) - z.hw, 0f), dz = Mathf.Max(Mathf.Abs(l.z) - z.hd, 0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        Undo.RegisterCompleteObjectUndo(td, "Aplanir le terrain sous la grange et la tente");
        int res = td.heightmapResolution;
        float[,] h = td.GetHeights(0, 0, res, res);
        int changed = 0;
        for (int i = 0; i < res; i++)
            for (int j = 0; j < res; j++)
            {
                var p = new Vector3(origin.x + j / (float)(res - 1) * size.x, 0f, origin.z + i / (float)(res - 1) * size.z);
                foreach (var z in zones)
                {
                    float d = Outside(z, p);
                    if (d >= FlattenBlend) continue;
                    float w = Mathf.SmoothStep(0f, 1f, 1f - d / FlattenBlend);
                    h[i, j] = Mathf.Lerp(h[i, j], (z.pos.y - origin.y) / size.y, w);
                    changed++;
                }
            }
        td.SetHeights(0, 0, h);

        int cleared = ClearDetailObstacles(terrain, data);
        EditorUtility.SetDirty(td);
        terrain.Flush();
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        return $"Terrain aplani sous {zones.Count} bâtiments visitables ({changed} points de hauteur, {cleared} touffes d'herbe retirées).";
    }

    /// <summary>
    /// Retire l'herbe et les buissons sous la grange, la tente, les potagers/plates-bandes, les rondins et les souches,
    /// et dégage les buissons/fougères des chemins et d'une marge de 2,2 m autour pour qu'ils ne débordent pas dessus.
    /// </summary>
    public static int ClearDetailObstacles(Terrain terrain, JObject data)
    {
        if (terrain == null || data == null) return 0;
        TerrainData td = terrain.terrainData;
        Vector3 origin = terrain.transform.position, size = td.size;

        var fullBoxes = new List<(Vector3 pos, Quaternion rot, float hw, float hd)>();
        var fullCircles = new List<(Vector2 center, float radius)>();
        var bushBoxes = new List<(Vector3 pos, Quaternion rot, float hw, float hd)>();
        var bushCircles = new List<(Vector2 center, float radius)>();

        // Bâtiments visitables + Potager (J1)
        foreach (JToken b in data["batiments"])
        {
            string id = (string)b["id"], type = (string)b["type"];
            if (System.Array.IndexOf(InteriorTypes, type) >= 0)
            {
                GameObject go = GameObject.Find($"{RootName}/Buildings/{id}_{type}");
                Vector3 pos = go != null ? go.transform.position : new Vector3((float)b["x"], 0f, (float)b["z"]);
                Quaternion rot = go != null ? go.transform.rotation : Quaternion.Euler(0f, (float)b["yaw_deg"], 0f);
                fullBoxes.Add((pos, rot, (float)b["largeur_facade_m"] / 2f + FlattenMargin, (float)b["profondeur_m"] / 2f + FlattenMargin));
            }
            else if (type == "Potager")
            {
                GameObject go = GameObject.Find($"{RootName}/Buildings/{id}_{type}");
                if (go != null && go.transform.childCount > 0)
                {
                    foreach (Transform bed in go.transform)
                    {
                        fullBoxes.Add((bed.position, bed.rotation, 6f * bed.localScale.x + 1.0f, 6f * bed.localScale.z + 1.0f));
                        bushBoxes.Add((bed.position, bed.rotation, 6f * bed.localScale.x + 2.8f, 6f * bed.localScale.z + 2.8f));
                    }
                }
                else
                {
                    Vector3 pos = new Vector3((float)b["x"], 0f, (float)b["z"]);
                    Quaternion rot = Quaternion.Euler(0f, (float)b["yaw_deg"], 0f);
                    fullBoxes.Add((pos, rot, (float)b["largeur_facade_m"] / 2f + 1.0f, (float)b["profondeur_m"] / 2f + 1.0f));
                    bushBoxes.Add((pos, rot, (float)b["largeur_facade_m"] / 2f + 2.8f, (float)b["profondeur_m"] / 2f + 2.8f));
                }
            }
        }

        // Plate-bande (P25), Rondins, Souches et dégagement des buissons autour des accessoires
        foreach (JToken p in data["accessoires"])
        {
            string id = (string)p["id"], type = (string)p["type"];
            GameObject go = GameObject.Find($"{RootName}/Props/{id}_{type}");
            Vector3 pos = go != null ? go.transform.position : new Vector3((float)p["x"], 0f, (float)p["z"]);
            if (type == "Plate_Bande")
            {
                Quaternion rot = go != null ? go.transform.rotation : Quaternion.Euler(0f, 290.6f, 0f);
                Vector3 sc = go != null ? go.transform.localScale : new Vector3(7f / 12f, 1f, 1f);
                fullBoxes.Add((pos, rot, 6f * sc.x + 1.0f, 6f * sc.z + 1.0f));
                bushBoxes.Add((pos, rot, 6f * sc.x + 4.2f, 6f * sc.z + 4.2f));
            }
            else if (type == "Rondin")
            {
                fullCircles.Add((new Vector2(pos.x, pos.z), 2.7f));
                bushCircles.Add((new Vector2(pos.x, pos.z), 5.8f));
            }
            else if (type == "Souche")
            {
                fullCircles.Add((new Vector2(pos.x, pos.z), 2.6f));
                bushCircles.Add((new Vector2(pos.x, pos.z), 4.2f));
            }
            else if (type == "Caisse")
            {
                fullCircles.Add((new Vector2(pos.x, pos.z), 2.0f));
                bushCircles.Add((new Vector2(pos.x, pos.z), 3.8f));
            }
            else
            {
                bushCircles.Add((new Vector2(pos.x, pos.z), 3.8f));
            }
        }

        // Décor de la place et du village : pas d'herbe dessous, et buissons dégagés autour de chaque accessoire.
        foreach (string groupName in new[] { "PlazaDecor", "VillageDecor" })
        {
            Transform decorGroup = GameObject.Find($"{RootName}/{groupName}")?.transform;
            if (decorGroup == null) continue;
            foreach (Transform ch in decorGroup)
            {
                Vector3 pos = ch.position;
                bool large = ch.name.Contains("P_HayPile") || ch.name.Contains("P_HayRack") ||
                             ch.name.Contains("P_Cart") || ch.name.Contains("P_Woodpile");
                bool stump = ch.name.Contains("P_Stump") || ch.name.Contains("P_ChoppingBlock");
                fullCircles.Add((new Vector2(pos.x, pos.z), large ? 3.0f : stump ? 2.6f : 2.0f));
                bushCircles.Add((new Vector2(pos.x, pos.z), large ? 4.8f : stump ? 4.0f : 4.2f));
            }
        }

        // Chemins et zones de terre (pour garder les buissons et fougères en retrait de 2,2 m).
        var paths = new List<(List<Vector2> pts, float halfWidth)>();
        foreach (JToken s in data["chemins"]) paths.Add((ReadPoints(s["points"]), (float)s["largeur_m"] / 2f));
        var dirtZones = new List<List<Vector2>>();
        foreach (JToken z in data["zones_terre"]) dirtZones.Add(ReadPoints(z["polygone"]));

        float OutsideBox((Vector3 pos, Quaternion rot, float hw, float hd) z, Vector3 p)
        {
            Vector3 l = Quaternion.Inverse(z.rot) * (p - z.pos);
            float dx = Mathf.Max(Mathf.Abs(l.x) - z.hw, 0f), dz = Mathf.Max(Mathf.Abs(l.z) - z.hd, 0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        float DirtClearance(Vector2 pt)
        {
            float best = float.MaxValue;
            foreach (var path in paths) best = Mathf.Min(best, DistanceToPolyline(pt, path.pts) - path.halfWidth);
            foreach (List<Vector2> zone in dirtZones)
            {
                float d = DistanceToPolyline(pt, zone, true);
                best = Mathf.Min(best, InsidePolygon(pt, zone) ? -d : d);
            }
            return best;
        }

        int dres = td.detailResolution, cleared = 0;
        const float bushPathMargin = 2.2f;
        for (int layer = 0; layer < td.detailPrototypes.Length; layer++)
        {
            GameObject proto = td.detailPrototypes[layer].prototype;
            bool isBush = proto != null && proto.name.StartsWith("P_Bush_");
            int[,] dens = td.GetDetailLayer(0, 0, dres, dres, layer);
            bool dirty = false;
            for (int i = 0; i < dres; i++)
            {
                for (int j = 0; j < dres; j++)
                {
                    if (dens[i, j] == 0) continue;
                    var p = new Vector3(origin.x + (j + 0.5f) / dres * size.x, 0f, origin.z + (i + 0.5f) / dres * size.z);
                    bool clear = false;
                    foreach (var z in fullBoxes)
                        if (OutsideBox(z, p) <= 0f) { clear = true; break; }
                    if (!clear)
                    {
                        var p2 = new Vector2(p.x, p.z);
                        foreach (var c in fullCircles)
                            if (Vector2.Distance(p2, c.center) <= c.radius) { clear = true; break; }
                        if (!clear && isBush)
                        {
                            if (DirtClearance(p2) < bushPathMargin) clear = true;
                            else
                            {
                                foreach (var bb in bushBoxes)
                                    if (OutsideBox(bb, p) <= 0f) { clear = true; break; }
                                if (!clear)
                                    foreach (var bc in bushCircles)
                                        if (Vector2.Distance(p2, bc.center) <= bc.radius) { clear = true; break; }
                            }
                        }
                    }
                    if (clear)
                    {
                        dens[i, j] = 0;
                        cleared++;
                        dirty = true;
                    }
                }
            }
            if (dirty) td.SetDetailLayer(0, 0, layer, dens);
        }
        EditorUtility.SetDirty(td);
        terrain.Flush();
        return cleared;
    }

    #endregion

    #region --- DÉCORATION DE LA PLACE ET DU VILLAGE ---

    private sealed class DecorHelper
    {
        private readonly Transform _decor;
        private readonly Terrain _terrain;
        private readonly JObject _pathData;
        private readonly List<Vector2> _treePositions;
        private readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();

        public DecorHelper(Transform decor, Terrain terrain, JObject pathData = null)
        {
            _decor = decor;
            _terrain = terrain;
            _pathData = pathData;
            if (_terrain != null && _terrain.terrainData != null)
            {
                Vector3 origin = _terrain.transform.position;
                Vector3 size = _terrain.terrainData.size;
                TreeInstance[] trees = _terrain.terrainData.treeInstances;
                _treePositions = new List<Vector2>(trees.Length);
                foreach (TreeInstance ti in trees)
                    _treePositions.Add(new Vector2(origin.x + ti.position.x * size.x, origin.z + ti.position.z * size.z));
            }
        }

        private Vector2 PushOutOfTrees(Vector2 p, float minRadius)
        {
            if (_treePositions == null || _treePositions.Count == 0 || minRadius <= 0f) return p;
            for (int iter = 0; iter < 8; iter++)
            {
                Vector2 nearest = Vector2.zero;
                float bestSq = minRadius * minRadius;
                bool found = false;
                foreach (Vector2 t in _treePositions)
                {
                    float sq = (p - t).sqrMagnitude;
                    if (sq < bestSq) { bestSq = sq; nearest = t; found = true; }
                }
                if (!found) break;
                Vector2 diff = p - nearest;
                float d = diff.magnitude;
                Vector2 dir = d > 1e-4f ? diff / d : Vector2.right;
                p = nearest + dir * (minRadius + 0.3f);
            }
            return p;
        }

        private GameObject GetPrefab(string name)
        {
            if (!_cache.TryGetValue(name, out GameObject pf))
            {
                pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");
                _cache[name] = pf;
            }
            return pf;
        }

        public GameObject SpawnWorld(string prefabName, Vector3 worldPos, float yawDeg, float scale = 1f, float yOffset = 0f, float pathClearance = 0f, float treeClearance = 0f)
        {
            GameObject pf = GetPrefab(prefabName);
            if (pf == null) return null;
            Vector2 pt = new Vector2(worldPos.x, worldPos.z);
            if (_pathData != null && pathClearance > 0f) pt = PushOutOfPaths(_pathData, pt, pathClearance);
            if (treeClearance > 0f) pt = PushOutOfTrees(pt, treeClearance);
            if (_pathData != null && pathClearance > 0f) pt = PushOutOfPaths(_pathData, pt, pathClearance);
            Vector3 p = Ground(_terrain, pt.x, pt.y);
            p.y += yOffset;
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(pf, _decor);
            go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yawDeg, 0f));
            if (!Mathf.Approximately(scale, 1f)) go.transform.localScale = Vector3.one * scale;
            return go;
        }

        public GameObject SpawnLocal(Transform anchor, string prefabName, Vector3 localPos, float localYawDeg, float scale = 1f, float yOffset = 0f)
        {
            if (anchor == null) return null;
            Vector3 wp = anchor.TransformPoint(new Vector3(localPos.x, 0f, localPos.z));
            float yaw = anchor.eulerAngles.y + localYawDeg;
            return SpawnWorld(prefabName, wp, yaw, scale, yOffset);
        }

        public void PlaceLantern(Vector3 worldPos, Vector3 lookTarget)
        {
            GameObject pf = GetPrefab("P_LanternPost");
            if (pf == null) return;
            MeshFilter mf = pf.GetComponentInChildren<MeshFilter>(true);
            float armSign = mf != null && mf.sharedMesh != null && mf.sharedMesh.bounds.min.x < -mf.sharedMesh.bounds.max.x ? -1f : 1f;
            Vector2 d = new Vector2(lookTarget.x - worldPos.x, lookTarget.z - worldPos.z);
            float dirYaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            // Pour orienter localRight * armSign vers d : yaw = dirYaw - 90 * armSign.
            SpawnWorld("P_LanternPost", worldPos, dirYaw - 90f * armSign);
        }

        public void PlaceBorderFence(params Vector2[] pts)
        {
            GameObject bayPf = GetPrefab("P_Fence_Wood");
            GameObject postPf = GetPrefab("P_Fence_Post");
            if (bayPf == null) return;
            const float bay = 8f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                float len = Vector2.Distance(a, b);
                if (len < 0.5f) continue;
                Vector2 dir = (b - a) / len;
                Quaternion rot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
                int bays = Mathf.Max(1, Mathf.RoundToInt(len / bay));
                float step = len / bays;
                for (int k = 0; k < bays; k++)
                {
                    Vector2 mid = a + dir * (step * (k + 0.5f));
                    GameObject seg = (GameObject)PrefabUtility.InstantiatePrefab(bayPf, _decor);
                    seg.transform.SetPositionAndRotation(Ground(_terrain, mid.x, mid.y), rot);
                    seg.transform.localScale = new Vector3(1f, 1f, step / bay);
                }
                if (i == pts.Length - 2 && postPf != null)
                {
                    GameObject post = (GameObject)PrefabUtility.InstantiatePrefab(postPf, _decor);
                    post.transform.SetPositionAndRotation(Ground(_terrain, b.x, b.y), rot);
                }
            }
        }
    }

    /// <summary>
    /// Décore la place autour du feu de camp et des 4 étals (E1 à E4) avec les prefabs disponibles,
    /// d'après la composition de GameDesign/99_Attachments/Lobby/Lobby_Place_Decoree_Proposition.png.
    /// </summary>
    public static void DecoratePlaza(Transform root, Terrain terrain)
    {
        if (root == null) return;
        Transform old = root.Find("PlazaDecor");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Transform decor = NewChild(root, "PlazaDecor");
        var h = new DecorHelper(decor, terrain);

        Transform stalls = root.Find("Stalls");
        Transform e1 = stalls != null ? stalls.Find("E1_Etal_Double") : null;
        Transform e2 = stalls != null ? stalls.Find("E2_Etal_Appentis") : null;
        Transform e3 = stalls != null ? stalls.Find("E3_Etal_Simple") : null;
        Transform e4 = stalls != null ? stalls.Find("E4_Etal_Simple") : null;
        Transform c1 = stalls != null ? stalls.Find("C1_Feu_De_Camp") : null;

        Vector3 fireCenter = c1 != null ? c1.position : new Vector3(-40.6f, 0f, -48.9f);

        // 1. Feu de camp central : 2 souches + 1 billot à hache en sièges autour du foyer.
        h.SpawnWorld("P_Stump", fireCenter + new Vector3(-5.0f, 0f, -2.5f), 65f);
        h.SpawnWorld("P_ChoppingBlock", fireCenter + new Vector3(-0.8f, 0f, 5.2f), 145f);
        h.SpawnWorld("P_Stump", fireCenter + new Vector3(4.6f, 0f, -3.2f), 305f);

        // 2. Étal Sud-Ouest (E4) : tonneaux, caisses, pot sur les flancs, cagettes devant et sur le comptoir, lanterne.
        if (e4 != null)
        {
            h.SpawnLocal(e4, "P_Barrel", new Vector3(6.8f, 0f, 1.5f), 15f);
            h.SpawnLocal(e4, "P_Crate_01", new Vector3(7.4f, 0f, 4.9f), 8f);
            h.SpawnLocal(e4, "P_Pot", new Vector3(4.6f, 0f, 4.9f), 20f);
            h.SpawnLocal(e4, "P_Barrel", new Vector3(-6.8f, 0f, 2.2f), -20f);
            h.SpawnLocal(e4, "P_Crate_02", new Vector3(-7.0f, 0f, 5.6f), 18f);
            h.SpawnLocal(e4, "P_Crate_02", new Vector3(-1.9f, 0f, 5.7f), -6f);
            h.SpawnLocal(e4, "P_Crate_03", new Vector3(1.9f, 0f, 5.6f), 10f);
            h.SpawnLocal(e4, "P_Crate_03", new Vector3(-2.1f, 0f, 4.05f), 6f, 0.48f, 3.51f);
            h.SpawnLocal(e4, "P_Crate_03", new Vector3(2.0f, 0f, 4.05f), -8f, 0.48f, 3.51f);
            h.PlaceLantern(e4.TransformPoint(new Vector3(8.8f, 0f, -2.5f)), fireCenter);
        }

        // Clôture en bordure Sud-Est de la place (entre l'arrivée du chemin S6 et l'arrière de E3).
        h.PlaceBorderFence(
            new Vector2(-41.0f, -84.5f),
            new Vector2(-32.5f, -84.5f),
            new Vector2(-24.0f, -81.0f),
            new Vector2(-16.0f, -75.5f));

        // 3. Étal appentis Nord-Ouest (E2) : tonneau, tréteau, sac, tas de bois, charrette, brouette, tonneaux empilés, lanterne.
        if (e2 != null)
        {
            h.SpawnLocal(e2, "P_Barrel", new Vector3(-2.9f, 0f, -1.8f), 20f);
            h.SpawnLocal(e2, "P_Sawhorse", new Vector3(-0.3f, 0f, 0.2f), 85f);
            h.SpawnLocal(e2, "P_Sack", new Vector3(4.2f, 0f, 4.2f), -25f);
            h.SpawnLocal(e2, "P_Woodpile", new Vector3(10.8f, 0f, -0.5f), 12f);
            h.SpawnLocal(e2, "P_Wheelbarrow", new Vector3(9.6f, 0f, 4.2f), -35f);
            h.SpawnLocal(e2, "P_Cart", new Vector3(17.8f, 0f, 2.8f), 22f);
            h.SpawnLocal(e2, "P_Barrel_Stack", new Vector3(24.2f, 0f, -0.8f), 25f);
            h.SpawnLocal(e2, "P_Crate_01", new Vector3(14.2f, 0f, 7.2f), 14f);
            h.PlaceLantern(e2.TransformPoint(new Vector3(-9.5f, 0f, 1.5f)), fireCenter);
        }

        // 4. Étal Sud-Est (E3, premier plan gauche) : caisses empilées côté place, panier, sacs, tonneaux, pots.
        if (e3 != null)
        {
            h.SpawnLocal(e3, "P_Barrel", new Vector3(-3.0f, 0f, 1.5f), 10f);
            h.SpawnLocal(e3, "P_Barrel", new Vector3(-6.7f, 0f, -1.5f), -18f);
            h.SpawnLocal(e3, "P_Pot", new Vector3(-4.8f, 0f, 2.4f), -10f);
            h.SpawnLocal(e3, "P_Crate_01", new Vector3(-6.8f, 0f, 3.0f), 12f);
            h.SpawnLocal(e3, "P_Basket", new Vector3(-6.9f, 0f, 5.8f), 25f);
            h.SpawnLocal(e3, "P_Sack", new Vector3(-3.4f, 0f, 5.7f), -15f);
            h.SpawnLocal(e3, "P_Crate_01", new Vector3(3.0f, 0f, 5.7f), -8f);
            h.SpawnLocal(e3, "P_Crate_01", new Vector3(6.6f, 0f, 5.1f), 10f);
            h.SpawnLocal(e3, "P_Crate_02", new Vector3(3.0f, 0f, 5.7f), 12f, 0.78f, 3.0f);
            h.SpawnLocal(e3, "P_Crate_03", new Vector3(6.6f, 0f, 5.1f), -15f, 0.78f, 3.0f);
            h.SpawnLocal(e3, "P_Crate_03", new Vector3(1.8f, 0f, 4.05f), 5f, 0.48f, 3.51f);
            h.SpawnLocal(e3, "P_Pot", new Vector3(-2.4f, 0f, 4.05f), 0f, 0.55f, 3.51f);
        }

        // 5. Étal double Nord-Est (E1, premier plan droit) : tonneaux, panonceau, bout de clôture, caisses, sacs, panier, pots.
        if (e1 != null)
        {
            h.SpawnLocal(e1, "P_Barrel", new Vector3(-12.2f, 0f, 3.2f), 25f);
            h.SpawnLocal(e1, "P_Barrel", new Vector3(-12.6f, 0f, 0.4f), -10f);
            h.SpawnLocal(e1, "P_Pot", new Vector3(-10.2f, 0f, 4.9f), 12f);
            h.SpawnLocal(e1, "P_Sign_Small", new Vector3(-14.2f, 0f, 5.2f), 215f, 1.25f);
            Vector3 f1 = e1.TransformPoint(new Vector3(-13.8f, 0f, 3.5f));
            Vector3 f2 = e1.TransformPoint(new Vector3(-15.5f, 0f, -4.5f));
            h.PlaceBorderFence(new Vector2(f1.x, f1.z), new Vector2(f2.x, f2.z));
            h.SpawnLocal(e1, "P_Crate_01", new Vector3(-7.4f, 0f, 5.3f), 6f);
            h.SpawnLocal(e1, "P_Crate_02", new Vector3(-7.4f, 0f, 5.3f), -12f, 0.78f, 3.0f);
            h.SpawnLocal(e1, "P_Sack", new Vector3(-3.8f, 0f, 5.2f), 30f);
            h.SpawnLocal(e1, "P_Sack", new Vector3(3.4f, 0f, 5.1f), -20f);
            h.SpawnLocal(e1, "P_Basket", new Vector3(-3.0f, 0f, 3.55f), 15f, 0.55f, 3.51f);
            h.SpawnLocal(e1, "P_Pot", new Vector3(0.4f, 0f, 3.55f), 20f, 0.52f, 3.51f);
            h.SpawnLocal(e1, "P_Crate_03", new Vector3(4.0f, 0f, 3.55f), -6f, 0.48f, 3.51f);
        }

        // 6. Zone Nord-Ouest de la place (autour de P11_Caisse en -76.7, -66.1) : pile de caisses + tonneau, sac et panier.
        h.SpawnWorld("P_Crate_01", new Vector3(-74.1f, 0f, -64.5f), 16f);
        h.SpawnWorld("P_Crate_02", new Vector3(-75.2f, 0f, -62.2f), -22f);
        h.SpawnWorld("P_Crate_01", new Vector3(-76.6f, 0f, -66.0f), 35f, 0.78f, 3.0f);
        h.SpawnWorld("P_Crate_03", new Vector3(-74.1f, 0f, -64.5f), -12f, 0.78f, 3.0f);
        h.SpawnWorld("P_Barrel", new Vector3(-79.2f, 0f, -64.3f), -18f);
        h.SpawnWorld("P_Sack", new Vector3(-74.8f, 0f, -67.8f), 42f);
        h.SpawnWorld("P_Basket", new Vector3(-72.0f, 0f, -66.2f), 15f);
    }

    /// <summary>
    /// Décore le reste du village (enclos, parvis de la grange, cour de la forge, tente du médecin,
    /// potager, abords des maisons et carrefours) avec les accessoires du kit.
    /// </summary>
    public static void DecorateVillage(Transform root, Terrain terrain, JObject data)
    {
        if (root == null) return;
        Transform old = root.Find("VillageDecor");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Transform decor = NewChild(root, "VillageDecor");
        var h = new DecorHelper(decor, terrain, data);

        Transform buildings = root.Find("Buildings");
        Transform g1 = buildings != null ? buildings.Find("G1_Grange") : null;
        Transform t1 = buildings != null ? buildings.Find("T1_Tente_Medecin") : null;

        // 1. Enclos des bêtes (K3) : mangeoire à foin, tas de foin, bottes de foin empilées, tonneau et sac.
        h.SpawnWorld("P_HayRack", new Vector3(-91.5f, 0f, 31.5f), 55f, 1f, 0f, 2.5f, 4.5f);
        h.SpawnWorld("P_HayPile", new Vector3(-89.0f, 0f, 18.5f), 15f, 1f, 0f, 2.5f, 4.5f);
        h.SpawnWorld("P_HayBale", new Vector3(-83.5f, 0f, 12.0f), 40f, 1f, 0f, 2.5f, 4.0f);
        h.SpawnWorld("P_HayBale", new Vector3(-85.5f, 0f, 14.2f), 28f, 1f, 0f, 2.5f, 4.0f);
        h.SpawnWorld("P_HayBale", new Vector3(-84.5f, 0f, 13.0f), 35f, 1f, 1.52f);
        h.SpawnWorld("P_Barrel", new Vector3(-77.5f, 0f, 29.0f), 18f, 1f, 0f, 2.5f, 3.5f);
        h.SpawnWorld("P_Sack", new Vector3(-79.2f, 0f, 27.2f), -35f, 1f, 0f, 2.5f, 3.5f);

        // 2. Parvis et flancs de la Grange (G1) : foin, mangeoire, charrette, brouette, tas de bois, billot, lanterne.
        if (g1 != null)
        {
            h.SpawnLocal(g1, "P_HayPile", new Vector3(-23.0f, 0f, 12.0f), 20f);
            h.SpawnLocal(g1, "P_HayBale", new Vector3(-23.5f, 0f, 6.5f), 12f);
            h.SpawnLocal(g1, "P_HayBale", new Vector3(-21.8f, 0f, 4.2f), -8f);
            h.SpawnLocal(g1, "P_HayBale", new Vector3(-22.8f, 0f, 5.3f), 4f, 1f, 1.52f);
            h.SpawnLocal(g1, "P_HayRack", new Vector3(-22.5f, 0f, -4.0f), 90f);
            h.SpawnLocal(g1, "P_Cart", new Vector3(24.5f, 0f, 15.0f), 25f);
            h.SpawnLocal(g1, "P_Wheelbarrow", new Vector3(18.0f, 0f, 21.5f), -40f);
            h.SpawnLocal(g1, "P_Crate_01", new Vector3(21.5f, 0f, 4.0f), 8f);
            h.SpawnLocal(g1, "P_Barrel", new Vector3(21.5f, 0f, 0.5f), -15f);
            h.SpawnLocal(g1, "P_Sack", new Vector3(21.5f, 0f, 7.2f), 30f);
            h.SpawnLocal(g1, "P_Woodpile", new Vector3(22.5f, 0f, -10.0f), 90f);
            h.SpawnLocal(g1, "P_ChoppingBlock", new Vector3(22.5f, 0f, -15.5f), 45f);
            h.PlaceLantern(g1.TransformPoint(new Vector3(13.5f, 0f, 23.0f)), g1.TransformPoint(new Vector3(0f, 0f, 24.0f)));
        }

        // 3. Cour de la Forge (F1, F2, F3) : bois, billot à hache, brouette, tonneau, caisse, sac, lanterne.
        h.SpawnWorld("P_Woodpile", new Vector3(-56.0f, 0f, 135.5f), 22f, 1f, 0f, 2.5f, 4.2f);
        h.SpawnWorld("P_ChoppingBlock", new Vector3(-58.5f, 0f, 129.5f), 65f, 1f, 0f, 2.5f, 4.0f);
        h.SpawnWorld("P_Wheelbarrow", new Vector3(-66.5f, 0f, 136.0f), 115f, 1f, 0f, 2.5f, 4.0f);
        h.SpawnWorld("P_Barrel", new Vector3(-91.5f, 0f, 136.5f), 10f, 1f, 0f, 2.5f, 3.5f);
        h.SpawnWorld("P_Crate_01", new Vector3(-91.5f, 0f, 132.5f), -15f, 1f, 0f, 2.5f, 3.5f);
        h.SpawnWorld("P_Sack", new Vector3(-89.0f, 0f, 129.5f), 30f, 1f, 0f, 2.5f, 3.5f);
        h.PlaceLantern(new Vector3(-64.5f, 0f, 120.5f), new Vector3(-75.0f, 0f, 133.0f));

        // 4. Tente du Médecin (T1) : lanterne à l'entrée, caisses, tonneau, panier, sac, pot.
        if (t1 != null)
        {
            h.PlaceLantern(t1.TransformPoint(new Vector3(11.8f, 0f, 12.5f)), t1.TransformPoint(new Vector3(0f, 0f, 16.0f)));
            h.SpawnLocal(t1, "P_Crate_01", new Vector3(12.2f, 0f, 6.0f), 12f);
            h.SpawnLocal(t1, "P_Crate_03", new Vector3(12.4f, 0f, 2.2f), -18f);
            h.SpawnLocal(t1, "P_Sack", new Vector3(12.0f, 0f, -1.5f), 25f);
            h.SpawnLocal(t1, "P_Barrel", new Vector3(-12.2f, 0f, 8.5f), -10f);
            h.SpawnLocal(t1, "P_Basket", new Vector3(-12.2f, 0f, 5.2f), 20f);
            h.SpawnLocal(t1, "P_Pot", new Vector3(-11.8f, 0f, 11.5f), 5f);
        }

        // 5. Potager (J1) et abords des maisons (M05, M06, M10, M12, E5) + lanternes aux carrefours.
        h.SpawnWorld("P_Wheelbarrow", new Vector3(-123.5f, 0f, -18.5f), 100f, 1f, 0f, 2.5f, 4.0f);
        h.SpawnWorld("P_Basket", new Vector3(-123.8f, 0f, -13.5f), 20f, 1f, 0f, 2.5f, 3.5f);
        h.SpawnWorld("P_Sack", new Vector3(-124.2f, 0f, -10.5f), -30f, 1f, 0f, 2.5f, 3.5f);
        h.SpawnWorld("P_Pot", new Vector3(-126.0f, 0f, -26.5f), 10f, 1f, 0f, 2.5f, 3.5f);
        h.SpawnWorld("P_ChoppingBlock", new Vector3(-175.5f, 0f, -31.5f), 45f, 1f, 0f, 2.2f, 4.0f);
        h.SpawnWorld("P_HayBale", new Vector3(-157.0f, 0f, -4.5f), 15f, 1f, 0f, 2.5f, 4.0f);
        h.SpawnWorld("P_Barrel", new Vector3(-171.0f, 0f, -1.5f), 40f, 1f, 0f, 2.2f, 3.5f);
        h.SpawnWorld("P_Woodpile", new Vector3(-76.5f, 0f, -142.5f), 30f, 1f, 0f, 2.5f, 4.2f);
        h.SpawnWorld("P_Cart", new Vector3(-97.5f, 0f, -108.5f), 35f, 1f, 0f, 2.8f, 5.5f);
        h.SpawnWorld("P_Barrel", new Vector3(-125.0f, 0f, -152.5f), -20f, 1f, 0f, 2.5f, 3.5f);
        h.SpawnWorld("P_Sack", new Vector3(-123.5f, 0f, -150.2f), 45f, 1f, 0f, 2.5f, 3.5f);
        h.SpawnWorld("P_Pot", new Vector3(-128.5f, 0f, -81.5f), 0f, 1f, 0f, 2.5f, 3.5f);
        h.SpawnWorld("P_Sack", new Vector3(-126.8f, 0f, -83.8f), 25f, 1f, 0f, 2.5f, 3.5f);
        h.PlaceLantern(new Vector3(-153.5f, 0f, -12.0f), new Vector3(-145.0f, 0f, -12.0f));
        h.PlaceLantern(new Vector3(-122.0f, 0f, -88.0f), new Vector3(-118.0f, 0f, -96.0f));
        h.PlaceLantern(new Vector3(-66.0f, 0f, -154.0f), new Vector3(-56.0f, 0f, -158.0f));
    }

    #endregion

    #region --- PHOTO DE CONTRÔLE ---

    /// <summary>Capture la scène depuis la caméra du concept art (lue dans le JSON) et l'enregistre en JPEG.</summary>
    public static string CaptureControlPhoto(string outputPath)
    {
        JObject data = LoadLayout(out string error);
        if (data == null) return error;
        JToken cam = data["meta"]["camera_concept"];
        int width = (int)cam["image"][0], height = (int)cam["image"][1];
        float focal = (float)cam["focale_px"];

        GameObject go = new GameObject("LobbyControlCamera") { hideFlags = HideFlags.HideAndDontSave };
        Camera camera = go.AddComponent<Camera>();
        camera.transform.position = new Vector3((float)cam["position"][0], (float)cam["position"][1], (float)cam["position"][2]);
        camera.transform.rotation = Quaternion.Euler((float)cam["pitch_deg"], (float)cam["yaw_deg"], 0f);
        float fov = 2f * Mathf.Atan(height / 2f / focal) * Mathf.Rad2Deg;
        camera.fieldOfView = fov;
        camera.nearClipPlane = 1f;
        camera.farClipPlane = 3000f;

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = previous;
        camera.targetTexture = null;

        string full = Path.IsPathRooted(outputPath) ? outputPath : Path.Combine(ProjectRoot(), outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllBytes(full, tex.EncodeToJPG(90));
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
        rt.Release();
        Object.DestroyImmediate(rt);
        return $"Photo de contrôle enregistrée : {full} ({width} x {height}, champ vertical {fov:0.0}°).";
    }

    #endregion

    #region --- UTILITAIRES ---

    private static string ProjectRoot() => Path.GetDirectoryName(Application.dataPath);

    private static JObject LoadLayout(out string error)
    {
        string full = Path.Combine(ProjectRoot(), JsonPath);
        if (!File.Exists(full)) { error = $"{JsonPath} introuvable : lancer « python ArtSource/Lobby/lobby_layout.py »."; return null; }
        error = null;
        return JObject.Parse(File.ReadAllText(full));
    }

    /// <summary>Terrain fait main du Lobby (celui dont les données sont « Manual Center Data » du TerrainGenerator).</summary>
    private static Terrain FindLobbyTerrain()
    {
        TerrainGenerator generator = Object.FindFirstObjectByType<TerrainGenerator>();
        if (generator == null || generator.manualCenterData == null) return null;
        foreach (Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            if (terrain.terrainData == generator.manualCenterData) return terrain;
        return null;
    }

    /// <summary>Point du monde posé sur le terrain du Lobby.</summary>
    private static Vector3 Ground(Terrain terrain, float x, float z)
    {
        var p = new Vector3(x, 0f, z);
        if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
        return p;
    }

    private static GameObject LoadPrefab(string type)
    {
        return PrefabByType.TryGetValue(type, out string name)
            ? AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab")
            : null;
    }

    private static Transform NewChild(Transform parent, string name)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private static List<Vector2> ReadPoints(JToken array)
    {
        var list = new List<Vector2>();
        foreach (JToken p in array) list.Add(new Vector2((float)p[0], (float)p[1]));
        return list;
    }

    private static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Color color, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = BlockoutMaterial(color);
        return go;
    }

    private static GameObject Cylinder(Transform parent, string name, float diameter, float height, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, height / 2f - 0.25f, 0f);
        go.transform.localScale = new Vector3(diameter, height / 2f + 0.25f, diameter);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        BoxCollider col = go.AddComponent<BoxCollider>();   // collider primitif simple, à la taille du cylindre
        col.size = new Vector3(1f, 2f, 1f);
        go.GetComponent<MeshRenderer>().sharedMaterial = BlockoutMaterial(color);
        return go;
    }

    /// <summary>Matériau de maquette (URP/Lit mat) partagé par couleur, créé à la demande dans BlockoutFolder.</summary>
    private static Material BlockoutMaterial(Color color)
    {
        string path = $"{BlockoutFolder}/M_Blockout_{ColorUtility.ToHtmlStringRGB(color)}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        Directory.CreateDirectory(Path.Combine(ProjectRoot(), BlockoutFolder));
        AssetDatabase.Refresh();
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0.1f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static float DistanceToPolyline(Vector2 p, List<Vector2> pts, bool closed = false)
    {
        float best = float.MaxValue;
        int n = closed ? pts.Count : pts.Count - 1;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % pts.Count], ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }

    private static bool InsidePolygon(Vector2 p, List<Vector2> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }

    #endregion
}
