using System;
using UnityEngine;

public class MazeCell : MonoBehaviour
{
    [Header("Walls (Assurez-vous qu'ils n'ont PAS de piliers intégrés)")]
    public GameObject _frontWall; // Mur Z+ (Devant)
    public GameObject _backWall;  // Mur Z- (Derrière) - utile seulement pour les bordures
    public GameObject _leftWall;  // Mur X- (Gauche) - utile seulement pour les bordures
    public GameObject _rightWall; // Mur X+ (Droite)

    [Header("Visuals")]
    [SerializeField] private GameObject _unvisitedBlock;

    public bool IsVisited { get; private set; }
    private int _x, _z;

    public void SetIndices(int x, int z) { _x = x; _z = z; }
    public (int, int) GetIndices() => (_x, _z);

    public void Visit()
    {
        IsVisited = true;
        if (_unvisitedBlock != null) _unvisitedBlock.SetActive(false);
    }

    // Cette méthode force l'état d'un mur spécifique
    public void SetWallState(string wallName, bool active)
    {
        switch (wallName)
        {
            case "front": if(_frontWall) _frontWall.SetActive(active); break;
            case "back": if(_backWall) _backWall.SetActive(active); break;
            case "left": if(_leftWall) _leftWall.SetActive(active); break;
            case "right": if(_rightWall) _rightWall.SetActive(active); break;
        }
    }
}