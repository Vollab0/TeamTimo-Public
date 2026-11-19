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

    [Header("Mouvement Settings")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float speedBoost = 2f;
    [SerializeField] private float jump = 4.0f;
    [SerializeField] private float jumpBoost = 2f;
    [SerializeField] private float smoothTime = 10f;
    [SerializeField] private float gravityMultiplier = 3.0f;
    [SerializeField] private float coyoteTimeDuration = 0.1f;
    [SerializeField] private float jumpBufferDuration = 0.15f;
    [SerializeField] private bool _isThirdPerson = true;
    
    [Header("Camera References")]
    public CinemachineVirtualCamera firstPersonCamera;
    public CinemachineFreeLook thirdPersonCamera;

    // --- AJOUT POUR LE KNOCKBACK (RECUL) ---
    [Header("Knockback Settings")]
    [SerializeField] private float knockbackDecay = 5f; // Vitesse à laquelle le recul s'arrête (plus c'est haut, plus c'est court)
    private Vector3 _knockbackVelocity; // La force actuelle du recul
    // ----------------------------------------

    [Header("Roulade")]
    [Tooltip("Vitesse au début de la roulade (m/s) ; elle ralentit jusqu'à la fin")]
    [SerializeField] private float rollSpeed = 26f;
    [Tooltip("Vitesse en fin de roulade, en fraction de la vitesse de départ")]
    [Range(0f, 1f)]
    [SerializeField] private float rollEndSpeedRatio = 0.3f;
    [Tooltip("Durée de la roulade (s). L'animation dure 0,6 s : elle est accélérée ou ralentie pour suivre")]
    [SerializeField] private float rollDuration = 0.6f;
    [Tooltip("Durée d'invincibilité au début de la roulade (s) : on passe à travers la charge du taureau ou un coup")]
    [SerializeField] private float rollInvincibility = 0.35f;
    [Tooltip("Temps minimum entre la fin d'une roulade et la suivante (s). La stamina viendra s'y ajouter")]
    [SerializeField] private float rollCooldown = 0.3f;

    [Header("À terre")]
    [Tooltip("Vitesse de reptation quand le joueur est à terre (0 cœur)")]
    [SerializeField] private float crawlSpeed = 3f;

    [Header("Attaque")]
    [Tooltip("Durée (s) pendant laquelle le joueur est immobilisé au début d'une attaque. Doit couvrir l'impact (0,27 s) : dès qu'elle est finie, l'Animator quitte l'attaque pour la marche / la course (fondu de 0,1 s) si le joueur se déplace")]
    [Min(0f)]
    [SerializeField] private float attackDuration = 0.3f;
    [Tooltip("Délai minimal (s) entre deux attaques : laisse l'animation (0,6 s) aller jusqu'au coup avant d'en relancer une")]
    [Min(0f)]
    [SerializeField] private float attackCooldown = 0.45f;

    /// <summary>Déclenché au début d'une roulade (animation).</summary>
    public event Action OnRoll;

    /// <summary>Déclenché au début d'une attaque (PlayerMeleeAttack applique le coup).</summary>
    public event Action OnAttack;

    private bool _isRolling;
    private bool _isDowned;
    private Vector3 _rollDirection;
    private float _rollStartTime;
    private float _nextRollTime;
    private float _invulnerableUntil;

    private float _gravity = -9.81f;
    private float _potionSpeedMultiplier = 1f;
    private float _verticalVelocity;
    private float _velocity;
    private float _jumpHeight;
    private float _coyoteTimeExpiration;
    private float _jumpBufferExpiration;
    private Vector3 _newDirection;
    private bool _hasJumped;
    private bool _canSprint = true;
    private bool _sprintActivated;
    private bool _isSprinting;
    private bool _isFalling = false;
    private bool _isJumping = false;
    private bool _isWalking;
    private bool _isAttacking = false;
    private float _nextAttackTime;

    private bool IsGrounded() => _characterController.isGrounded;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _velocity = speed;
        _jumpHeight = 1.0f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (_isThirdPerson)
        {
            if(thirdPersonCamera != null) thirdPersonCamera.gameObject.SetActive(true);
        }
        else
        {
            if(firstPersonCamera != null) firstPersonCamera.gameObject.SetActive(true);
        }
    }

    private void Update()
    {
        ApplyGravity();
        ApplyRotation();
        ApplyMovement();
        HandleJump();
    }

    // --- AJOUT : LA MÉTHODE APPELÉE PAR PLAYER HEALTH ---
    public void ApplyKnockback(Vector3 sourcePosition, float force)
    {
        // 1. Calcul de la direction (De la source vers le joueur)
        Vector3 direction = (transform.position - sourcePosition).normalized;
        
        // 2. On aplatit l'axe Y pour un recul horizontal, et on ajoute un léger saut
        direction.y = 0; 
        
        // 3. On applique la force (avec un petit bonus vers le haut pour "décoller" le joueur)
        _knockbackVelocity = (direction + Vector3.up * 0.5f).normalized * force;

        // 4. On reset la gravité actuelle pour que le "pop" vers le haut fonctionne bien
        if (_verticalVelocity < 0) _verticalVelocity = 0;
    }
    // ----------------------------------------------------

    private void ApplyGravity()
    {
        if (IsGrounded() && _verticalVelocity < 0.0f)
        {
            _verticalVelocity = -1.0f;
            _coyoteTimeExpiration = Time.time + coyoteTimeDuration;
            _hasJumped = false;
            
            _isFalling = false;
            _isJumping = false;
        }
        else
        {
            _verticalVelocity += _gravity * gravityMultiplier * Time.deltaTime;
            
            if (_verticalVelocity > 0.0f)
            {
                _isJumping = true;
                _isFalling = false;
            }
            else if (_verticalVelocity < 0.0f)
            {
                _isJumping = false;
                
                if(!_isFalling)
                {
                    _isFalling = true;
                    StartCoroutine(LandSmoke());
                }
            }
        }
        _direction.y = _verticalVelocity;
    }

 private void ApplyRotation()
{
    // Si le joueur ne touche pas aux touches de déplacement (_input est vide),
    // on arrête tout de suite.
    // Résultat : Le joueur garde sa dernière orientation (donc il continue de faire face à l'ennemi)
    // même s'il est poussé en arrière par le knockback.
    if (_input.sqrMagnitude == 0 || _isRolling) return;

    Vector3 targetOrientation = Vector3.zero;

    if (_isThirdPerson)
    {
        // 1. On récupère la direction de la caméra
        Vector3 camFwd = transform.position - thirdPersonCamera.gameObject.transform.position;
        camFwd.y = 0f; // On garde le plan horizontal
        Quaternion cameraRot = Quaternion.LookRotation(camFwd);

        // 2. On calcule la direction voulue par le joueur (INPUT SEULEMENT)
        // On n'utilise PAS _newDirection ici car _newDirection contient le Knockback !
        Vector3 inputDir = new Vector3(_input.x, 0, _input.y);
        
        // 3. On oriente l'input selon la caméra
        targetOrientation = cameraRot * inputDir;
    }
    else // Vue Première personne
    {
        // Pour la vue FPS, on suit généralement le regard de la caméra
        if (_cameraForward.sqrMagnitude > 0.001f)
        {
            targetOrientation = _cameraForward;
        }
    }

    // 4. Application de la rotation
    if (targetOrientation.sqrMagnitude > 0.001f)
    {
        Quaternion targetRotation = Quaternion.LookRotation(targetOrientation);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothTime * Time.deltaTime);
    }
}

    private void ApplyMovement()
    {
        // 1. Calcul de la direction de la caméra
        if (_isThirdPerson)
        {
            _cameraForward = _characterController.transform.position - thirdPersonCamera.gameObject.transform.position;
        }
        else
        {
            float cameraAngle = firstPersonCamera.State.RawOrientation.eulerAngles.y;
            float angleInRadians = cameraAngle * Mathf.Deg2Rad;
            _cameraForward = new Vector3(Mathf.Sin(angleInRadians), 0f, Mathf.Cos(angleInRadians));
        }

        _cameraForward.y = 0f;
        Quaternion cameraRotation = Quaternion.LookRotation(_cameraForward);

        // 2. Calcul du mouvement normal (Input + Gravité + Vitesse)
        // À terre : on rampe lentement ; pendant la roulade, les touches ne dirigent plus le joueur
        float moveSpeed = _isDowned ? crawlSpeed : _velocity * _potionSpeedMultiplier;
        if (_isRolling) moveSpeed = 0f;

        _newDirection = new Vector3(
             _direction.x * moveSpeed * Time.deltaTime,
             _direction.y * speed * _jumpHeight * Time.deltaTime,
             _direction.z * moveSpeed * Time.deltaTime
        );

        _newDirection = cameraRotation * _newDirection;

        if (_isRolling)
        {
            float t = (Time.time - _rollStartTime) / rollDuration;
            if (t >= 1f)
            {
                _isRolling = false;
                _nextRollTime = Time.time + rollCooldown;
            }
            else
            {
                _newDirection += _rollDirection * (rollSpeed * Mathf.Lerp(1f, rollEndSpeedRatio, t) * Time.deltaTime);
            }
        }

        // --- AJOUT : INTEGRATION DU KNOCKBACK ---
        
        // On ajoute la vélocité du recul au mouvement final
        _newDirection += _knockbackVelocity * Time.deltaTime;

        // On réduit progressivement la vélocité du recul (Decay)
        _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, knockbackDecay * Time.deltaTime);
        
        // ----------------------------------------

        _characterController.Move(_newDirection);
        _isWalking = _input.sqrMagnitude > 0.01f;
    }

    /// <summary>Roulade (action Roll : Ctrl gauche, bouton Est de la manette).</summary>
    public void Roll(InputAction.CallbackContext context)
    {
        if (!context.started) return;
        if (_isRolling || _isAttacking || _isDowned || !IsGrounded() || Time.time < _nextRollTime) return;

        // Direction : celle des touches (relative à la caméra), sinon droit devant
        Vector3 dir = transform.forward;
        if (_input.sqrMagnitude > 0.01f && _cameraForward.sqrMagnitude > 0.001f)
        {
            dir = Quaternion.LookRotation(_cameraForward) * new Vector3(_input.x, 0f, _input.y);
        }
        dir.y = 0f;
        _rollDirection = dir.normalized;
        transform.rotation = Quaternion.LookRotation(_rollDirection);
        _rollStartTime = Time.time;
        _invulnerableUntil = Time.time + rollInvincibility;
        _isRolling = true;
        OnRoll?.Invoke();
    }

    /// <summary>Passe le joueur « à terre » (0 cœur, appelé par PlayerHealth) ou le relève.</summary>
    public void SetDowned(bool downed)
    {
        _isDowned = downed;
        if (!downed) return;
        _isRolling = false;
        _canSprint = false;
        _sprintActivated = false;
        _velocity = speed;
        _jumpHeight = 1.0f;
        _jumpBufferExpiration = 0f;
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

  public void Attack(InputAction.CallbackContext context)
{

    if (context.started && !_isAttacking && !_isDowned && !_isRolling && Time.time >= _nextAttackTime)
    {
        StartCoroutine(AttackRoutine());
    }
}

private IEnumerator AttackRoutine()
{
    _isAttacking = true;
    _nextAttackTime = Time.time + attackCooldown;
    OnAttack?.Invoke();

    float originalSpeed = _velocity;
    _velocity = 0; 

    yield return new WaitForSeconds(attackDuration);

    _velocity = originalSpeed;

    _isAttacking = false;
}
    public void HandleJump()
    {
        if (_isDowned || _isRolling) return;
        bool canJump = IsGrounded() || Time.time <= _coyoteTimeExpiration;
        bool jumpBuffered = Time.time <= _jumpBufferExpiration;

        if (jumpBuffered && canJump && !_hasJumped)
        {
            _jumpBufferExpiration = 0f;
            _hasJumped = true;
            _isJumping = true;
            _verticalVelocity += jump;
            StartCoroutine(JumpSmoke());
        }
    }

    public void Sprint(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Started)
        {
            if (_isDowned) return;
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

    // États lus par AnimationStateController à chaque frame (sans allouer de tableau, contrairement à GetState)
    public bool IsWalking => _isWalking;
    public bool IsSprinting => _isSprinting;
    public bool IsJumping => _isJumping;
    public bool IsFalling => _isFalling;
    public bool IsGroundedNow => IsGrounded();
    public bool IsAttacking => _isAttacking;
    public bool IsRolling => _isRolling;
    public bool IsDowned => _isDowned;
    /// <summary>Vrai pendant les frames d'invincibilité de la roulade (lu par PlayerHealth).</summary>
    public bool IsInvulnerable => Time.time < _invulnerableUntil;

    public bool[] GetState()
    {
        bool[] stateArray = new bool[6];
        stateArray[0] = _isWalking;
        stateArray[1] = _isSprinting;
        stateArray[2] = _isJumping;
        stateArray[3] = _isFalling;
        stateArray[4] = IsGrounded();
        stateArray[5] = _isAttacking;
        return stateArray;
   }

    private IEnumerator BoostCoroutine()
    {
        while (!IsGrounded())
        {
            yield return null;
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
            if(IsGrounded() && !smokeActive && transform.GetComponent<CharacterController>().velocity != Vector3.zero)
            {
                var vfx = transform.Find("SprintSmoke")?.gameObject.GetComponentInChildren<VisualEffect>();
                if(vfx) vfx.SendEvent("StartSprint");
                smokeActive = true;
                _isSprinting = true;
            }
            else
            {
                if (IsGrounded() && smokeActive && transform.GetComponent<CharacterController>().velocity == Vector3.zero)
                {
                    var vfx = transform.Find("SprintSmoke")?.gameObject.GetComponentInChildren<VisualEffect>();
                    if(vfx) vfx.SendEvent("StopSprint");
                    smokeActive = false;
                    _isSprinting = false;
                } else
                {
                    if (!IsGrounded() && smokeActive)
                    {
                        var vfx = transform.Find("SprintSmoke")?.gameObject.GetComponentInChildren<VisualEffect>();
                        if(vfx) vfx.SendEvent("StopSprint");
                        smokeActive = false;
                        _isSprinting = false;
                    }
                }
            }
            yield return null;
        }
        if (smokeActive)
        {
            var vfx = transform.Find("SprintSmoke")?.gameObject.GetComponentInChildren<VisualEffect>();
            if(vfx) vfx.SendEvent("StopSprint");
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
        var vfx = transform.Find("LandSmoke")?.gameObject.GetComponentInChildren<VisualEffect>();
        if(vfx) vfx.SendEvent("OnLand");
    }

    private IEnumerator JumpSmoke()
    {
        yield return null;
        var vfx = transform.Find("JumpSmoke")?.gameObject.GetComponentInChildren<VisualEffect>();
        if(vfx) vfx.SendEvent("OnJump");
    }

    public void ActiverBoostVitesse(float multiplicateur, float duree)
    {
        StartCoroutine(RoutinePotionVitesse(multiplicateur, duree));
    }

    private IEnumerator RoutinePotionVitesse(float mult, float time)
    {
        _potionSpeedMultiplier = mult;
        Debug.Log("⚡ Potion bue : Vitesse augmentée !");
        yield return new WaitForSeconds(time);
        _potionSpeedMultiplier = 1f;
        Debug.Log("🐌 Fin de l'effet de la potion.");
    }
}