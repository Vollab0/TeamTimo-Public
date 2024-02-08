using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{

    private Vector2 _input;
    private CharacterController _characterController;
    private Vector3 _direction;

    [SerializeField] private float smoothTime = 0.05f;
    [SerializeField] private float speed = 10.0f;
    [SerializeField] private float gravityMultiplier = 3.0f;
    private float _gravity = -9.81f;
    private float _currentVelocity;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        ApplyMovement();
        ApplyRotation();
        ApplyGravity();
    }

    private void ApplyMovement()
    {
        _characterController.Move(_direction * speed * Time.deltaTime);
    }
    private void ApplyRotation()
    {
        if (_input.sqrMagnitude == 0) return;
        var targetAngle = Mathf.Atan2(_direction.x, _direction.z) * Mathf.Rad2Deg;        //Calcul la direction dans laquel le personnage doit regarder
        var angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _currentVelocity, smoothTime);  //Permet de smooth le changement de direction
        transform.rotation = Quaternion.Euler(0.0f, angle, 0.0f);
    }
    public void Move(InputAction.CallbackContext context)
    {
        _input = context.ReadValue<Vector2>();
        _direction = new Vector3(_input.x, 0.0f, _input.y);
        Debug.Log(_input);
    }
    private void ApplyGravity()
    {
        if (_characterController.isGrounded)
        {
            _direction.y = 0.0f;
        }
        else
        {
            _direction.y += _gravity * gravityMultiplier * Time.deltaTime;
        }
        
    }

}
