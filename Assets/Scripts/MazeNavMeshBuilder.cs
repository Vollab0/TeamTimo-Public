using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Netcode;
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
    [SerializeField] private float _wallZoneStartHeight = 45.0f; 
    
    [Tooltip("Epaisseur de la zone vers le haut.")]
    [SerializeField] private float _wallZoneHeight = 15.0f; 

    [Tooltip("Marge supplémentaire ajoutée autour du labyrinthe pour bien englober les murs extérieurs.")]
    [SerializeField] private float _wallZonePadding = 50.0f; // <--- NOUVEAU

    [Header("Réglages Précision")]
    [Tooltip("Décalage manuel appliqué à TOUTES les zones (Sols + WallTop)")]
    [SerializeField] private Vector3 _zonePositionOffset = Vector3.zero;

    [Tooltip("Hauteur des volumes de zone (sols). Doit englober le relief du terrain sous le labyrinthe.")]
    [SerializeField] private float _zoneVolumeHeight = 30f;

    [Tooltip("Hauteur du centre des volumes de zone au-dessus du pied des cellules.")]
    [SerializeField] private float _zoneVolumeCenterY = 10f;
    
    [Header("Debug")]
    [SerializeField] private bool _showDebugGizmos = true;

    #endregion

    #region --- PRIVATE VARIABLES ---

    private int _areaSW, _areaNW, _areaNE, _areaSE;
    private int _areaSWRoom, _areaNWRoom, _areaNERoom, _areaSERoom;
    private int _areaLobby;
    private int _areaWallTop;

    /// <summary>Vrai quand le NavMesh est calculé et l'apparition des mobs lancée (ou quand il n'y a rien à faire chez un client).</summary>
    public bool IsReady { get; private set; }

    // Pour les Gizmos
    private Vector3 _debugWallTopCenter;
    private Vector3 _debugWallTopSize;

    #endregion

    public void BuildNavMeshForZones()
    {
        IsReady = false;

        // Client en réseau : l'IA ne tourne que chez l'hôte, inutile de calculer le NavMesh.
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening && !manager.IsServer)
        {
            IsReady = true;
            return;
        }

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

        // 2. Le Lobby (aire « Lobby », hors du masque des monstres : ils s'arrêtent aux portes)
        RectInt lobby = mazeGenerator.GetLobbyRect();
        CreateNavMeshModifier(lobby, _areaLobby, cellSize, "LobbyModifier", 0);
        LobbyZone.SetBounds(GetWorldCenter(lobby) + _zonePositionOffset, new Vector2(lobby.width * cellSize, lobby.height * cellSize));

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

        // Centre calé sur les cellules réelles (l'objet Maze peut être déplacé dans la scène)
        Vector3 center = GetWorldCenter(new RectInt(0, 0, mazeGenerator.GetMazeWidth(), mazeGenerator.GetMazeDepth()));
        center.y += _wallZoneStartHeight + (_wallZoneHeight / 2f);

        // Position finale avec l'offset manuel
        modifierObj.transform.position = center + _zonePositionOffset;

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

        // Centre calé sur les cellules réelles : avant, il était calculé par rapport à l'objet Maze,
        // et les volumes se décalaient dès que cet objet n'était pas à l'origine de la scène.
        Vector3 center = GetWorldCenter(rect);
        center.y += yOffset + _zoneVolumeCenterY;

        modifierObj.transform.position = center + _zonePositionOffset;

        NavMeshModifierVolume volume = modifierObj.AddComponent<NavMeshModifierVolume>();
        volume.area = areaIndex;
        volume.size = new Vector3(width, _zoneVolumeHeight, depth);
        volume.center = Vector3.zero;
    }

    /// <summary>Centre (monde) d'un rectangle de cellules, au pied des cellules.</summary>
    private Vector3 GetWorldCenter(RectInt rect)
    {
        Vector3 first = mazeGenerator.GetCellWorldPosition(rect.x, rect.y);
        Vector3 last = mazeGenerator.GetCellWorldPosition(rect.x + rect.width - 1, rect.y + rect.height - 1);
        return (first + last) / 2f;
    }

    private IEnumerator BakeNavMeshDelayed()
    {
        yield return null;
        foreach (var surface in navMeshSurfaces)
        {
            if (surface != null)
            {
                System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();

                // Calcul asynchrone : le jeu et le réseau continuent de tourner pendant le calcul.
                if (surface.navMeshData == null)
                {
                    surface.navMeshData = new NavMeshData(surface.agentTypeID)
                    {
                        position = surface.transform.position,
                        rotation = surface.transform.rotation
                    };
                    surface.AddData();
                }
                yield return surface.UpdateNavMesh(surface.navMeshData);

                Debug.Log($"NavMesh : surface « {surface.name} » calculée en {timer.Elapsed.TotalSeconds:F1} s");
            }
        }

        yield return null;
        if (spawnerManager != null) spawnerManager.StartSpawning();
        IsReady = true;
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