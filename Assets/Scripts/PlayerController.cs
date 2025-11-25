using Cinemachine;
using System;
using System.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
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


    [SerializeField] private float speed = 15f;
    [SerializeField] private float speedBoost = 2f;
    [SerializeField] private float jump = 4.0f;
    [SerializeField] private float jumpBoost = 2f;
    [SerializeField] private float smoothTime = 10f;
    [SerializeField] private float gravityMultiplier = 3.0f;
    [SerializeField] private float coyoteTimeDuration = 0.1f; //Autorise un leger retard pour le saut lors de la descente d'un rebord
    [SerializeField] private float jumpBufferDuration = 0.15f; // Durée du buffer
    [SerializeField] private bool _isThirdPerson = true;
    public CinemachineVirtualCamera firstPersonCamera;
    public CinemachineFreeLook thirdPersonCamera;


    private float _gravity = -9.81f;
    private float _potionSpeedMultiplier = 1f;
    private float _verticalVelocity;
    private float _velocity;
    private float _jumpHeight;
    private float _coyoteTimeExpiration;
    private float _jumpBufferExpiration;
    private Vector3 _newDirection;
    private bool _hasJumped; // Verifie si le joueur a deja saut pour empcher le double saut
    private bool _canSprint = true; // Atteste que le joueur a appuy sur sprint (afin d'attendre de retomber au sol pour s'activer)
    private bool _sprintActivated;
    private bool _isSprinting;
    private bool _isFalling = false;
    private bool _isJumping = false;
    private bool _isWalking;



    private bool IsGrounded() => _characterController.isGrounded;

    //public override void OnNetworkSpawn()
    //{
    //    if (!IsOwner) Destroy(this);
    //}//Ce script est execut seuleument pour le owner : si il ne l'est pas tous les players sont controlls par une seule personne
    private void Awake() // Se lance une fois  l'execution du script
    {
        _characterController = GetComponent<CharacterController>();
        _velocity = speed;
        _jumpHeight = 1.0f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (_isThirdPerson)
        {
            thirdPersonCamera.gameObject.SetActive(true);
        }
        else
        {
            firstPersonCamera.gameObject.SetActive(true);
        }

        //playerCamera = GameObject.FindObjectOfType<CinemachineFreeLook>();
    }

    private void Update()
    {
        ApplyGravity();
        ApplyRotation();
        ApplyMovement();
        HandleJump();
    }

    private void ApplyGravity()
    {
        if (IsGrounded() && _verticalVelocity < 0.0f)
        {
            _verticalVelocity = -1.0f;
            _coyoteTimeExpiration = Time.time + coyoteTimeDuration;
            _hasJumped = false;
            
            // Réinitialiser les états en touchant le sol
            _isFalling = false;
            _isJumping = false; // 🛑 Réinitialisation de isJumping à l'atterrissage
        }
        else
        {
            _verticalVelocity += _gravity * gravityMultiplier * Time.deltaTime; // verticalVelocity augmente au fur du temps donc il y a une acceleration ( operateur += )
            
            // Logique de séparation des phases (ascendante vs descendante)
            if (_verticalVelocity > 0.0f) // Phase ascendante
            {
                // Si on monte, on est en train de sauter
                _isJumping = true;
                _isFalling = false;
            }
            else if (_verticalVelocity < 0.0f) // Phase descendante
            {
                // Si on descend (et qu'on n'est pas au sol, vérifié par le 'else' principal), on chute
                _isJumping = false; // 🛑 Le saut est terminé, on passe à la chute
                
                if(!_isFalling)
                {
                    _isFalling = true; // Déclenche la chute
                    StartCoroutine(LandSmoke()); // L'ancienne logique LandSmoke est déplacée ici
                }
            }
        }
        _direction.y = _verticalVelocity;
    }

    private void ApplyRotation()
    {
        if (_isThirdPerson)
        {
            if (_input.sqrMagnitude == 0) return;
            Vector3 characterOrientation = new Vector3(_newDirection.x, 0.0f, _newDirection.z);
            Quaternion targetRotation = Quaternion.LookRotation(characterOrientation); // Quaternion.LookRotation permet de transformer un vecteur en Quaternion
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothTime * Time.deltaTime);// Lissage de la rotation avec la fonction Slerp (Spherical Linear Interpolation)
        }
        else // Pour la vue à la première personne
        {
            Quaternion targetRotation = Quaternion.LookRotation(_cameraForward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothTime* Time.deltaTime);
        }

    }

    private void ApplyMovement()
    {
        if (_isThirdPerson)
        {
            _cameraForward = _characterController.transform.position - thirdPersonCamera.gameObject.transform.position;
        }
        else // Pour la vue à la première personne
        {
            float cameraAngle = firstPersonCamera.State.RawOrientation.eulerAngles.y;
            float angleInRadians = cameraAngle * Mathf.Deg2Rad;
            _cameraForward = new Vector3(Mathf.Sin(angleInRadians), 0f, Mathf.Cos(angleInRadians));
        }

        _cameraForward.y = 0f;
        Quaternion cameraRotation = Quaternion.LookRotation(_cameraForward);

        // --- CORRECTION ICI ---
        _newDirection = new Vector3(
             _direction.x * _velocity * _potionSpeedMultiplier * Time.deltaTime,
             _direction.y * speed * _jumpHeight * Time.deltaTime,
             _direction.z * _velocity * _potionSpeedMultiplier * Time.deltaTime
        ); // On ferme bien la parenthèse et on met le point virgule ici

        _newDirection = cameraRotation * _newDirection; // Ceci doit être sur une nouvelle ligne

        _characterController.Move(_newDirection);
        _isWalking = _input.sqrMagnitude > 0.01f;
    }

    public void Move(InputAction.CallbackContext context)
    {
        _input = context.ReadValue<Vector2>();
        _direction = new Vector3(_input.x, 0.0f, _input.y);
    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            _jumpBufferExpiration = Time.time + jumpBufferDuration;
        }
    }

        public void HandleJump()
    {


        bool canJump = IsGrounded() || Time.time <= _coyoteTimeExpiration;
        
        bool jumpBuffered = Time.time <= _jumpBufferExpiration;

        if (jumpBuffered && canJump && !_hasJumped)
        {
            _jumpBufferExpiration = 0f;

            _hasJumped = true;
            _isJumping = true; // On active _isJumping au moment du saut
            _verticalVelocity += jump;

            StartCoroutine(JumpSmoke());
        }
    }



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
        bool[] stateArray = new bool[5];
        stateArray[0] = _isWalking;
        stateArray[1] = _isSprinting;
        stateArray[2] = _isJumping;
        stateArray[3] = _isFalling;
        stateArray[4] = IsGrounded();
        return stateArray;
   }

    private IEnumerator BoostCoroutine() //Coroutine qui attend que le joueur retombe au sol avant d'activer le sprint
    {
        while (!IsGrounded())
        {
            yield return null; // Attendre un frame avant de v�rifier � nouveau
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
                if (IsGrounded() && smokeActive && transform.GetComponent<CharacterController>().velocity == new Vector3(0.0f, 0.0f, 0.0f)) //Desactive la fum�e si le joueur n'avance pas
                {
                    transform.Find("SprintSmoke").gameObject.GetComponentInChildren<VisualEffect>().SendEvent("StopSprint");
                    smokeActive = false;
                    _isSprinting = false;
                } else
                {
                    if (!IsGrounded() && smokeActive) //Desactive la fum�e si le joueur est en l'air
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
        transform.Find("JumpSmoke").gameObject.GetComponentInChildren<VisualEffect>().SendEvent("OnJump");
    }

    // PARTIE BOOST VITESSE -----------------------------------------------------------------
    public void ActiverBoostVitesse(float multiplicateur, float duree)
    {
        StartCoroutine(RoutinePotionVitesse(multiplicateur, duree));
    }

    private IEnumerator RoutinePotionVitesse(float mult, float time)
    {
        _potionSpeedMultiplier = mult; // On applique le multiplicateur
        Debug.Log("⚡ Potion bue : Vitesse augmentée !");

        yield return new WaitForSeconds(time); // On attend la durée de la potion

        _potionSpeedMultiplier = 1f; // On remet la vitesse normale
        Debug.Log("🐌 Fin de l'effet de la potion.");
    }

    // FIN PARTIE BOOST VITESSE --------------------------------------------------------------
}

