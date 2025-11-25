using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class MazeNavMeshBuilder : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private MazeGenerator mazeGenerator;
    [SerializeField] private NavMeshSurface navMeshSurface;

    [Header("Réglages Précision")]
    [Tooltip("Décalage manuel pour aligner les zones avec les murs (ex: 2, 0, 2)")]
    [SerializeField] private Vector3 zonePositionOffset = Vector3.zero;

    // Index des Areas
    private int areaSW, areaNW, areaNE, areaSE, areaLobby, areaRoom; // NOUVEAU: areaRoom

    public void BuildNavMeshForZones()
    {
        // 1. Récupération des IDs via les noms définis dans Unity
        areaSW = NavMesh.GetAreaFromName("SouthWest");
        areaNW = NavMesh.GetAreaFromName("NorthWest");
        areaNE = NavMesh.GetAreaFromName("NorthEast");
        areaSE = NavMesh.GetAreaFromName("SouthEast");
        areaLobby = NavMesh.GetAreaFromName("Lobby");
        areaRoom = NavMesh.GetAreaFromName("Room"); // NOUVEAU

        // 2. Nettoyage
        Transform mazeParent = mazeGenerator.GetMazeParent();
        foreach (Transform child in mazeParent) 
        { 
            // On supprime aussi les "RoomModifier"
            if (child.name.StartsWith("ZoneModifier_") || child.name == "LobbyModifier" || child.name.StartsWith("RoomModifier_"))
                Destroy(child.gameObject); 
        }

        int cellSize = mazeGenerator.GetCellSize();

        // 3. Création Volumes ZONES (Le sol de base)
        List<RectInt> zones = mazeGenerator.GetAllZones(); 
        int[] areaIndices = new int[] { areaSW, areaNW, areaNE, areaSE };

        for (int i = 0; i < zones.Count; i++)
        {
            CreateNavMeshModifier(zones[i], areaIndices[i], cellSize, $"ZoneModifier_{i}");
        }

        // 4. Création Volume LOBBY (Écrase les zones au centre)
        CreateNavMeshModifier(mazeGenerator.GetLobbyRect(), areaLobby, cellSize, "LobbyModifier");

        // 5. Création Volumes SALLES (Écrase les zones aux endroits des salles) --- NOUVEAU ---
        List<RectInt> rooms = mazeGenerator.GetRandomRooms();
        for (int i = 0; i < rooms.Count; i++)
        {
            CreateNavMeshModifier(rooms[i], areaRoom, cellSize, $"RoomModifier_{i}");
        }

        // 6. Calcul NavMesh
        StartCoroutine(BakeNavMeshDelayed());
    }

    private void CreateNavMeshModifier(RectInt rect, int areaIndex, int cellSize, string name)
    {
        GameObject modifierObj = new GameObject(name);
        modifierObj.transform.SetParent(mazeGenerator.GetMazeParent(), false);

        float width = rect.width * cellSize;
        float depth = rect.height * cellSize;
        
        // Calcul du centre local
        float centerX = (rect.x * cellSize) + (width / 2f) - (cellSize / 2f);
        float centerZ = (rect.y * cellSize) + (depth / 2f) - (cellSize / 2f);

        Vector3 finalPos = new Vector3(centerX, 10f, centerZ) + zonePositionOffset;

        modifierObj.transform.localPosition = finalPos;

        NavMeshModifierVolume volume = modifierObj.AddComponent<NavMeshModifierVolume>();
        volume.area = areaIndex;
        volume.size = new Vector3(width, 50f, depth);
        volume.center = Vector3.zero;
    }

    private IEnumerator BakeNavMeshDelayed()
    {
        yield return null; 
        navMeshSurface.BuildNavMesh();
        //Debug.Log("NavMesh Bake Terminé (Zones + Lobby + Salles) !");
    }
}