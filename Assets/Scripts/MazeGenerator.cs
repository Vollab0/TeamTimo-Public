using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MazeGenerator : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private MazeCell _mazeCellPrefab;
    [SerializeField] private GameObject _pillarPrefab;

    [Header("Dimensions Maze")]
    [SerializeField] private int _mazeWidth = 20;
    [SerializeField] private int _mazeDepth = 20;
    [SerializeField] private int _cellSize = 4; // Remis à 4m standard

    [Header("Lobby Settings")]
    [SerializeField] private int _lobbyWidth = 6;
    [SerializeField] private int _lobbyDepth = 6;

    [Header("Rooms Settings")]
    [SerializeField] private int _roomWidth = 4; 
    [SerializeField] private int _roomDepth = 4; 

    [Header("Connections Settings")]
    [Tooltip("Nombre de portes directes entre les zones adjacentes (hors lobby)")]
    [SerializeField] private int _interZoneDoors = 2; // NOUVEAU

    [Tooltip("Nombre de portes reliant le Lobby à chaque zone")]
    [SerializeField] private int _doorsPerLobbySide = 1;

    [Tooltip("Nombre de portes pour les salles aléatoires")]
    [SerializeField] private int _doorsPerRoom = 1;

    [Header("Debug")]
    [SerializeField] private bool _slowMotion = false;
    [Range(0.001f, 1f)] 
    [SerializeField] private float _stepDelay = 0.01f;
    
    [Tooltip("Affiche un sol coloré transparent (URP)")]
    [SerializeField] private bool _showZoneColors = false; 

    private MazeCell[,] _mazeGrid;
    private GameObject _mazeParent;
    private List<RectInt> _roomsRects = new List<RectInt>(); 

    // --- GETTERS ---
    public int GetMazeWidth() { return _mazeWidth; }
    public int GetMazeDepth() { return _mazeDepth; }
    public int GetCellSize() { return _cellSize; }
    public List<RectInt> GetAllZones() { return CalculateQuadrants(); }
    public RectInt GetZone(int index) {
        List<RectInt> z = CalculateQuadrants();
        return (index >= 0 && index < z.Count) ? z[index] : new RectInt(0,0,0,0);
    }
    // ---------------

    void Start()
    {
        StartCoroutine(Generate());
    }

    public IEnumerator Generate()
    {
        if (_mazeParent != null) Destroy(_mazeParent);
        _mazeParent = new GameObject("MazeEnvironment");
        _mazeParent.transform.SetParent(this.transform); 
        _mazeParent.transform.localPosition = Vector3.zero;

        Initialisation();
        DefineRooms(); 
        CarveAllRooms(); 
        GeneratePillars();

        List<RectInt> quadrants = CalculateQuadrants();

        // Génération des 4 labyrinthes isolés
        foreach (RectInt zone in quadrants)
        {
            MazeCell startCell = GetRandomCellInZone(zone);
            if (startCell != null)
                yield return StartCoroutine(GenerateMazePath(null, startCell, zone));
        }

        // Connexions
        ConnectRoomsToMaze(); // Portes du Lobby et Salles
        ConnectAdjacentZones(); // NOUVEAU : Portes entre les zones
        
        CreateRandomExit();
        
        if (_showZoneColors) CreateZoneOverlaysURP(quadrants);

        CenterMaze();
    }

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

                cell.SetWallState("right", true);
                cell.SetWallState("front", true);
                cell.SetWallState("left", x == 0); 
                cell.SetWallState("back", z == 0); 
            }
        }
    }

    // --- NOUVEAU : CONNEXION INTER-LABYRINTHES ---
    private void ConnectAdjacentZones()
    {
        if (_interZoneDoors <= 0) return;

        int midX = _mazeWidth / 2;
        int midZ = _mazeDepth / 2;

        // Limites du Lobby (pour ne pas percer dedans)
        int lobbyX = (_mazeWidth - _lobbyWidth) / 2;
        int lobbyZ = (_mazeDepth - _lobbyDepth) / 2;
        RectInt lobbyRect = new RectInt(lobbyX, lobbyZ, _lobbyWidth, _lobbyDepth);

        // 1. Ligne Verticale (Sépare Gauche / Droite)
        // On connecte Bas-Gauche <-> Bas-Droite (Segment Bas)
        CreatePassageAlongVertical(midX, 0, lobbyZ, _interZoneDoors);
        // On connecte Haut-Gauche <-> Haut-Droite (Segment Haut)
        CreatePassageAlongVertical(midX, lobbyZ + _lobbyDepth, _mazeDepth, _interZoneDoors);

        // 2. Ligne Horizontale (Sépare Bas / Haut)
        // On connecte Bas-Gauche <-> Haut-Gauche (Segment Gauche)
        CreatePassageAlongHorizontal(midZ, 0, lobbyX, _interZoneDoors);
        // On connecte Bas-Droite <-> Haut-Droite (Segment Droite)
        CreatePassageAlongHorizontal(midZ, lobbyX + _lobbyWidth, _mazeWidth, _interZoneDoors);
    }

    // Ouvre le mur "Right" de la cellule à gauche de la ligne de séparation
    private void CreatePassageAlongVertical(int fixedX, int zStart, int zEnd, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int attempts = 0;
            while (attempts < 20) // Sécurité
            {
                int randomZ = Random.Range(zStart, zEnd);
                // On vérifie que le mur existe encore
                // fixedX - 1 est la cellule à gauche du trait
                if (fixedX > 0 && randomZ < _mazeDepth)
                {
                    _mazeGrid[fixedX - 1, randomZ].SetWallState("right", false);
                    break;
                }
                attempts++;
            }
        }
    }

    // Ouvre le mur "Front" de la cellule en dessous de la ligne de séparation
    private void CreatePassageAlongHorizontal(int fixedZ, int xStart, int xEnd, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int attempts = 0;
            while (attempts < 20)
            {
                int randomX = Random.Range(xStart, xEnd);
                // fixedZ - 1 est la cellule en dessous du trait
                if (fixedZ > 0 && randomX < _mazeWidth)
                {
                    _mazeGrid[randomX, fixedZ - 1].SetWallState("front", false);
                    break;
                }
                attempts++;
            }
        }
    }
    // ---------------------------------------------

    private List<RectInt> CalculateQuadrants()
    {
        List<RectInt> quads = new List<RectInt>();
        int midX = _mazeWidth / 2;
        int midZ = _mazeDepth / 2;

        quads.Add(new RectInt(0, 0, midX, midZ)); // Bas-Gauche
        quads.Add(new RectInt(0, midZ, midX, _mazeDepth - midZ)); // Haut-Gauche
        quads.Add(new RectInt(midX, midZ, _mazeWidth - midX, _mazeDepth - midZ)); // Haut-Droite
        quads.Add(new RectInt(midX, 0, _mazeWidth - midX, midZ)); // Bas-Droite

        return quads;
    }

    private void DefineRooms()
    {
        _roomsRects.Clear();
        int lx = (_mazeWidth - _lobbyWidth) / 2;
        int lz = (_mazeDepth - _lobbyDepth) / 2;
        _roomsRects.Add(new RectInt(lx, lz, _lobbyWidth, _lobbyDepth));

        List<RectInt> zones = CalculateQuadrants();
        foreach (RectInt zone in zones)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                int w = _roomWidth; int h = _roomDepth;
                if (zone.width - w <= 1 || zone.height - h <= 1) continue;
                int rx = Random.Range(zone.x + 1, zone.x + zone.width - w - 1);
                int rz = Random.Range(zone.y + 1, zone.y + zone.height - h - 1);
                RectInt newRoom = new RectInt(rx, rz, w, h);
                if (IsRoomValid(newRoom)) { _roomsRects.Add(newRoom); break; }
            }
        }
    }

    private bool IsRoomValid(RectInt room)
    {
        foreach (var existing in _roomsRects)
        {
            RectInt expanded = new RectInt(existing.x - 2, existing.y - 2, existing.width + 4, existing.height + 4);
            if (expanded.Overlaps(room)) return false;
        }
        return true;
    }

    private void CarveAllRooms()
    {
        foreach (var room in _roomsRects)
        {
            for (int x = room.x; x < room.x + room.width; x++)
            {
                for (int z = room.y; z < room.y + room.height; z++)
                {
                    if (x >= _mazeWidth || z >= _mazeDepth) continue;
                    MazeCell current = _mazeGrid[x, z];
                    current.Visit();
                    current.SetWallState("right", false);
                    current.SetWallState("front", false);
                    if (x == 0) current.SetWallState("left", false);
                    if (z == 0) current.SetWallState("back", false);
                    if (x > 0) _mazeGrid[x - 1, z].SetWallState("right", false);
                    if (z > 0) _mazeGrid[x, z - 1].SetWallState("front", false);
                }
            }
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
            if (x > room.x && x < room.x + room.width && z > room.y && z < room.y + room.height) return true;
        }
        return false;
    }

    private MazeCell GetRandomCellInZone(RectInt zone)
    {
        for(int x = zone.x; x < zone.x + zone.width; x++)
            for(int z = zone.y; z < zone.y + zone.height; z++)
                if (!_mazeGrid[x,z].IsVisited) return _mazeGrid[x,z];
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
            if (next != null) yield return StartCoroutine(GenerateMazePath(current, next, bounds));
            else break;
        }
    }

    private MazeCell GetRandomUnvisitedNeighbor(MazeCell cell, RectInt bounds)
    {
        var (x, z) = cell.GetIndices();
        List<MazeCell> neighbors = new List<MazeCell>();
        if (x + 1 < _mazeWidth && x + 1 < bounds.x + bounds.width && !_mazeGrid[x + 1, z].IsVisited) neighbors.Add(_mazeGrid[x + 1, z]);
        if (x - 1 >= 0 && x - 1 >= bounds.x && !_mazeGrid[x - 1, z].IsVisited) neighbors.Add(_mazeGrid[x - 1, z]);
        if (z + 1 < _mazeDepth && z + 1 < bounds.y + bounds.height && !_mazeGrid[x, z + 1].IsVisited) neighbors.Add(_mazeGrid[x, z + 1]);
        if (z - 1 >= 0 && z - 1 >= bounds.y && !_mazeGrid[x, z - 1].IsVisited) neighbors.Add(_mazeGrid[x, z - 1]);
        if (neighbors.Count == 0) return null;
        return neighbors[Random.Range(0, neighbors.Count)];
    }

    private void RemoveWallBetween(MazeCell a, MazeCell b)
    {
        if (a == null || b == null) return;
        var (ax, az) = a.GetIndices(); var (bx, bz) = b.GetIndices();
        if (bx > ax) a.SetWallState("right", false);
        else if (bx < ax) b.SetWallState("right", false);
        else if (bz > az) a.SetWallState("front", false);
        else if (bz < az) b.SetWallState("front", false);
    }

    private void ConnectRoomsToMaze()
    {
        for (int i = 0; i < _roomsRects.Count; i++)
        {
            RectInt room = _roomsRects[i];
            if (i == 0) // LOBBY
            {
                int midX = room.x + (room.width / 2);
                int midZ = room.y + (room.height / 2);
                for (int k = 0; k < _doorsPerLobbySide; k++)
                {
                    if (midX + k < room.x + room.width && room.y + room.height < _mazeDepth) _mazeGrid[midX + k, room.y + room.height - 1].SetWallState("front", false);
                    if (midX - 1 - k >= room.x && room.y > 0) _mazeGrid[midX - 1 - k, room.y - 1].SetWallState("front", false);
                    else if (midX - 1 - k >= room.x && room.y == 0) _mazeGrid[midX - 1 - k, 0].SetWallState("back", false);
                    if (midZ - 1 - k >= room.y && room.x + room.width < _mazeWidth) _mazeGrid[room.x + room.width - 1, midZ - 1 - k].SetWallState("right", false);
                    if (midZ + k < room.y + room.height) { if (room.x > 0) _mazeGrid[room.x - 1, midZ + k].SetWallState("right", false); else _mazeGrid[0, midZ + k].SetWallState("left", false); }
                }
            }
            else // SALLES
            {
                int doorX = room.x + room.width / 2;
                int doorZ = room.y + room.height - 1; 
                if (doorZ + 1 < _mazeDepth) _mazeGrid[doorX, doorZ].SetWallState("front", false); else _mazeGrid[doorX, room.y - 1].SetWallState("front", false);
                if (_doorsPerRoom >= 2 && room.y > 0) _mazeGrid[doorX, room.y - 1].SetWallState("front", false);
                if (_doorsPerRoom >= 3 && room.x + room.width < _mazeWidth) _mazeGrid[room.x + room.width - 1, room.y + room.height / 2].SetWallState("right", false);
                if (_doorsPerRoom >= 4 && room.x > 0) _mazeGrid[room.x - 1, room.y + room.height / 2].SetWallState("right", false);
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

    private void CreateZoneOverlaysURP(List<RectInt> quadrants)
    {
        GameObject overlayParent = new GameObject("DebugZoneOverlays");
        overlayParent.transform.SetParent(_mazeParent.transform);
        Color[] zoneColors = new Color[] { new Color(1f, 0f, 0f, 0.3f), new Color(0f, 0f, 0f, 0.3f), new Color(0f, 1f, 0f, 0.3f), new Color(0f, 0f, 1f, 0.3f) };
        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default"); 
        for (int i = 0; i < quadrants.Count; i++)
        {
            RectInt zone = quadrants[i];
            GameObject floorPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorPlate.transform.SetParent(overlayParent.transform);
            float width = zone.width * _cellSize; float depth = zone.height * _cellSize;
            float centerX = (zone.x * _cellSize) + (width / 2f) - (_cellSize / 2f);
            float centerZ = (zone.y * _cellSize) + (depth / 2f) - (_cellSize / 2f);
            floorPlate.transform.localPosition = new Vector3(centerX, 0.1f, centerZ);
            floorPlate.transform.localScale = new Vector3(width, 0.05f, depth);
            Renderer rend = floorPlate.GetComponent<Renderer>();
            if (rend != null) {
                Material mat = new Material(urpShader);
                mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0); mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); mat.SetInt("_ZWrite", 0); mat.renderQueue = 3000;
                Color col = zoneColors[i % zoneColors.Length]; mat.SetColor("_BaseColor", col); mat.SetColor("_BaseMap", col); mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); 
                rend.material = mat; rend.shadowCastingMode = ShadowCastingMode.Off;
            }
            Destroy(floorPlate.GetComponent<Collider>());
        }
    }

    private void CenterMaze()
    {
        float centerX = (_mazeWidth - 1) * _cellSize / 2f;
        float centerZ = (_mazeDepth - 1) * _cellSize / 2f;
        _mazeParent.transform.localPosition = new Vector3(-centerX, 0, -centerZ);
    }
}