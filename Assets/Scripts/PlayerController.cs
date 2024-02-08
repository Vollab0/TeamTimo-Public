using System.Collections;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.Rendering.DebugUI;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{

    private Vector2 _input;
    private CharacterController _characterController;
    private Vector3 _direction;

    [SerializeField] private float smoothTime = 0.05f;
    [SerializeField] private float speed = 15.0f;
    [SerializeField] private float speedBoost = 2.0f;
    [SerializeField] private float jump = 3.0f;
    [SerializeField] private float jumpBoost = 1.2f; // Coeff multiplicateur de la hauteur de saut lors du sprint par rapport a la marche
    [SerializeField] private float gravityMultiplier = 1.0f;
    

    private float _gravity = -9.81f;
    private float _currentVelocity;
    private float _fallSpeed;
    private float _velocity;
    private float _jumpHeight;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _velocity = speed;
        _jumpHeight = 1.0f;
    }

    private void Update()
    {
        ApplyMovement();
        ApplyGravity();
        ApplyRotation();
    }

    private void ApplyMovement()
    {
        _characterController.Move(new Vector3(_direction.x * _velocity, _direction.y * speed * _jumpHeight, _direction.z * _velocity) * Time.deltaTime);
    }
    private void ApplyRotation()
    {
        if (_input.sqrMagnitude == 0) return;
        var targetAngle = Mathf.Atan2(_direction.x, _direction.z) * Mathf.Rad2Deg;        //Calcul la direction dans laquel le personnage doit regarder
        var angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _currentVelocity, smoothTime);  //Permet de smooth le changement de direction
        transform.rotation = Quaternion.Euler(0.0f, angle, 0.0f);
    }
    public void Move(InputAction.CallbackContext context) // Met a jour le vecteur de direction à chaque fois qu'une touche est appuyée
    {
        _input = context.ReadValue<Vector2>();
        _direction = new Vector3(_input.x, 0.0f, _input.y);
    }
    private void ApplyGravity()
    {
        if (_characterController.isGrounded)
        {
            _fallSpeed = 0.0f;
        }
        else
        {
            _fallSpeed += _gravity * gravityMultiplier * Time.deltaTime;
        }
        _direction.y = _fallSpeed;
    }
    public void Jump(InputAction.CallbackContext context)
    {
        if(context.phase == InputActionPhase.Started)
        {
            _direction.y = jump;
            _fallSpeed = jump;
        }
    }

    public void Sprint(InputAction.CallbackContext context)
    {
        if(context.phase == InputActionPhase.Performed) { _velocity = speed * speedBoost; _jumpHeight = jumpBoost;  }
        if(context.phase == InputActionPhase.Canceled) { _velocity = speed; _jumpHeight = 1.0f; }
    }
}
