using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class DeplacementObstacles : MonoBehaviour
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
            Rigidbody rb = hit.collider.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                // Calcule la force en fonction de la masse de l'objet
                float force = 5f / rb.mass; // Ajustez 10f selon besoins

                // Applique la force en fonction de la direction dans laquelle le personnage le pousse
                rb.AddForce(hit.moveDirection * force, ForceMode.Impulse);
            }
        }
    }
}
