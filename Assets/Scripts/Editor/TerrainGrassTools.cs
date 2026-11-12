using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Outil d'éditeur : applique les réglages d'herbe (TerrainGrassSettings) au terrain fait main du Lobby.
/// L'herbe est repeinte d'après la splatmap : peignez la couche de terre dans l'outil Terrain,
/// puis relancez ce menu pour que l'herbe suive.
/// </summary>
public static class TerrainGrassTools
{
    private const string MenuPath = "TeamTimo/Terrain/Appliquer l'herbe au terrain du Lobby";

    [MenuItem(MenuPath)]
    public static void ApplyGrassToLobby()
    {
        TerrainGenerator generator = Object.FindFirstObjectByType<TerrainGenerator>();
        if (generator == null)
        {
            EditorUtility.DisplayDialog("Herbe du Lobby", "Aucun TerrainGenerator dans la scène ouverte.", "OK");
            return;
        }

        var so = new SerializedObject(generator);
        var settings = so.FindProperty("grassSettings").objectReferenceValue as TerrainGrassSettings;
        var grassLayer = so.FindProperty("grassLayer").objectReferenceValue as TerrainLayer;
        var lobbyData = so.FindProperty("manualCenterData").objectReferenceValue as TerrainData;
        if (settings == null || lobbyData == null)
        {
            EditorUtility.DisplayDialog("Herbe du Lobby", "Renseignez « Grass Settings » et « Manual Center Data » sur le TerrainGenerator.", "OK");
            return;
        }

        Terrain lobby = null;
        foreach (Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            if (terrain.terrainData == lobbyData) lobby = terrain;
        }
        if (lobby == null)
        {
            EditorUtility.DisplayDialog("Herbe du Lobby", "Le terrain du Lobby n'est pas dans la scène ouverte.", "OK");
            return;
        }

        Undo.RegisterCompleteObjectUndo(new Object[] { lobbyData, lobby }, "Appliquer l'herbe au Lobby");
        Paint(lobby, settings, grassLayer);
        EditorUtility.SetDirty(lobbyData);
        EditorUtility.SetDirty(lobby);
        EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        Debug.Log($"Herbe appliquée au terrain « {lobby.name} » ({settings.name}).");
    }

    /// <summary>Repeint l'herbe d'un terrain d'après sa couche <paramref name="grassLayer"/> (le reste = terre).</summary>
    public static void Paint(Terrain terrain, TerrainGrassSettings settings, TerrainLayer grassLayer)
    {
        TerrainData data = terrain.terrainData;
        int res = data.alphamapResolution;
        var grassWeight = new float[res, res];
        int grassIndex = System.Array.IndexOf(data.terrainLayers, grassLayer);
        if (grassIndex < 0)
        {
            Debug.LogWarning($"La couche d'herbe « {(grassLayer ? grassLayer.name : "null")} » n'est pas utilisée par {terrain.name} : herbe partout.");
            for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) grassWeight[z, x] = 1f;
        }
        else
        {
            float[,,] maps = data.GetAlphamaps(0, 0, res, res);
            for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) grassWeight[z, x] = maps[z, x, grassIndex];
        }

        settings.ApplyPrototypes(data);
        int[][,] buffers = null;
        settings.PaintDetails(data, grassWeight, terrain.transform.position, 12345, ref buffers);
        settings.ApplyRendering(terrain);
    }
}
