using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MazeGenerator : MonoBehaviour
{
    #region --- INSPECTOR SETTINGS ---

    [Header("1. Assets & Prefabs")]
    [SerializeField] private MazeCell _mazeCellPrefab;
    [SerializeField] private GameObject _pillarPrefab;

    [Header("2. Dimensions Maze")]
    [Tooltip("Largeur totale en nombre de cellules")]
    [SerializeField] private int _mazeWidth = 30;
    [Tooltip("Profondeur totale en nombre de cellules")]
    [SerializeField] private int _mazeDepth = 30;
    [Tooltip("Taille d'une cellule en mètres")]
    [SerializeField] private int _cellSize = 50;

    [Header("3. Lobby & Rooms")]
    [SerializeField] private int _lobbyWidth = 6;
    [SerializeField] private int _lobbyDepth = 6;
    [Space(5)]
    [Tooltip("Largeur des salles aléatoires")]
    [SerializeField] private int _roomWidth = 4;
    [Tooltip("Profondeur des salles aléatoires")]
    [SerializeField] private int _roomDepth = 4;

    [Header("4. Connexions (Portes)")]
    [Tooltip("Nombre de passages directs entre les 4 zones (hors lobby)")]
    [SerializeField] private int _interZoneDoors = 2;
    [Tooltip("Nombre de portes reliant le Lobby à chaque zone")]
    [SerializeField] private int _doorsPerLobbySide = 1;
    [Tooltip("Nombre de portes pour les salles aléatoires")]
    [SerializeField] private int _doorsPerRoom = 1;

    [Header("5. Navigation & Debug")]
    [SerializeField] private MazeNavMeshBuilder navMeshBuilder; // Référence au script Builder
    [Space(5)]
    [SerializeField] private bool _slowMotion = false;
    [Range(0.001f, 1f)] [SerializeField] private float _stepDelay = 0.01f;
    [Tooltip("Affiche un sol coloré transparent (URP) pour visualiser les zones")]
    [SerializeField] private bool _showZoneColors = false;

    #endregion

    #region --- PRIVATE VARIABLES ---

    private MazeCell[,] _mazeGrid;
    private GameObject _mazeParent;
    private List<RectInt> _roomsRects = new List<RectInt>();

    #endregion

    #region --- PUBLIC API (GETTERS) ---

    public int GetMazeWidth() { return _mazeWidth; }
    public int GetMazeDepth() { return _mazeDepth; }
    public int GetCellSize() { return _cellSize; }
    
    // Retourne le Transform parent pour que le NavMeshBuilder puisse s'y attacher
    public Transform GetMazeParent() { return _mazeParent != null ? _mazeParent.transform : transform; }

    // Retourne la liste des 4 zones calculées
    public List<RectInt> GetAllZones() { return CalculateQuadrants(); }

    // Retourne une zone spécifique par index (0:SW, 1:NW, 2:NE, 3:SE)
    public RectInt GetZone(int index)
    {
        List<RectInt> z = CalculateQuadrants();
        return (index >= 0 && index < z.Count) ? z[index] : new RectInt(0, 0, 0, 0);
    }

    // Retourne le rectangle du Lobby
    public RectInt GetLobbyRect()
    {
        int lx = (_mazeWidth - _lobbyWidth) / 2;
        int lz = (_mazeDepth - _lobbyDepth) / 2;
        return new RectInt(lx, lz, _lobbyWidth, _lobbyDepth);
    }

    // Récupère la liste des salles (sauf le lobby qui est à l'index 0)
    public List<RectInt> GetRandomRooms()
    {
        List<RectInt> roomsOnly = new List<RectInt>();
        // On commence à 1 pour sauter le Lobby
        for (int i = 1; i < _roomsRects.Count; i++)
        {
            roomsOnly.Add(_roomsRects[i]);
        }
        return roomsOnly;
    }

    #endregion

    #region --- MAIN FLOW ---

    void Start()
    {
        StartCoroutine(Generate());
    }

    public IEnumerator Generate()
    {
        // 1. Nettoyage et Création du Parent
        if (_mazeParent != null) Destroy(_mazeParent);
        _mazeParent = new GameObject("MazeEnvironment");
        _mazeParent.transform.SetParent(this.transform);
        _mazeParent.transform.localPosition = Vector3.zero;

        // 2. Initialisation de la grille
        Initialisation();

        // 3. Définition des emplacements (Lobby + Salles)
        DefineRooms();

        // 4. Creusage des salles (suppression des murs intérieurs)
        CarveAllRooms();

        // 5. Ajout des piliers aux intersections
        GeneratePillars();

        // 6. Génération des 4 labyrinthes distincts (un par zone)
        List<RectInt> quadrants = CalculateQuadrants();
        foreach (RectInt zone in quadrants)
        {
            MazeCell startCell = GetRandomCellInZone(zone);
            if (startCell != null)
                yield return StartCoroutine(GenerateMazePath(null, startCell, zone));
        }

        // 7. Création des connexions (Portes)
        ConnectRoomsToMaze();      // Portes du Lobby et des Salles
        ConnectAdjacentZones();    // Passages secrets entre les zones

        // 8. Sortie du labyrinthe
        CreateRandomExit();

        // 9. Debug Visuel (Optionnel)
        if (_showZoneColors) CreateZoneOverlaysURP(quadrants);

        // 10. Centrage du Labyrinthe
        CenterMaze();

        // 11. Lancement du calcul NavMesh (si le builder est assigné)
        if (navMeshBuilder != null)
        {
            navMeshBuilder.BuildNavMeshForZones();
        }
        else
        {
            Debug.LogWarning("NavMeshBuilder n'est pas assigné dans MazeGenerator !");
        }
    }

    #endregion

    #region --- INITIALIZATION & SETUP ---

    private void Initialisation()
    {
        _mazeGrid = new MazeCell[_mazeWidth, _mazeDepth];

        for (int x = 0; x < _mazeWidth; x++)
        {
            for (int z = 0; z < _mazeDepth; z++)
            {
                Vector3 pos = new Vector3(x * _cellSize, 0, z * _cellSize);
                MazeCell cell = Instantiate(_mazeCellPrefab, pos, Quaternion.identity, _mazeParent.transform);
                cell.name = $"Cell_{x}_{z}";
                cell.SetIndices(x, z);
                _mazeGrid[x, z] = cell;

                // Initialisation : Tous les murs fermés (Règle du mur unique)
                cell.SetWallState("right", true);
                cell.SetWallState("front", true);
                cell.SetWallState("left", x == 0); // Bordure map
                cell.SetWallState("back", z == 0); // Bordure map
            }
        }
    }

    private List<RectInt> CalculateQuadrants()
    {
        List<RectInt> quads = new List<RectInt>();
        int midX = _mazeWidth / 2;
        int midZ = _mazeDepth / 2;

        // L'ordre est important pour la logique des portes
        quads.Add(new RectInt(0, 0, midX, midZ));                               // 0: Bas-Gauche (SouthWest)
        quads.Add(new RectInt(0, midZ, midX, _mazeDepth - midZ));               // 1: Haut-Gauche (NorthWest)
        quads.Add(new RectInt(midX, midZ, _mazeWidth - midX, _mazeDepth - midZ)); // 2: Haut-Droite (NorthEast)
        quads.Add(new RectInt(midX, 0, _mazeWidth - midX, midZ));               // 3: Bas-Droite (SouthEast)

        return quads;
    }

    private void DefineRooms()
    {
        _roomsRects.Clear();

        // 1. Ajouter le Lobby
        _roomsRects.Add(GetLobbyRect());

        // 2. Ajouter une salle par Zone
        List<RectInt> zones = CalculateQuadrants();
        foreach (RectInt zone in zones)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                int w = _roomWidth;
                int h = _roomDepth;

                // Vérifier si la salle rentre dans la zone
                if (zone.width - w <= 1 || zone.height - h <= 1) continue;

                // Random avec marge (+1) pour ne pas toucher les murs séparateurs
                int rx = Random.Range(zone.x + 1, zone.x + zone.width - w - 1);
                int rz = Random.Range(zone.y + 1, zone.y + zone.height - h - 1);

                RectInt newRoom = new RectInt(rx, rz, w, h);

                if (IsRoomValid(newRoom))
                {
                    _roomsRects.Add(newRoom);
                    break;
                }
            }
        }
    }

    private bool IsRoomValid(RectInt room)
    {
        foreach (var existing in _roomsRects)
        {
            // Marge de sécurité de 2 cases autour des salles existantes
            RectInt expanded = new RectInt(existing.x - 2, existing.y - 2, existing.width + 4, existing.height + 4);
            if (expanded.Overlaps(room)) return false;
        }
        return true;
    }

    #endregion

    #region --- WALLS & PILLARS MANAGEMENT ---

    private void CarveAllRooms()
    {
        foreach (var room in _roomsRects)
        {
            // 1. Vider l'intérieur
            for (int x = room.x; x < room.x + room.width; x++)
            {
                for (int z = room.y; z < room.y + room.height; z++)
                {
                    if (x >= _mazeWidth || z >= _mazeDepth) continue;

                    MazeCell current = _mazeGrid[x, z];
                    current.Visit(); // Marqué visité pour que l'algo ne passe pas dedans

                    // Désactiver les murs internes
                    current.SetWallState("right", false);
                    current.SetWallState("front", false);
                    if (x == 0) current.SetWallState("left", false);
                    if (z == 0) current.SetWallState("back", false);

                    // Désactiver les murs entrants des voisins
                    if (x > 0) _mazeGrid[x - 1, z].SetWallState("right", false);
                    if (z > 0) _mazeGrid[x, z - 1].SetWallState("front", false);
                }
            }

            // 2. Reconstruire l'enceinte de la salle
            for (int x = room.x; x < room.x + room.width; x++)
            {
                if (room.y > 0) _mazeGrid[x, room.y - 1].SetWallState("front", true);
                else _mazeGrid[x, 0].SetWallState("back", true);

                _mazeGrid[x, room.y + room.height - 1].SetWallState("front", true);
            }
            for (int z = room.y; z < room.y + room.height; z++)
            {
                if (room.x > 0) _mazeGrid[room.x - 1, z].SetWallState("right", true);
                else _mazeGrid[0, z].SetWallState("left", true);

                _mazeGrid[room.x + room.width - 1, z].SetWallState("right", true);
            }
        }
    }

    private void GeneratePillars()
    {
        GameObject pillarsParent = new GameObject("Pillars");
        pillarsParent.transform.SetParent(_mazeParent.transform);
        float offset = _cellSize / 2f;

        for (int x = 0; x <= _mazeWidth; x++)
        {
            for (int z = 0; z <= _mazeDepth; z++)
            {
                // Ne pas mettre de pilier si on est DANS une salle
                if (IsPointInsideAnyRoom(x, z)) continue;

                Vector3 pos = new Vector3(x * _cellSize - offset, 0, z * _cellSize - offset);
                Instantiate(_pillarPrefab, pos, Quaternion.identity, pillarsParent.transform);
            }
        }
    }

    private bool IsPointInsideAnyRoom(int x, int z)
    {
        foreach (var room in _roomsRects)
        {
            // Strictement à l'intérieur (> et <)
            if (x > room.x && x < room.x + room.width &&
                z > room.y && z < room.y + room.height)
                return true;
        }
        return false;
    }

    #endregion

    #region --- MAZE ALGORITHM (RECURSIVE BACKTRACKER) ---

    private MazeCell GetRandomCellInZone(RectInt zone)
    {
        // Cherche une cellule non visitée dans la zone
        for (int x = zone.x; x < zone.x + zone.width; x++)
            for (int z = zone.y; z < zone.y + zone.height; z++)
                if (!_mazeGrid[x, z].IsVisited) return _mazeGrid[x, z];
        return null;
    }

    private IEnumerator GenerateMazePath(MazeCell previous, MazeCell current, RectInt bounds)
    {
        current.Visit();
        RemoveWallBetween(previous, current);

        if (_slowMotion) yield return new WaitForSeconds(_stepDelay);

        while (true)
        {
            MazeCell next = GetRandomUnvisitedNeighbor(current, bounds);
            if (next != null)
                yield return StartCoroutine(GenerateMazePath(current, next, bounds));
            else
                break;
        }
    }

    private MazeCell GetRandomUnvisitedNeighbor(MazeCell cell, RectInt bounds)
    {
        var (x, z) = cell.GetIndices();
        List<MazeCell> neighbors = new List<MazeCell>();

        // Vérifie les limites globales ET les limites de la Zone (bounds)
        if (x + 1 < _mazeWidth && x + 1 < bounds.x + bounds.width && !_mazeGrid[x + 1, z].IsVisited)
            neighbors.Add(_mazeGrid[x + 1, z]);

        if (x - 1 >= 0 && x - 1 >= bounds.x && !_mazeGrid[x - 1, z].IsVisited)
            neighbors.Add(_mazeGrid[x - 1, z]);

        if (z + 1 < _mazeDepth && z + 1 < bounds.y + bounds.height && !_mazeGrid[x, z + 1].IsVisited)
            neighbors.Add(_mazeGrid[x, z + 1]);

        if (z - 1 >= 0 && z - 1 >= bounds.y && !_mazeGrid[x, z - 1].IsVisited)
            neighbors.Add(_mazeGrid[x, z - 1]);

        if (neighbors.Count == 0) return null;
        return neighbors[Random.Range(0, neighbors.Count)];
    }

    private void RemoveWallBetween(MazeCell a, MazeCell b)
    {
        if (a == null || b == null) return;
        var (ax, az) = a.GetIndices();
        var (bx, bz) = b.GetIndices();

        if (bx > ax) a.SetWallState("right", false);
        else if (bx < ax) b.SetWallState("right", false);
        else if (bz > az) a.SetWallState("front", false);
        else if (bz < az) b.SetWallState("front", false);
    }

    #endregion

    #region --- CONNECTIONS & DOORS ---

    private void ConnectRoomsToMaze()
    {
        for (int i = 0; i < _roomsRects.Count; i++)
        {
            RectInt room = _roomsRects[i];

            if (i == 0) // --- LOBBY ---
            {
                int midX = room.x + (room.width / 2);
                int midZ = room.y + (room.height / 2);

                for (int k = 0; k < _doorsPerLobbySide; k++)
                {
                    // HAUT -> Vers Zone NE
                    if (midX + k < room.x + room.width && room.y + room.height < _mazeDepth)
                        _mazeGrid[midX + k, room.y + room.height - 1].SetWallState("front", false);

                    // BAS -> Vers Zone SW
                    if (midX - 1 - k >= room.x)
                    {
                        if (room.y > 0) _mazeGrid[midX - 1 - k, room.y - 1].SetWallState("front", false);
                        else _mazeGrid[midX - 1 - k, 0].SetWallState("back", false);
                    }

                    // DROITE -> Vers Zone SE
                    if (midZ - 1 - k >= room.y && room.x + room.width < _mazeWidth)
                        _mazeGrid[room.x + room.width - 1, midZ - 1 - k].SetWallState("right", false);

                    // GAUCHE -> Vers Zone NW
                    if (midZ + k < room.y + room.height)
                    {
                        if (room.x > 0) _mazeGrid[room.x - 1, midZ + k].SetWallState("right", false);
                        else _mazeGrid[0, midZ + k].SetWallState("left", false);
                    }
                }
            }
            else // --- SALLES ALÉATOIRES ---
            {
                int doorX = room.x + room.width / 2;
                int doorZ = room.y + room.height - 1;

                // Porte Principale
                if (doorZ + 1 < _mazeDepth) _mazeGrid[doorX, doorZ].SetWallState("front", false);
                else _mazeGrid[doorX, room.y - 1].SetWallState("front", false);

                // Portes Supplémentaires
                if (_doorsPerRoom >= 2 && room.y > 0)
                    _mazeGrid[doorX, room.y - 1].SetWallState("front", false);
                if (_doorsPerRoom >= 3 && room.x + room.width < _mazeWidth)
                    _mazeGrid[room.x + room.width - 1, room.y + room.height / 2].SetWallState("right", false);
                if (_doorsPerRoom >= 4 && room.x > 0)
                    _mazeGrid[room.x - 1, room.y + room.height / 2].SetWallState("right", false);
            }
        }
    }

    private void ConnectAdjacentZones()
    {
        if (_interZoneDoors <= 0) return;

        int midX = _mazeWidth / 2;
        int midZ = _mazeDepth / 2;
        int lobbyX = (_mazeWidth - _lobbyWidth) / 2;
        int lobbyZ = (_mazeDepth - _lobbyDepth) / 2;

        // 1. Passages Verticaux (Gauche <-> Droite)
        // Segment Bas (SW <-> SE)
        CreatePassageAlongVertical(midX, 0, lobbyZ, _interZoneDoors);
        // Segment Haut (NW <-> NE)
        CreatePassageAlongVertical(midX, lobbyZ + _lobbyDepth, _mazeDepth, _interZoneDoors);

        // 2. Passages Horizontaux (Bas <-> Haut)
        // Segment Gauche (SW <-> NW)
        CreatePassageAlongHorizontal(midZ, 0, lobbyX, _interZoneDoors);
        // Segment Droite (SE <-> NE)
        CreatePassageAlongHorizontal(midZ, lobbyX + _lobbyWidth, _mazeWidth, _interZoneDoors);
    }

    private void CreatePassageAlongVertical(int fixedX, int zStart, int zEnd, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int attempts = 0;
            while (attempts++ < 20)
            {
                int randomZ = Random.Range(zStart, zEnd);
                if (fixedX > 0 && randomZ < _mazeDepth)
                {
                    _mazeGrid[fixedX - 1, randomZ].SetWallState("right", false);
                    break;
                }
            }
        }
    }

    private void CreatePassageAlongHorizontal(int fixedZ, int xStart, int xEnd, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int attempts = 0;
            while (attempts++ < 20)
            {
                int randomX = Random.Range(xStart, xEnd);
                if (fixedZ > 0 && randomX < _mazeWidth)
                {
                    _mazeGrid[randomX, fixedZ - 1].SetWallState("front", false);
                    break;
                }
            }
        }
    }

    private void CreateRandomExit()
    {
        int side = Random.Range(0, 4);
        int randX = Random.Range(0, _mazeWidth);
        int randZ = Random.Range(0, _mazeDepth);

        switch (side)
        {
            case 0: _mazeGrid[randX, 0].SetWallState("back", false); break;
            case 1: _mazeGrid[_mazeWidth - 1, randZ].SetWallState("right", false); break;
            case 2: _mazeGrid[randX, _mazeDepth - 1].SetWallState("front", false); break;
            case 3: _mazeGrid[0, randZ].SetWallState("left", false); break;
        }
    }

    #endregion

    #region --- UTILS & DEBUG ---

    private void CenterMaze()
    {
        float centerX = (_mazeWidth - 1) * _cellSize / 2f;
        float centerZ = (_mazeDepth - 1) * _cellSize / 2f;
        _mazeParent.transform.localPosition = new Vector3(-centerX, 0, -centerZ);
    }

    private void CreateZoneOverlaysURP(List<RectInt> quadrants)
    {
        GameObject overlayParent = new GameObject("DebugZoneOverlays");
        overlayParent.transform.SetParent(_mazeParent.transform);

        Color[] zoneColors = new Color[] {
            new Color(1f, 0f, 0f, 0.3f), // Rouge (SW)
            new Color(0f, 0f, 0f, 0.3f), // Noir (NW)
            new Color(0f, 1f, 0f, 0.3f), // Vert (NE)
            new Color(0f, 0f, 1f, 0.3f)  // Bleu (SE)
        };

        // Fallback shader si URP Lit n'est pas trouvé
        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");

        for (int i = 0; i < quadrants.Count; i++)
        {
            RectInt zone = quadrants[i];
            GameObject floorPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorPlate.name = "ZoneOverlay_" + i;
            floorPlate.transform.SetParent(overlayParent.transform);

            float width = zone.width * _cellSize;
            float depth = zone.height * _cellSize;
            float centerX = (zone.x * _cellSize) + (width / 2f) - (_cellSize / 2f);
            float centerZ = (zone.y * _cellSize) + (depth / 2f) - (_cellSize / 2f);

            floorPlate.transform.localPosition = new Vector3(centerX, 0.1f, centerZ);
            floorPlate.transform.localScale = new Vector3(width, 0.05f, depth);

            Renderer rend = floorPlate.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(urpShader);
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetFloat("_Blend", 0);
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;

                Color col = zoneColors[i % zoneColors.Length];
                mat.SetColor("_BaseColor", col);
                mat.SetColor("_BaseMap", col);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                rend.material = mat;
                rend.shadowCastingMode = ShadowCastingMode.Off;
            }
            Destroy(floorPlate.GetComponent<Collider>());
        }
    }

    #endregion
}