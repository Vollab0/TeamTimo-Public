using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeCell : MonoBehaviour
{
    [SerializeField] private GameObject _frontWall;
    [SerializeField] private GameObject _backWall;
    [SerializeField] private GameObject _leftWall;
    [SerializeField] private GameObject _rightWall;
    [SerializeField] private GameObject _unvisitedBlock;

    public bool IsVisited { get; private set; }

    private Tuple<int, int> indices;

    public Tuple<int, int> GetIndices()
    {
        return indices;
    }

    public void SetIndices(int i, int j)
    {
        indices = new Tuple<int, int>(i, j);
    }

    public void Visit()
    {
        IsVisited = true;
        _unvisitedBlock.SetActive(false);
    }

    public void ClearLeftWall() 
    { 
        _leftWall.SetActive(false);
    }

    public void ClearRightWall()
    {
        _rightWall.SetActive(false);
    }

    public void ClearFrontWall() 
    { 
        _frontWall.SetActive(false);
    }

    public void ClearBackWall()
    {
        _backWall.SetActive(false);
    }

    public void AddLeftWall()
    {
        _leftWall.SetActive(true);
    }

    public void AddRightWall()
    {
        _rightWall.SetActive(true);
    }

    public void AddFrontWall()
    {
        _frontWall.SetActive(true);
    }

    public void AddBackWall()
    {
        _backWall.SetActive(true);
    }
}
