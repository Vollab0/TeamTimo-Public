using UnityEngine;

public class InitialisationPositionObjet : MonoBehaviour
{
    // Rendre cette variable publique pour qu'elle soit visible et accessible 
    // par le script TeleportationObjet.
    [HideInInspector] public Vector3 _positionInitial;

    void Start()
    {
        // Enregistre la position de l'objet au moment du démarrage du jeu
        _positionInitial = transform.position;
    }
}