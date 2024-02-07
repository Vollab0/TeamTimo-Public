using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private CharacterController characterController;

    public float Speed = 20f;
    public float SprintSpeed = 30f; // Vitesse de sprint
    public KeyCode sprintKey = KeyCode.LeftShift; // Touche de sprint

    private bool isSprinting = false; // Indique si le joueur est en train de sprinter

    void Start()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Vérifie si la touche de sprint est enfoncée
        if (Input.GetKeyDown(sprintKey))
        {
            isSprinting = true;
        }
        else if (Input.GetKeyUp(sprintKey))
        {
            isSprinting = false;
        }

        // Calcul du mouvement en fonction du sprint
        float currentSpeed = isSprinting ? SprintSpeed : Speed;
        Vector3 move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        characterController.Move(move * Time.deltaTime * currentSpeed);
    }
}


