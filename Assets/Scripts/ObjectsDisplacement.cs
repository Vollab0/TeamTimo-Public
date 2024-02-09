using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeplacementObjet : MonoBehaviour
{
    private CharacterController characterController;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Vérifie si le personnage est en collision avec cet objet
        if (hit.gameObject.CompareTag("Obstacle"))
        {
            // Déplace l'objet en fonction de la direction dans laquelle le personnage le pousse
            Rigidbody rb = hit.collider.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                rb.velocity = hit.moveDirection * 5f;
            }
        }
    }
}
