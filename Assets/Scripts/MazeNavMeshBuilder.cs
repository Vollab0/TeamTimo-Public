using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class MazeNavMeshBuilder : MonoBehaviour
{
    #region --- SETTINGS ---

    [Header("Références")]
    [SerializeField] private MazeGenerator mazeGenerator;
    [SerializeField] private List<NavMeshSurface> navMeshSurfaces;
    [SerializeField] private SpawnerManager spawnerManager;

    [Header("Réglages WallTop (Zone Haute)")]
    [Tooltip("A partir de quelle hauteur commence la zone 'WallTop' ?")]
    [SerializeField] private float _wallZoneStartHeight = 2.0f; 
    
    [Tooltip("Epaisseur de la zone vers le haut.")]
    [SerializeField] private float _wallZoneHeight = 10.0f; 

    [Tooltip("Marge supplémentaire ajoutée autour du labyrinthe pour bien englober les murs extérieurs.")]
    [SerializeField] private float _wallZonePadding = 50.0f; // <--- NOUVEAU

    [Header("Réglages Précision")]
    [Tooltip("Décalage manuel appliqué à TOUTES les zones (Sols + WallTop)")]
    [SerializeField] private Vector3 _zonePositionOffset = Vector3.zero;
    
    [Header("Debug")]
    [SerializeField] private bool _showDebugGizmos = true;

    #endregion

    #region --- PRIVATE VARIABLES ---

    private int _areaSW, _areaNW, _areaNE, _areaSE;
    private int _areaSWRoom, _areaNWRoom, _areaNERoom, _areaSERoom;
    private int _areaLobby;
    private int _areaWallTop;

    // Pour les Gizmos
    private Vector3 _debugWallTopCenter;
    private Vector3 _debugWallTopSize;

    #endregion

    public void BuildNavMeshForZones()
    {
        UpdateNavMeshAreaIDs();
        ClearOldModifiers();

        int cellSize = mazeGenerator.GetCellSize();
        
        // 1. Les 4 Zones Principales (Sol)
        List<RectInt> zones = mazeGenerator.GetAllZones();
        int[] zoneAreaIDs = new int[] { _areaSW, _areaNW, _areaNE, _areaSE };

        for (int i = 0; i < zones.Count; i++)
        {
            CreateNavMeshModifier(zones[i], zoneAreaIDs[i], cellSize, $"ZoneModifier_{i}", 0);
        }

        // 2. Le Lobby
        CreateNavMeshModifier(mazeGenerator.GetLobbyRect(), _areaLobby, cellSize, "LobbyModifier", 0);

        // 3. Les Salles Spécifiques
        List<RectInt> rooms = mazeGenerator.GetRandomRooms();
        for (int i = 0; i < rooms.Count; i++)
        {
            int specificRoomAreaID = GetAreaIDForRoom(rooms[i], zones);
            CreateNavMeshModifier(rooms[i], specificRoomAreaID, cellSize, $"RoomModifier_{i}", 0);
        }

        // 4. Le Haut des Murs (WallTop)
        CreateWallTopModifier();

        StartCoroutine(BakeNavMeshDelayed());
    }

    private void UpdateNavMeshAreaIDs()
    {
        _areaSW = NavMesh.GetAreaFromName("SouthWest");
        _areaNW = NavMesh.GetAreaFromName("NorthWest");
        _areaNE = NavMesh.GetAreaFromName("NorthEast");
        _areaSE = NavMesh.GetAreaFromName("SouthEast");
        
        _areaSWRoom = NavMesh.GetAreaFromName("SouthWestRoom");
        _areaNWRoom = NavMesh.GetAreaFromName("NorthWestRoom");
        _areaNERoom = NavMesh.GetAreaFromName("NorthEastRoom");
        _areaSERoom = NavMesh.GetAreaFromName("SouthEastRoom");

        _areaLobby = NavMesh.GetAreaFromName("Lobby");
        _areaWallTop = NavMesh.GetAreaFromName("WallTop");
    }

private void ClearOldModifiers()
    {
        // Sécurité 1 : Vérifier si le générateur est assigné
        if (mazeGenerator == null)
        {
            Debug.LogError("Erreur : La référence 'MazeGenerator' est manquante dans l'inspecteur !");
            return;
        }

        Transform mazeParent = mazeGenerator.GetMazeParent();

        // Sécurité 2 : Vérifier si le parent du labyrinthe existe
        if (mazeParent == null)
        {
            // S'il n'y a pas de parent, il n'y a rien à nettoyer, on sort simplement.
            // On peut mettre un Warning si c'est censé ne jamais arriver.
            Debug.LogWarning("Attention : GetMazeParent() a retourné null. Aucun nettoyage effectué.");
            return; 
        }

        List<GameObject> toDestroy = new List<GameObject>();
        
        // Maintenant, on est sûr que mazeParent n'est pas null, le foreach ne plantera pas
        foreach (Transform child in mazeParent)
        {
            if (child.name.Contains("Modifier") || child.name == "GLOBAL_WALL_TOP")
                toDestroy.Add(child.gameObject);
        }
        
        foreach (var obj in toDestroy) Destroy(obj);
    }

    private int GetAreaIDForRoom(RectInt roomRect, List<RectInt> zones)
    {
        Vector2 roomCenter = roomRect.center;
        if (zones[0].Contains(new Vector2Int((int)roomCenter.x, (int)roomCenter.y))) return _areaSWRoom;
        if (zones[1].Contains(new Vector2Int((int)roomCenter.x, (int)roomCenter.y))) return _areaNWRoom;
        if (zones[2].Contains(new Vector2Int((int)roomCenter.x, (int)roomCenter.y))) return _areaNERoom;
        if (zones[3].Contains(new Vector2Int((int)roomCenter.x, (int)roomCenter.y))) return _areaSERoom;
        return NavMesh.GetAreaFromName("Room"); 
    }

    /// <summary>
    /// Crée un Modifier Volume géant pour le haut des murs.
    /// Avec Padding et Offset.
    /// </summary>
    private void CreateWallTopModifier()
    {
        GameObject modifierObj = new GameObject("GLOBAL_WALL_TOP");
        modifierObj.transform.SetParent(mazeGenerator.GetMazeParent(), false);

        // Dimensions théoriques de la grille
        float rawWidth = mazeGenerator.GetMazeWidth() * mazeGenerator.GetCellSize();
        float rawDepth = mazeGenerator.GetMazeDepth() * mazeGenerator.GetCellSize();

        // Calcul du centre de la grille (inchangé pour rester aligné)
        float centerX = (rawWidth / 2f) - (mazeGenerator.GetCellSize() / 2f);
        float centerZ = (rawDepth / 2f) - (mazeGenerator.GetCellSize() / 2f);
        float centerY = _wallZoneStartHeight + (_wallZoneHeight / 2f);

        // Position finale avec l'offset manuel
        Vector3 finalPos = new Vector3(centerX, centerY, centerZ) + _zonePositionOffset;
        modifierObj.transform.localPosition = finalPos;

        NavMeshModifierVolume volume = modifierObj.AddComponent<NavMeshModifierVolume>();
        volume.area = _areaWallTop;

        // AJOUT DU PADDING : On ajoute la marge à la taille totale
        // Cela va étendre le volume dans toutes les directions depuis le centre
        volume.size = new Vector3(rawWidth + _wallZonePadding, _wallZoneHeight, rawDepth + _wallZonePadding);
        volume.center = Vector3.zero;

        // Debug Gizmos
        _debugWallTopCenter = modifierObj.transform.position; 
        _debugWallTopSize = volume.size;
    }

    private void CreateNavMeshModifier(RectInt rect, int areaIndex, int cellSize, string name, float yOffset)
    {
        GameObject modifierObj = new GameObject(name);
        modifierObj.transform.SetParent(mazeGenerator.GetMazeParent(), false);

        float width = rect.width * cellSize;
        float depth = rect.height * cellSize;

        float centerX = (rect.x * cellSize) + (width / 2f) - (cellSize / 2f);
        float centerZ = (rect.y * cellSize) + (depth / 2f) - (cellSize / 2f);

        Vector3 finalPos = new Vector3(centerX, yOffset + 2f, centerZ) + _zonePositionOffset;

        modifierObj.transform.localPosition = finalPos;

        NavMeshModifierVolume volume = modifierObj.AddComponent<NavMeshModifierVolume>();
        volume.area = areaIndex;
        volume.size = new Vector3(width, 10f, depth);
        volume.center = Vector3.zero;
    }

    private IEnumerator BakeNavMeshDelayed()
    {
        yield return null;
        foreach (var surface in navMeshSurfaces)
        {
            if (surface != null)
            {
                surface.BuildNavMesh();
            }
        }

        yield return null;
        if (spawnerManager != null) spawnerManager.StartSpawning();
    }

    private void OnDrawGizmos()
    {
        if (!_showDebugGizmos) return;

        if (_debugWallTopSize != Vector3.zero)
        {
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.3f); // Jaune transparent
            Gizmos.DrawCube(_debugWallTopCenter, _debugWallTopSize);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(_debugWallTopCenter, _debugWallTopSize);
        }
    }
}