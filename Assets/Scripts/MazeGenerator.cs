using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class MazeGenerator : MonoBehaviour
{
    [SerializeField] private MazeCell _mazeCellPrefab;
    [SerializeField] private int _mazeWidth;
    [SerializeField] private int _mazeDepth;
    [SerializeField] private int _lobbyWidth;
    [SerializeField] private int _lobbyDepth;

    private MazeCell[,] _mazeGrid; //Tableau 2 dimensions

    public void Generate()
    {
        _mazeGrid = new MazeCell[_mazeWidth, _mazeDepth];

        for (int x = 0; x<_mazeWidth; x++)
        {
            for(int z = 0; z<_mazeDepth; z++)
            {
                _mazeGrid[x,z] = Instantiate(_mazeCellPrefab, new Vector3(x,0,z), Quaternion.identity);
            }
        }

        GenerateMaze(null, _mazeGrid[0, 0]);

        AddLobby();
    }

    private void GenerateMaze(MazeCell previousCell, MazeCell currentCell)
    {
        currentCell.Visit();
        ClearWalls(previousCell, currentCell);

        MazeCell nextCell;

        do
        {
            nextCell = GetNextUnvisitedCell(currentCell);

            if (nextCell != null)
            {
                GenerateMaze(currentCell, nextCell);
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
        int x = (int)currentCell.transform.position.x;
        int z = (int)currentCell.transform.position.z;

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

        if (previousCell.transform.position.x < currentCell.transform.position.x)
        {
            previousCell.ClearRightWall();
            currentCell.ClearLeftWall();
            return;
        }

        if (previousCell.transform.position.x > currentCell.transform.position.x)
        {
            previousCell.ClearLeftWall();
            currentCell.ClearRightWall();
            return;
        }

        if (previousCell.transform.position.z < currentCell.transform.position.z)
        {
            previousCell.ClearFrontWall();
            currentCell.ClearBackWall();
            return;
        }

        if (previousCell.transform.position.z > currentCell.transform.position.z)
        {
            previousCell.ClearBackWall();
            currentCell.ClearFrontWall();
            return;
        }
    }

    private void AddLobby() //Suppression des cellules du milieu pour creer un looby
    {
        int startLobbyX = (_mazeWidth - _lobbyWidth) / 2;
        int startLobbyZ = (_mazeDepth - _lobbyDepth) / 2;
        for(int i = startLobbyX; i <= _lobbyWidth + startLobbyX; i++) {
            for(int j = startLobbyZ; j <= _lobbyDepth + startLobbyZ; j++)
            {
                _mazeGrid[i, j].ClearLeftWall();
                _mazeGrid[i, j].ClearRightWall();
                _mazeGrid[i, j].ClearFrontWall();
                _mazeGrid[i, j].ClearBackWall();

                if (i == startLobbyX)
                {
                    _mazeGrid[i, j].AddLeftWall();
                }
                if (i == _lobbyWidth + startLobbyX)
                {
                    _mazeGrid[i, j].AddRightWall();
                }
                if (j == startLobbyZ)
                {
                    _mazeGrid[i, j].AddFrontWall();
                }
                if (j == _lobbyDepth + startLobbyZ)
                {
                    _mazeGrid[i, j].AddBackWall();
                }
            }
        }
    }
}
