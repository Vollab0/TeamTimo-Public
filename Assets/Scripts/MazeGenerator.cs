using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.PlayerLoop;

public class MazeGenerator : MonoBehaviour
{
    [SerializeField] private MazeCell _mazeCellPrefab;
    [SerializeField] private int _mazeWidth;
    [SerializeField] private int _mazeDepth;
    [SerializeField] private int _lobbyWidth;
    [SerializeField] private int _lobbyDepth;
    [SerializeField] private int _cellSize;
    [SerializeField] private int _nbrRooms;

    private MazeCell[,] _mazeGrid; //Tableau 2 dimensions
    private GameObject _mazeParent;
    public int[][] _rooms;

    private Vector3 _repositioningOffset;

    void Start()
    {
        // Lance la génération du labyrinthe au début du jeu
        StartCoroutine(Generate());
    }

    public int GetCellSize()
    {
        return _cellSize;
    }

    public int GetMazeWidth()
    {
        return _mazeWidth;
    }

    public int GetMazeDepth()
    {
        return _mazeDepth;
    }


    public IEnumerator Generate()
    {
        CreateMazeParent();

        Initialisation();

        MarkLobbyCellsVisited();

        LocateRooms();

        MarkRoomsCellsVisited(_rooms);

        yield return StartCoroutine(GenerateMaze(null, _mazeGrid[0, 0]));

        AddLobby();

        AddRooms();

        RandomExit();

        Repositioning();

    }

    private void Initialisation()
    {
        _mazeGrid = new MazeCell[_mazeWidth, _mazeDepth];
        _rooms = new int[_nbrRooms][];

        for (int x = 0; x < _mazeWidth; x++)
        {
            for (int z = 0; z < _mazeDepth; z++)
            {
                _mazeGrid[x, z] = Instantiate(_mazeCellPrefab, new Vector3(_cellSize * x, 0, _cellSize * z), Quaternion.identity);
                _mazeGrid[x, z].SetIndices(x, z);
                _mazeGrid[x, z].transform.SetParent(_mazeParent.transform);
            }
        }
    }

    private IEnumerator GenerateMaze(MazeCell previousCell, MazeCell currentCell)
    {
        currentCell.Visit();
        ClearWalls(previousCell, currentCell);

        MazeCell nextCell;

        do
        {
            nextCell = GetNextUnvisitedCell(currentCell);

            if (nextCell != null)
            {
                yield return StartCoroutine(GenerateMaze(currentCell, nextCell));
            }
        } while (nextCell != null);
    }

    private MazeCell GetNextUnvisitedCell(MazeCell currentCell)
    {
        var univisitedCells = GetUnvisitedCells(currentCell);

        return univisitedCells.OrderBy(_ => Random.Range(1, 10)).FirstOrDefault(); //A comprendre
    }

    private IEnumerable<MazeCell> GetUnvisitedCells(MazeCell currentCell)
    {
        int x = currentCell.GetIndices().Item1;
        int z = currentCell.GetIndices().Item2;

        if (x+1< _mazeWidth)
        {
            MazeCell cellToRight = _mazeGrid[x+1,z];

            if(cellToRight.IsVisited == false)
            {
                yield return cellToRight;
            }
        }

        if (z+1< _mazeDepth)
        {
            MazeCell cellToFront = _mazeGrid[x, z+1];

            if (cellToFront.IsVisited == false)
            {
                yield return cellToFront;
            }
        }

        if (x - 1 >= 0)
        {
            MazeCell cellToLeft = _mazeGrid[x - 1, z];

            if (cellToLeft.IsVisited == false)
            {
                yield return cellToLeft;
            }
        }

        if (z - 1 >= 0)
        {
            MazeCell cellToBack = _mazeGrid[x, z - 1];

            if (cellToBack.IsVisited == false)
            {
                yield return cellToBack;
            }
        }
    }

    private void ClearWalls(MazeCell previousCell, MazeCell currentCell)
    {
        if (previousCell == null)
        {
            return; 
        }

        if (previousCell.GetIndices().Item1 < currentCell.GetIndices().Item1)
        {
            previousCell.ClearRightWall();
            currentCell.ClearLeftWall();
            return;
        }

        if (previousCell.GetIndices().Item1 > currentCell.GetIndices().Item1)
        {
            previousCell.ClearLeftWall();
            currentCell.ClearRightWall();
            return;
        }

        if (previousCell.GetIndices().Item2 < currentCell.GetIndices().Item2)
        {
            previousCell.ClearFrontWall();
            currentCell.ClearBackWall();
            return;
        }

        if (previousCell.GetIndices().Item2 > currentCell.GetIndices().Item2)
        {
            previousCell.ClearBackWall();
            currentCell.ClearFrontWall();
            return;
        }
    }

    private void MarkLobbyCellsVisited()
    {
        int startLobbyX = (_mazeWidth - _lobbyWidth) / 2;
        int startLobbyZ = (_mazeDepth - _lobbyDepth) / 2;
        for (int i = startLobbyX; i < _lobbyWidth + startLobbyX; i++)
        {
            for (int j = startLobbyZ; j < _lobbyDepth + startLobbyZ; j++)
            {
                _mazeGrid[i, j].Visit();
            }
        }
    }

    private void AddLobby() //Suppression des cellules du milieu pour creer un looby
    {
        int startLobbyX = (_mazeWidth - _lobbyWidth) / 2;
        int startLobbyZ = (_mazeDepth - _lobbyDepth) / 2;
        for(int i = startLobbyX; i < _lobbyWidth + startLobbyX; i++) {
            for(int j = startLobbyZ; j < _lobbyDepth + startLobbyZ; j++)
            {
                _mazeGrid[i, j].ClearLeftWall();
                _mazeGrid[i, j].ClearRightWall();
                _mazeGrid[i, j].ClearFrontWall();
                _mazeGrid[i, j].ClearBackWall();

                if (i == startLobbyX)
                {
                    _mazeGrid[i, j].AddLeftWall();
                }
                if (i == _lobbyWidth + startLobbyX - 1)
                {
                    _mazeGrid[i, j].AddRightWall();
                }
                if (j == startLobbyZ)
                {
                    _mazeGrid[i, j].AddBackWall();
                }
                if (j == _lobbyDepth + startLobbyZ - 1)
                {
                    _mazeGrid[i, j].AddFrontWall();
                }
            }
        }

        //============= Ajout des 4 portes ==============

        _mazeGrid[startLobbyX, startLobbyZ + ((_lobbyDepth - 1) / 2)].ClearLeftWall();
        _mazeGrid[startLobbyX-1, startLobbyZ + ((_lobbyDepth - 1) / 2)].ClearRightWall();

        _mazeGrid[startLobbyX + _lobbyWidth - 1, startLobbyZ + ((_lobbyDepth - 1) / 2)].ClearRightWall();
        _mazeGrid[startLobbyX + _lobbyWidth, startLobbyZ + ((_lobbyDepth - 1) / 2)].ClearLeftWall();

        _mazeGrid[(_mazeWidth-1)/2, _lobbyDepth + startLobbyZ-1].ClearFrontWall();
        _mazeGrid[(_mazeWidth - 1) / 2, _lobbyDepth + startLobbyZ].ClearBackWall();

        _mazeGrid[(_mazeWidth - 1)/ 2, startLobbyZ].ClearBackWall();
        _mazeGrid[(_mazeWidth - 1) / 2, startLobbyZ-1].ClearFrontWall();
    }

    private void RandomExit()
    {
        int randomSide = Random.Range(1, 5);
        if (randomSide == 1)
        {
            int randomCell = Random.Range(0, _mazeWidth - 1);
            _mazeGrid[randomCell, 0].ClearBackWall();
        }
        else if(randomSide == 2) 
        {
            int randomCell = Random.Range(0, _mazeDepth - 1);
            _mazeGrid[_mazeWidth-1, randomCell].ClearRightWall();
        }
        else if (randomSide == 3)
        {
            int randomCell = Random.Range(0, _mazeDepth - 1);
            _mazeGrid[randomCell, _mazeDepth - 1].ClearFrontWall();
        }
        else
        {
            int randomCell = Random.Range(0, _mazeDepth - 1);
            _mazeGrid[0 , randomCell].ClearLeftWall();
        }
    }

    private void CreateMazeParent()
    {
        if (_mazeParent != null)
        {
            Destroy(_mazeParent);
        }
        _mazeParent = new GameObject("MazeWalls");
    }

    private void Repositioning()
    {
        _repositioningOffset = new Vector3((_mazeWidth-2) * _cellSize / 2, 0.0f, _mazeDepth * _cellSize /2);
        _mazeParent.transform.position -= _repositioningOffset;
    }

    private void LocateRooms()
    {
        for (int i = 0; i < _nbrRooms; i++)
        {
            _rooms[i] = LocateRoom(5, 5);
        }
    }

    private int[] LocateRoom(int width, int depth)
    {
        int startX;
        int startZ;
        int minDistanceFromLobby = 20;
        int minDistanceBetweenRooms = 15;

        // G�n�rer des coordonn�es de salle jusqu'� ce qu'elles soient � une distance ad�quate du lobby et des autres salles
        do
        {
            startX = Random.Range(1, _mazeWidth - width - 1);
            startZ = Random.Range(1, _mazeDepth - depth - 1);
        } while (DistanceToLobby(startX, startZ) < minDistanceFromLobby || DistanceToClosestRoom(startX, startZ) < minDistanceBetweenRooms);

        return new int[] { startX, startZ, width, depth };
    }

    private void AddRooms()
    {
        foreach (var room in _rooms)
        {
            AddRoom(room[0], room[1], room[2], room[3]);
        }
    }

    private void AddRoom(int startX, int startZ, int width, int depth)
    {
        
        for (int i = startX; i < startX + width; i++)
        {
            for (int j = startZ; j < startZ + depth; j++)
            {
                _mazeGrid[i, j].ClearLeftWall();
                _mazeGrid[i, j].ClearRightWall();
                _mazeGrid[i, j].ClearFrontWall();
                _mazeGrid[i, j].ClearBackWall();

                if (i == startX)
                {
                    _mazeGrid[i, j].AddLeftWall();
                }
                if (i == startX + width - 1)
                {
                    _mazeGrid[i, j].AddRightWall();
                }
                if (j == startZ)
                {
                    _mazeGrid[i, j].AddBackWall();
                }
                if (j == startZ + depth - 1)
                {
                    _mazeGrid[i, j].AddFrontWall();
                }
            }
        }

        
        _mazeGrid[startX, startZ + (depth / 2)].ClearLeftWall();
        _mazeGrid[startX - 1, startZ + (depth / 2)].ClearRightWall();

        _mazeGrid[startX + width - 1, startZ + (depth / 2)].ClearRightWall();
        _mazeGrid[startX + width, startZ + (depth / 2)].ClearLeftWall();

        _mazeGrid[(startX + (width / 2)), startZ + depth - 1].ClearFrontWall();
        _mazeGrid[(startX + (width / 2)), startZ + depth].ClearBackWall();

        _mazeGrid[(startX + (width / 2)), startZ].ClearBackWall();
        _mazeGrid[(startX + (width / 2)), startZ - 1].ClearFrontWall();

    }

    private float DistanceToLobby(int x, int z)
    {
        int lobbyX = (_mazeWidth - _lobbyWidth) / 2;
        int lobbyZ = (_mazeDepth - _lobbyDepth) / 2;
        return Mathf.Sqrt(Mathf.Pow(x - lobbyX, 2) + Mathf.Pow(z - lobbyZ, 2));
    }

    private float DistanceToClosestRoom(int x, int z)
    {
        float minDistance = float.MaxValue;

        foreach (var room in _rooms)
        {
            if (room == null)
                continue;

            float distance = Mathf.Sqrt(Mathf.Pow(x - room[0], 2) + Mathf.Pow(z - room[1], 2));
            if (distance < minDistance)
            {
                minDistance = distance;
            }
        }

        return minDistance;
    }

    private void MarkRoomsCellsVisited(int[][] rooms)
    {
        foreach (var room in rooms)
        {
            if (room == null) continue;
            for (int i = room[0]; i < room[2] + room[0]; i++)
            {
                for (int j = room[1]; j < room[3] + room[1]; j++)
                {
                    _mazeGrid[i, j].Visit();
                }
            }
        }
    }

}
