using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeGenerator : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private MazeCell _mazeCellPrefab;
    [SerializeField] private GameObject _pillarPrefab;

    [Header("Dimensions Maze")]
    [SerializeField] private int _mazeWidth = 20;
    [SerializeField] private int _mazeDepth = 20;
    [SerializeField] private int _cellSize = 4;

    [Header("Lobby Settings")]
    [SerializeField] private int _lobbyWidth = 6;
    [SerializeField] private int _lobbyDepth = 6;

    [Header("Rooms Settings")]
    [SerializeField] private int _nbrRooms = 3;
    // MODIFICATION ICI : Dimensions fixes
    [SerializeField] private int _roomWidth = 4; 
    [SerializeField] private int _roomDepth = 4; 

    [Header("Debug")]
    [SerializeField] private bool _slowMotion = false;
    [Range(0.01f, 1f)] 
    [SerializeField] private float _stepDelay = 0.05f; 

    private MazeCell[,] _mazeGrid;
    private GameObject _mazeParent;
    private List<RectInt> _roomsRects = new List<RectInt>(); 

    // --- GETTERS ---
    public int GetMazeWidth() { return _mazeWidth; }
    public int GetMazeDepth() { return _mazeDepth; }
    public int GetCellSize() { return _cellSize; }
    // ---------------

    void Start()
    {
        StartCoroutine(Generate());
    }

    public IEnumerator Generate()
    {
        // 1. Création et Parentage
        if (_mazeParent != null) Destroy(_mazeParent);
        _mazeParent = new GameObject("MazeEnvironment");
        _mazeParent.transform.SetParent(this.transform); 
        _mazeParent.transform.localPosition = Vector3.zero;

        Initialisation();
        DefineRooms();
        
        CarveAllRooms(); 
        
        GeneratePillars();

        yield return StartCoroutine(GenerateMazePath(null, _mazeGrid[0, 0]));

        ConnectRoomsToMaze();
        CreateRandomExit();

        // 2. Centrage
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

    private void CenterMaze()
    {
        float centerX = (_mazeWidth - 1) * _cellSize / 2f;
        float centerZ = (_mazeDepth - 1) * _cellSize / 2f;
        _mazeParent.transform.localPosition = new Vector3(-centerX, 0, -centerZ);
    }

    private void DefineRooms()
    {
        _roomsRects.Clear();

        // Lobby
        int lx = (_mazeWidth - _lobbyWidth) / 2;
        int lz = (_mazeDepth - _lobbyDepth) / 2;
        _roomsRects.Add(new RectInt(lx, lz, _lobbyWidth, _lobbyDepth));

        // Autres salles
        for (int i = 0; i < _nbrRooms; i++)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                // MODIFICATION ICI : Utilisation des tailles fixes
                int w = _roomWidth;
                int h = _roomDepth;
                
                // On vérifie qu'il y a la place de poser la salle
                if (_mazeWidth - w - 1 <= 1 || _mazeDepth - h - 1 <= 1) continue;

                int rx = Random.Range(1, _mazeWidth - w - 1);
                int rz = Random.Range(1, _mazeDepth - h - 1);
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
            if (x > room.x && x < room.x + room.width && 
                z > room.y && z < room.y + room.height) 
                return true;
        }
        return false;
    }

    private IEnumerator GenerateMazePath(MazeCell previous, MazeCell current)
    {
        current.Visit();
        RemoveWallBetween(previous, current);

        if (_slowMotion) yield return new WaitForSeconds(_stepDelay);

        while (true)
        {
            MazeCell next = GetRandomUnvisitedNeighbor(current);
            if (next != null)
            {
                yield return StartCoroutine(GenerateMazePath(current, next));
            }
            else
            {
                break;
            }
        }
    }

    private MazeCell GetRandomUnvisitedNeighbor(MazeCell cell)
    {
        var (x, z) = cell.GetIndices();
        List<MazeCell> neighbors = new List<MazeCell>();

        if (x + 1 < _mazeWidth && !_mazeGrid[x + 1, z].IsVisited) neighbors.Add(_mazeGrid[x + 1, z]);
        if (x - 1 >= 0 && !_mazeGrid[x - 1, z].IsVisited) neighbors.Add(_mazeGrid[x - 1, z]);
        if (z + 1 < _mazeDepth && !_mazeGrid[x, z + 1].IsVisited) neighbors.Add(_mazeGrid[x, z + 1]);
        if (z - 1 >= 0 && !_mazeGrid[x, z - 1].IsVisited) neighbors.Add(_mazeGrid[x, z - 1]);

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

    private void ConnectRoomsToMaze()
    {
        for (int i = 0; i < _roomsRects.Count; i++)
        {
            RectInt room = _roomsRects[i];
            
            if (i == 0) // LOBBY
            {
                int midX = room.x + room.width / 2;
                int midZ = room.y + room.height / 2;

                if (room.y + room.height < _mazeDepth)
                    _mazeGrid[midX, room.y + room.height - 1].SetWallState("front", false);

                if (room.y > 0) 
                    _mazeGrid[midX, room.y - 1].SetWallState("front", false);
                else 
                    _mazeGrid[midX, 0].SetWallState("back", false);

                if (room.x + room.width < _mazeWidth)
                    _mazeGrid[room.x + room.width - 1, midZ].SetWallState("right", false);

                if (room.x > 0)
                    _mazeGrid[room.x - 1, midZ].SetWallState("right", false);
                else
                    _mazeGrid[0, midZ].SetWallState("left", false);
            }
            else // AUTRES SALLES
            {
                int doorX = room.x + room.width / 2;
                int doorZ = room.y + room.height - 1; 
                if (doorZ + 1 < _mazeDepth) 
                    _mazeGrid[doorX, doorZ].SetWallState("front", false);
            }
        }
    }

    private void CreateRandomExit()
    {
        int randZ = Random.Range(0, _mazeDepth);
        _mazeGrid[_mazeWidth - 1, randZ].SetWallState("right", false);
    }
}