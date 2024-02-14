using Cinemachine;
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
    private Vector3 _cameraForward;
    private CinemachineFreeLook _virtualCamera;

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
    private bool _canSprint = true; // Atteste que le joueur a appuyé sur sprint (afin d'attendre de retomber au sol pour s'activer)
    private bool _sprintActivated;
    private bool _isSprinting;
    private bool _isFalling = false;
    private bool _isJumping = false; // A Definir
    private bool _isWalking;



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
        _virtualCamera = GameObject.FindObjectOfType<CinemachineFreeLook>();
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
            _isFalling = false;
        }
        else
        {
            _verticalVelocity += _gravity * gravityMultiplier * Time.deltaTime;
            if(!_isFalling)
            {
                _isFalling = true;
                StartCoroutine(LandSmoke());
            } 
        }
        _direction.y = _verticalVelocity;
    }

    private void ApplyRotation()
    {
        if (_input.sqrMagnitude == 0) return;
        Vector3 characterOrientation = new Vector3(_newDirection.x, 0.0f, _newDirection.z);
        Quaternion targetRotation = Quaternion.LookRotation(characterOrientation);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothTime * Time.deltaTime);// Lissage de la rotation avec la fonction Slerp (Spherical Linear Interpolation)
    }

    private void ApplyMovement()
    {
        _cameraForward = _characterController.transform.position - _virtualCamera.transform.position;
        _cameraForward.y = 0f;
        Quaternion cameraRotation = Quaternion.LookRotation(_cameraForward);
        _newDirection = new Vector3(_direction.x * _velocity * Time.deltaTime, _direction.y * speed * _jumpHeight *  Time.deltaTime, _direction.z * _velocity * Time.deltaTime);
        _newDirection = cameraRotation * _newDirection;
        _characterController.Move(_newDirection);

        _isWalking = (_characterController.velocity != new Vector3(0.0f, 0.0f, 0.0f));
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
            if(!(transform.GetComponent<CharacterController>().velocity.x == 0.0f && transform.GetComponent<CharacterController>().velocity.z == 0.0f))
            {
                StartCoroutine(JumpSmoke());
            }
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
            _sprintActivated = false;
            _velocity = speed;
            _jumpHeight = 1.0f;
        }
    }

    public bool[] GetState()
    {
        bool[] stateArray = new bool[4];
        stateArray[0] = _isWalking;
        stateArray[1] = _isSprinting;
        stateArray[2] = _isJumping;
        stateArray[3] = _isFalling;
        return stateArray;
    }

    private IEnumerator BoostCoroutine() //Coroutine qui attend que le joueur retombe au sol avant d'activer le sprint
    {
        while (!IsGrounded())
        {
            yield return null; // Attendre un frame avant de vérifier à nouveau
        }
        if(_canSprint)
        {
            _sprintActivated = true;
            _velocity = speed * speedBoost;
            _jumpHeight = jumpBoost;
            StartCoroutine(SprintSmoke());
        }
    }

    private IEnumerator SprintSmoke()
    {
        bool smokeActive = false;
        while(_sprintActivated)
        {
            if(IsGrounded() && !smokeActive && transform.GetComponent<CharacterController>().velocity != new Vector3(0.0f, 0.0f, 0.0f))
            {
                transform.Find("SprintSmoke").gameObject.GetComponentInChildren<VisualEffect>().SendEvent("StartSprint");
                smokeActive = true;
                _isSprinting = true;
            }
            else
            {
                if (IsGrounded() && smokeActive && transform.GetComponent<CharacterController>().velocity == new Vector3(0.0f, 0.0f, 0.0f)) //Desactive la fumée si le joueur n'avance pas
                {
                    transform.Find("SprintSmoke").gameObject.GetComponentInChildren<VisualEffect>().SendEvent("StopSprint");
                    smokeActive = false;
                    _isSprinting = false;
                } else
                {
                    if (!IsGrounded() && smokeActive) //Desactive la fumée si le joueur est en l'air
                    {
                        transform.Find("SprintSmoke").gameObject.GetComponentInChildren<VisualEffect>().SendEvent("StopSprint");
                        smokeActive = false;
                        _isSprinting = false;
                    }
                }
            }
            yield return null;
        }
        if (smokeActive)
        {
            transform.Find("SprintSmoke").gameObject.GetComponentInChildren<VisualEffect>().SendEvent("StopSprint");
            _isSprinting = false;
        }
    }

    private IEnumerator LandSmoke()
    {
        yield return new WaitForSeconds(0.1f);
        while (!IsGrounded())
        {
            yield return null;
        }
        transform.Find("LandSmoke").gameObject.GetComponentInChildren<VisualEffect>().SendEvent("OnLand");
    }

    private IEnumerator JumpSmoke()
    {
        yield return null;
        transform.Find("SprintSmoke").gameObject.GetComponentInChildren<VisualEffect>().SendEvent("StartSprint");
        yield return new WaitForSeconds(0.1f);
        transform.Find("SprintSmoke").gameObject.GetComponentInChildren<VisualEffect>().SendEvent("StopSprint");
    }


}

