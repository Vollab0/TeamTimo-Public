using System.Collections;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Internal;
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
    [SerializeField] private float jumpPower = 5.0f;

    private float _gravity = -9.81f;
    private float _currentVelocity;
    private float _velocity;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        //ApplyMovement();
        //ApplyRotation();
        //ApplyGravity();
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
    public void Move(InputAction.CallbackContext context) // Met a jour le vecteur de direction à chaque fois qu'une touche est appuyée
    {
        _input = context.ReadValue<Vector2>();
        _direction = new Vector3(_input.x, 0.0f, _input.y);
    }
    private void ApplyGravity()
    {
        if (IsGrounded && _direction.y < 0.0f)
        {
            _velocity = 0.0f;
        }
        else
        {
            _velocity += _gravity * gravityMultiplier * Time.deltaTime;
        }
        _direction.y = _velocity;


    }
    public void Jump(InputAction.CallbackContext context)
    {
        Debug.Log(context);
        if (context.phase == InputActionPhase.Performed && IsGrounded)
        {
            _velocity += jumpPower;
        }
    }


    private bool IsGrounded => _characterController.isGrounded;
}
