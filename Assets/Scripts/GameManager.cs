using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance; // Permet l'accès global
    public Material globalOutlineMaterial; // Ton matériau jaune unique

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
}