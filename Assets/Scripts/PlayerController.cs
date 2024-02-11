using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

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
    [SerializeField] private float coyoteTimeDuration = 0.1f; //Autorise un leger retard pour le saut lors de la descente d'un rebord



    private float _currentVelocity;
    private float _gravity = -9.81f;
    private float _verticalVelocity;
    private float _velocity;
    private float _jumpHeight;
    private float _coyoteTimeExpiration;
    private Vector3 _newDirection;
    private bool _hasJumped; // Verifie si le joueur a deja sauté pour empêcher le double saut
    private bool _canSprint = true;

    private bool IsGrounded() => _characterController.isGrounded;

    //public override void OnNetworkSpawn()
    //{
    //    if (!IsOwner) Destroy(this);
    //}//Ce script est executé seuleument pour le owner : si il ne l'est pas tous les players sont controllés par une seule personne
    private void Awake() // Se lance une fois à l'execution du script
    {
        _characterController = GetComponent<CharacterController>();
        _velocity = speed;
        _jumpHeight = 1.0f;
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
            _coyoteTimeExpiration = Time.time + coyoteTimeDuration;
            _hasJumped = false;
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
        if ((IsGrounded() || Time.time <= _coyoteTimeExpiration) && !_hasJumped)
        {
            _hasJumped = true;
            _verticalVelocity += jump;
            StartCoroutine(JumpSmoke());
        }
    }//Peut être amelioré avec un CoyoteTime

    public void Sprint(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Started)
        {
            _canSprint = true;
            StartCoroutine(BoostCoroutine());
        }
        else if (context.phase == InputActionPhase.Canceled)
        {
            _canSprint = false;
            _velocity = speed;
            _jumpHeight = 1.0f;
            GetComponentInChildren<VisualEffect>().SendEvent("StopSprint");
        }
    }

    private IEnumerator BoostCoroutine() //Coroutine qui attend que le joueur retombe au sol avant d'activer le sprint
    {
        while (!IsGrounded())
        {
            yield return null; // Attendre un frame avant de vérifier à nouveau
        }
        if(_canSprint)
        {
            _velocity = speed * speedBoost;
            _jumpHeight = jumpBoost;
            GetComponentInChildren<VisualEffect>().SendEvent("StartSprint");
        }
    }

    private IEnumerator JumpSmoke()
    {
        yield return new WaitForSeconds(0.1f);
        while (!IsGrounded())
        {
            yield return null;
        }
        transform.Find("JumpSmoke").gameObject.GetComponentInChildren<VisualEffect>().SendEvent("OnJump");
    }

    

}

