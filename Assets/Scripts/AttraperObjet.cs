using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class AttraperObjets : MonoBehaviour
{
    private GameObject objetEnMain = null;
    private Rigidbody rbObjetEnMain = null;

    public void AttraperObjet(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            if (objetEnMain == null)
            {
                RamasserObjet();
            }
            else
            {
                LacherObjet();
            }
        }
    }

    void RamasserObjet()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, 2f); // Crée une sphere de rayon 2 centrée sur le joueur et renvoie tous les colliders à l'interieur
        foreach (Collider col in colliders)
        {
            // Si l'objet est ramassable
            if (col.CompareTag("Ramassable"))
            {
                // Récupère l'objet et le rend enfant du joueur
                objetEnMain = col.gameObject;
                rbObjetEnMain = objetEnMain.GetComponent<Rigidbody>();
                objetEnMain.transform.SetParent(transform);

                // Ajuste la position de l'objet par rapport au joueur
                Vector3 offset = new Vector3(0.0f, 2f, 1.5f); 
                objetEnMain.transform.localPosition = offset;

                rbObjetEnMain.isKinematic = true;
                break; // Sort de la boucle après avoir trouvé le premier objet ramassable
            }
        }
    }

    public void LacherObjet()
    {
        // Détache l'objet du joueur et lui rend sa physique
        if (objetEnMain != null)
        {
            objetEnMain.transform.SetParent(null);
            rbObjetEnMain.isKinematic = false;
            rbObjetEnMain.velocity = GetComponent<CharacterController>().velocity; // Transfère la vitesse du joueur à l'objet
            objetEnMain = null;
            rbObjetEnMain = null;
        }
    }
}
