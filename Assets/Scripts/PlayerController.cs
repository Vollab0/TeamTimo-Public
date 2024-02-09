using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    
    private Vector2 _input;
    private CharacterController _characterController;
    private Vector3 _direction;

    [SerializeField] private float speed = 15f;
    [SerializeField] private float speedBoost = 2f;
    [SerializeField] private float jump = 3.0f;
    [SerializeField] private float jumpBoost = 2f;
    [SerializeField] private float smoothTime = 0.05f;
    [SerializeField] private float gravityMultiplier = 3.0f;
    

    private float _currentVelocity;
    private float _gravity = -9.81f;
    private float _verticalVelocity;
    private float _velocity;
    private float _jumpHeight;
    private Vector3 _newDirection;



    private void Awake() // Se lance une fois à l'execution du script
    {
        _characterController = GetComponent<CharacterController>();
        _velocity = speed;
    }

    private void Update()
    {
        ApplyGravity();
        ApplyRotation();
        ApplyMovement();
    }

    private void ApplyGravity()
    {
        if (IsGrounded() && _verticalVelocity < 0.0f)
        {
            _verticalVelocity = -1.0f;
        }
        else
        {
            _verticalVelocity += _gravity * gravityMultiplier * Time.deltaTime;
        }

        _direction.y = _verticalVelocity;
    }

    private void ApplyRotation()
    {
        if (_input.sqrMagnitude == 0) return;

        var targetAngle = Mathf.Atan2(_direction.x, _direction.z) * Mathf.Rad2Deg;
        var angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _currentVelocity, smoothTime);
        transform.rotation = Quaternion.Euler(0.0f, angle, 0.0f);
    }

    private void ApplyMovement()
    {
        _newDirection = new Vector3(_direction.x * _velocity * Time.deltaTime, _direction.y * speed * _jumpHeight *  Time.deltaTime, _direction.z * _velocity * Time.deltaTime);
        _characterController.Move(_newDirection);
    }

    public void Move(InputAction.CallbackContext context)
    {
        _input = context.ReadValue<Vector2>();
        _direction = new Vector3(_input.x, 0.0f, _input.y);
    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (!context.started) return;
        if (!IsGrounded()) return;

        _verticalVelocity += jump;
    }

    public void Sprint(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Started)
        {
            StartCoroutine(BoostCoroutine());
        }
        else if (context.phase == InputActionPhase.Canceled)
        {
            _velocity = speed;
            _jumpHeight = 1.0f;
        }
    }

    private IEnumerator BoostCoroutine() //Coroutine qui attend que le joueur retombe au sol avant d'activer le sprint
    {
        while (!IsGrounded())
        {
            yield return null; // Attendre un frame avant de vérifier à nouveau
        }
        _velocity = speed * speedBoost;
        _jumpHeight = jumpBoost;
    }
    private bool IsGrounded() => _characterController.isGrounded;

    //Optimisation possible du saut : le rendre moins punitif par rapport aux descentes de rebords

}

