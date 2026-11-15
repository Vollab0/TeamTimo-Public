using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Outil éditeur : crée Assets/Prefabs/Maze/Wall.prefab (un mur seul) et pose dans la scène ouverte l'objet "LobbyWalls"
/// (murs + piliers du périmètre du Lobby) exactement là où MazeGenerator les générait avant que
/// _lobbyHasPlacedWalls ne les retire, avec une arche (Gate.prefab) dans chacune des 4 portes.
/// Les positions sont lues sur le prefab MazeCell et sur le MazeGenerator de la scène.
/// Une fois posés, les murs se déplacent / suppriment à la main dans la scène.
/// </summary>
public static class LobbyWallsBuilder
{
    private const string WallPrefabPath = "Assets/Prefabs/Maze/Wall.prefab";
    private const string CellPrefabPath = "Assets/Prefabs/Maze/MazeCell.prefab";
    private const string PillarPrefabPath = "Assets/Prefabs/Maze/Pillar.prefab";
    private const string GatePrefabPath = "Assets/Prefabs/Maze/Gate.prefab";
    private const string GateModelPath = "Assets/Art/Models/Environment/Gate_Stylized.fbx";
    private const string GateMeshPrefix = "Gate_Arch_";
    private const string RootName = "LobbyWalls";
    private const string GatesName = "Gates";

    [MenuItem("TeamTimo/Lobby/Créer le prefab Wall")]
    public static void CreateWallPrefab()
    {
        GameObject cellAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath);
        MazeCell cellSrc = cellAsset.GetComponent<MazeCell>();
        Transform srcModel = cellSrc._frontWall.transform.GetChild(0);

        // Racine vide + modèle (copie du mur "front" de MazeCell : mesh, matériaux, layer, collider, rotation locale).
        GameObject root = new GameObject("Wall");
        root.layer = srcModel.gameObject.layer;
        GameObject model = (GameObject)Object.Instantiate(srcModel.gameObject, root.transform);
        model.name = "Model";
        // Rotation locale du modèle dans la cellule (position relative à la cellule, pas à la racine : on la remet à zéro).
        Quaternion cellRot = Quaternion.Inverse(cellAsset.transform.rotation) * srcModel.rotation;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = cellRot;
        model.transform.localScale = srcModel.localScale;

        PrefabUtility.SaveAsPrefabAsset(root, WallPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("LobbyWallsBuilder : " + WallPrefabPath + " créé.");
    }

    [MenuItem("TeamTimo/Lobby/Poser les murs du Lobby dans la scène")]
    public static void BuildMenu()
    {
        if (GameObject.Find(RootName) != null &&
            !EditorUtility.DisplayDialog("LobbyWalls existe déjà",
                "Recréer LobbyWalls effacera les modifications faites à la main dans cet objet. Continuer ?", "Recréer", "Annuler"))
            return;
        Build();
    }

    /// <summary>Crée (ou recrée) LobbyWalls dans la scène active. Retourne un résumé.</summary>
    public static string Build()
    {
        MazeGenerator gen = Object.FindFirstObjectByType<MazeGenerator>();
        if (gen == null) return "MazeGenerator introuvable dans la scène";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath) == null) CreateWallPrefab();
        GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);
        GameObject pillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PillarPrefabPath);

        GetWallFrames(out Vector3 frontPos, out Vector3 rightPos, out Quaternion frontRot, out Quaternion rightRot);
        // Rotation locale du modèle dans Wall.prefab (= celle du mur "front")
        Quaternion modelLocalRot = wallPrefab.transform.GetChild(0).localRotation;

        int cs = gen.GetCellSize();
        float cx = (gen.GetMazeWidth() - 1) * cs / 2f;
        float cz = (gen.GetMazeDepth() - 1) * cs / 2f;
        RectInt lobby = gen.GetLobbyRect();

        Vector3 CellOrigin(int x, int z) => new Vector3(x * cs - cx, 0f, z * cs - cz);

        var frontDoors = new List<Vector2Int>();
        var rightDoors = new List<Vector2Int>();
        gen.GetLobbyDoors(frontDoors, rightDoors);

        GameObject old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);

        GameObject root = new GameObject(RootName);
        Transform wallsT = new GameObject("Walls").transform;
        Transform pillarsT = new GameObject("Pillars").transform;
        wallsT.SetParent(root.transform, false);
        pillarsT.SetParent(root.transform, false);

        int wallCount = 0, pillarCount = 0;

        void PlaceWall(string name, Vector3 cellOrigin, Vector3 relPos, Quaternion wallRot)
        {
            GameObject w = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, wallsT);
            w.name = name;
            w.transform.position = cellOrigin + relPos;
            w.transform.rotation = wallRot * Quaternion.Inverse(modelLocalRot);
            wallCount++;
        }

        // Bas / Haut : mur "front" des cellules z = lobby.y - 1 et z = lobby.yMax - 1
        for (int x = lobby.x; x < lobby.xMax; x++)
        {
            int zb = lobby.y - 1;
            if (zb >= 0 && !frontDoors.Contains(new Vector2Int(x, zb)))
                PlaceWall($"Wall_Sud_{x}", CellOrigin(x, zb), frontPos, frontRot);
            int zt = lobby.yMax - 1;
            if (!frontDoors.Contains(new Vector2Int(x, zt)))
                PlaceWall($"Wall_Nord_{x}", CellOrigin(x, zt), frontPos, frontRot);
        }
        // Gauche / Droite : mur "right" des cellules x = lobby.x - 1 et x = lobby.xMax - 1
        for (int z = lobby.y; z < lobby.yMax; z++)
        {
            int xl = lobby.x - 1;
            if (xl >= 0 && !rightDoors.Contains(new Vector2Int(xl, z)))
                PlaceWall($"Wall_Ouest_{z}", CellOrigin(xl, z), rightPos, rightRot);
            int xr = lobby.xMax - 1;
            if (!rightDoors.Contains(new Vector2Int(xr, z)))
                PlaceWall($"Wall_Est_{z}", CellOrigin(xr, z), rightPos, rightRot);
        }

        // Piliers : intersections du contour du Lobby (coins inclus), comme GeneratePillars.
        for (int x = lobby.x; x <= lobby.xMax; x++)
        {
            for (int z = lobby.y; z <= lobby.yMax; z++)
            {
                bool strictlyInside = x > lobby.x && x < lobby.xMax && z > lobby.y && z < lobby.yMax;
                if (strictlyInside) continue;
                GameObject p = (GameObject)PrefabUtility.InstantiatePrefab(pillarPrefab, pillarsT);
                p.name = $"Pillar_{x}_{z}";
                p.transform.position = new Vector3(x * cs - cs / 2f - cx, 0f, z * cs - cs / 2f - cz);
                p.transform.rotation = Quaternion.identity;
                pillarCount++;
            }
        }

        int gateCount = PlaceGates(gen, root.transform);

        EditorSceneManager.MarkSceneDirty(root.scene);
        string summary = $"LobbyWalls : {wallCount} murs, {pillarCount} piliers, {gateCount} arches (portes front={frontDoors.Count} right={rightDoors.Count})";
        Debug.Log(summary);
        return summary;
    }

    [MenuItem("TeamTimo/Lobby/Poser les arches des portes du Lobby")]
    public static void GatesMenu()
    {
        Debug.Log(BuildGates());
    }

    /// <summary>
    /// (Re)pose uniquement les arches des portes dans LobbyWalls/Gates, sans toucher aux murs ni aux piliers
    /// (qui ont pu être modifiés à la main). Retourne un résumé.
    /// </summary>
    public static string BuildGates()
    {
        MazeGenerator gen = Object.FindFirstObjectByType<MazeGenerator>();
        if (gen == null) return "MazeGenerator introuvable dans la scène";
        GameObject root = GameObject.Find(RootName);
        if (root == null) return "LobbyWalls introuvable : utiliser d'abord « Poser les murs du Lobby dans la scène »";
        int n = PlaceGates(gen, root.transform);
        EditorSceneManager.MarkSceneDirty(root.scene);
        return $"LobbyWalls/{GatesName} : {n} arches posées";
    }

    /// <summary>Pose une arche (Gate.prefab) dans chaque porte du Lobby, à la place du mur absent.</summary>
    private static int PlaceGates(MazeGenerator gen, Transform root)
    {
        GameObject gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GatePrefabPath);
        if (gatePrefab == null) { Debug.LogWarning("LobbyWallsBuilder : " + GatePrefabPath + " introuvable, portes laissées ouvertes."); return 0; }

        Transform old = root.Find(GatesName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Transform gatesT = new GameObject(GatesName).transform;
        gatesT.SetParent(root, false);

        GetWallFrames(out Vector3 frontPos, out Vector3 rightPos, out Quaternion frontRot, out Quaternion rightRot);
        Quaternion modelLocalRot = gatePrefab.transform.GetChild(0).localRotation;
        int cs = gen.GetCellSize();
        float cx = (gen.GetMazeWidth() - 1) * cs / 2f;
        float cz = (gen.GetMazeDepth() - 1) * cs / 2f;
        Vector3 CellOrigin(int x, int z) => new Vector3(x * cs - cx, 0f, z * cs - cz);

        var frontDoors = new List<Vector2Int>();
        var rightDoors = new List<Vector2Int>();
        gen.GetLobbyDoors(frontDoors, rightDoors);
        RectInt lobby = gen.GetLobbyRect();
        int count = 0;

        // Une variante de mesh par point cardinal : seule la rose des vents du fronton change (N, E, S, W).
        var gateMeshes = new Dictionary<string, Mesh>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(GateModelPath))
            if (o is Mesh m) gateMeshes[m.name] = m;

        void Place(string name, string letter, Vector3 cellOrigin, Vector3 relPos, Quaternion wallRot)
        {
            GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab, gatesT);
            g.name = name;
            g.transform.position = cellOrigin + relPos;
            g.transform.rotation = wallRot * Quaternion.Inverse(modelLocalRot);
            if (gateMeshes.TryGetValue(GateMeshPrefix + letter, out Mesh mesh))
                g.GetComponentInChildren<MeshFilter>().sharedMesh = mesh;
            else
                Debug.LogWarning($"LobbyWallsBuilder : mesh {GateMeshPrefix}{letter} introuvable dans {GateModelPath}.");
            count++;
        }

        // +Z = Nord, +X = Est
        foreach (Vector2Int d in frontDoors)
        {
            bool north = d.y >= lobby.y;
            Place(north ? "Gate_Nord" : "Gate_Sud", north ? "N" : "S", CellOrigin(d.x, d.y), frontPos, frontRot);
        }
        foreach (Vector2Int d in rightDoors)
        {
            bool east = d.x >= lobby.x;
            Place(east ? "Gate_Est" : "Gate_Ouest", east ? "E" : "W", CellOrigin(d.x, d.y), rightPos, rightRot);
        }
        return count;
    }

    /// <summary>Position (relative à la cellule) et rotation du modèle des murs "front" et "right" de MazeCell.prefab.</summary>
    private static void GetWallFrames(out Vector3 frontPos, out Vector3 rightPos, out Quaternion frontRot, out Quaternion rightRot)
    {
        GameObject cellAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath);
        MazeCell cellSrc = cellAsset.GetComponent<MazeCell>();
        Transform fModel = cellSrc._frontWall.transform.GetChild(0);
        Transform rModel = cellSrc._rightWall.transform.GetChild(0);
        frontPos = fModel.position - cellAsset.transform.position;
        rightPos = rModel.position - cellAsset.transform.position;
        frontRot = fModel.rotation;
        rightRot = rModel.rotation;
    }
}
