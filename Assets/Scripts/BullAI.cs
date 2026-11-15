using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// IA du Taureau (Taureau.md) : patrouille « sur rail » dans l'axe de son couloir, repère un joueur
/// devant lui dans le couloir, gratte le sol (préparation), puis charge en ligne droite.
/// Pendant la charge, il percute tout joueur sur toute la largeur du couloir (une seule fois par charge) :
/// 1 cœur + gros recul. Seule esquive dans l'axe : être en l'air (saut) au moment où il arrive.
/// La charge ignore le NavMesh (arbres, bosses) : il ne s'arrête que contre un mur (calque obstacleLayer),
/// sur le premier joueur percuté si la case est cochée, ou quand on l'interrompt (bouclier, Stun()).
/// Contre un mur, il rebondit en arrière. Il reste ensuite étourdi, se réveille (secoue la tête) puis repart.
///
/// Réseau (ADR 002) : l'IA ne tourne que chez l'hôte (ou sans réseau). Ce composant ne doit PAS être mis
/// dans les serverOnlyComponents d'EnemyNetworkSetup : il reste actif partout pour animer le taureau.
/// Chez un client, il calcule « Speed » à partir du déplacement du transform (NetworkTransform),
/// applique l'état répliqué (Preparing / Charging / Stunned / Dead) et rejoue le trigger Hit (RPC).
///
/// Contrat de l'Animator Controller : float Speed (m/s), bool Turning, bool Preparing, bool Charging,
/// bool Stunned, bool WallImpact, trigger Hit, bool Dead.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class BullAI : NetworkBehaviour
{
    // Valeurs répliquées (byte) : ajouter les nouveaux états à la fin.
    private enum State : byte { Init, Patrol, Preparing, Charging, Stunned, Dead, Recovering }

    #region --- PARAMETRES INSPECTOR ---
    [Header("1. Vision")]
    [Tooltip("Écart vertical max (m) entre les pieds du taureau et ceux du joueur pour qu'il le voie ; les yeux sont à 80 % de cette hauteur")]
    [Min(0f)]
    [SerializeField] private float visionHeight = 5.0f;

    [Tooltip("Portée de vision (m) devant lui, dans l'axe du couloir ; c'est aussi la longueur max d'une charge")]
    [Min(1f)]
    [SerializeField] private float maxVisionDistance = 300f;

    [Tooltip("Largeur max (m) retenue pour le couloir (vision et zone de frappe). Évite une zone géante si le taureau est posé dans une salle ou un carrefour")]
    [Min(1f)]
    [SerializeField] private float maxCorridorWidth = 60f;

    [Header("Layers")]
    [Tooltip("Calques qui bloquent la vue et arrêtent la charge (Wall)")]
    [SerializeField] private LayerMask obstacleLayer;

    [Tooltip("Calques du sol (terrain) : sert à mesurer la hauteur des pieds d'un joueur pour l'esquive au saut")]
    [SerializeField] private LayerMask groundLayer = 1; // Default

    [Header("2. Mouvement")]
    [Tooltip("Vitesse de patrouille (m/s). Joueur : 15 en marche, 30 en sprint")]
    [Min(0f)]
    [SerializeField] private float patrolSpeed = 8f;

    [Tooltip("Vitesse de charge (m/s), multipliée la nuit par EnemyNightMode.SpeedMultiplier")]
    [Min(0.1f)]
    [SerializeField] private float chargeSpeed = 100f;

    [Tooltip("Accélération (m/s²) au départ de la charge (la vitesse max est atteinte en chargeSpeed / chargeAcceleration s)")]
    [Min(0.1f)]
    [SerializeField] private float chargeAcceleration = 300f;

    [Tooltip("Durée (s) pendant laquelle il gratte le sol en soufflant avant de charger : le temps qu'ont les joueurs pour réagir")]
    [Min(0f)]
    [SerializeField] private float chargeWindup = 1.0f;

    [Tooltip("Distance libre (m) minimale devant lui pour lancer une charge. En dessous (mur ou arbre juste devant), il ignore le joueur au lieu de charger sur place")]
    [Min(0f)]
    [SerializeField] private float minChargeDistance = 15f;

    [Tooltip("Durée (s) d'étourdissement après un arrêt (mur, joueur, bouclier)")]
    [Min(0f)]
    [SerializeField] private float stunDuration = 5.0f;

    [Tooltip("Durée (s) du réveil après l'étourdissement (il secoue la tête, immobile) avant de reprendre sa patrouille. Laisse un court répit aux joueurs. Doit correspondre à l'animation WakeUp")]
    [Min(0f)]
    [SerializeField] private float wakeUpDuration = 1.0f;

    [Tooltip("Probabilité (%) de faire demi-tour, testée chaque seconde de patrouille")]
    [Range(0, 100)][SerializeField] private float randomTurnChance = 0f;

    [Header("3. Charge : dégâts")]
    [Tooltip("Dégâts infligés au joueur percuté, en demi-cœurs (2 = 1 cœur). Multipliés la nuit, arrondis au-dessus")]
    [Min(0)]
    [SerializeField] private int chargeDamage = 2;

    [Tooltip("Force du recul appliqué au joueur percuté (le recul générique des autres coups vaut 15)")]
    [Min(0f)]
    [SerializeField] private float knockbackForce = 50f;

    [Header("3b. Charge : sillage (zone de frappe)")]
    [Tooltip("Longueur (m) du sillage : zone de frappe en triangle (trapèze) qui part de l'avant du taureau et s'élargit vers l'arrière. Ne remonte jamais avant le point de départ de la charge")]
    [Min(0.1f)]
    [SerializeField] private float wakeLength = 30f;

    [Tooltip("Largeur (m) du sillage à l'avant du taureau (sa pointe) : environ la largeur de son corps (5 m pour le placeholder)")]
    [Min(0.1f)]
    [SerializeField] private float wakeFrontWidth = 5f;

    [Tooltip("Largeur (m) du sillage à l'arrière, au bout de wakeLength. Limitée à la largeur du couloir : une grande valeur = tout le couloir")]
    [Min(0.1f)]
    [SerializeField] private float wakeBackWidth = 60f;

    [Tooltip("Au choc contre un mur, frappe la tranche entre l'avant de son corps et le mur (sur au plus cette distance, en m), sur toute la largeur du couloir : un joueur plaqué au mur n'est pas épargné. 0 = désactivé")]
    [Min(0f)]
    [SerializeField] private float impactReach = 6f;

    [Tooltip("Esquive par le saut : un joueur dont les pieds sont plus haut que ça (m) au-dessus du sol quand le taureau l'atteint n'est pas touché")]
    [Min(0f)]
    [SerializeField] private float jumpClearHeight = 0.75f;

    [Tooltip("Décoché (par défaut) : il fonce jusqu'au mur et percute tous les joueurs sur la ligne. Coché : la charge s'arrête sur le premier joueur percuté (plus facile). Chaque joueur n'est touché qu'une fois par charge dans les deux cas")]
    [Label("S'arrête au premier joueur percuté")]
    [SerializeField] private bool stopOnFirstPlayerHit = false;

    [Header("3c. Charge : arrêt contre le mur")]
    [Tooltip("Distance (m) entre le pivot du taureau et la pointe de ses cornes en charge : il s'arrête quand elles touchent le mur (sinon la tête traverse le mur)")]
    [Min(0f)]
    [SerializeField] private float hornReach = 5.3f;

    [Tooltip("Distance (m) dont il rebondit en arrière après avoir percuté un mur, pour que sa tête ne traverse pas le mur pendant l'étourdissement")]
    [Min(0f)]
    [SerializeField] private float wallBounceDistance = 3f;

    [Tooltip("Durée (s) du rebond en arrière contre le mur (calée sur l'animation Impact)")]
    [Min(0.01f)]
    [SerializeField] private float wallBounceDuration = 0.3f;

    [Header("4. Animation")]
    [Tooltip("Animator du modèle (enfant « Model ») ; facultatif. Si vide, cherché dans les enfants")]
    [SerializeField] private Animator animator;

    [Tooltip("Lissage (s) du paramètre Speed")]
    [Min(0f)]
    [SerializeField] private float speedDampTime = 0.1f;
    #endregion

    // --- Contrat de l'Animator ---
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int PreparingHash = Animator.StringToHash("Preparing");
    private static readonly int ChargingHash = Animator.StringToHash("Charging");
    private static readonly int StunnedHash = Animator.StringToHash("Stunned");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DeadHash = Animator.StringToHash("Dead");
    private static readonly int TurningHash = Animator.StringToHash("Turning");
    private static readonly int WallImpactHash = Animator.StringToHash("WallImpact");

    // Distance (m) à laquelle le bout du rail est considéré comme atteint.
    private const float ArrivalTolerance = 0.1f;
    // Vitesse (×/s) à laquelle il se recentre sur l'axe du couloir en patrouille.
    private const float RailRecenterRate = 5f;
    // Hauteur (m) des rayons qui mesurent le couloir et cherchent le mur au bout de la charge.
    private const float WallProbeHeight = 2.0f;
    // En patrouille : si l'agent avance de moins de cette fraction du pas demandé, il est bloqué (bord du NavMesh) : demi-tour.
    private const float BlockedMoveRatio = 0.3f;
    // Au choix du rail : l'autre axe n'est un couloir (et pas la largeur du couloir) que s'il mesure au moins maxCorridorWidth × ce ratio.
    private const float MinCrossCorridorRatio = 1.5f;
    // Vitesse (°/s) à laquelle il se tourne vers l'axe de charge pendant qu'il gratte le sol.
    private const float PrepareTurnSpeed = 720f;
    // Rayon (m) de recherche du NavMesh sous lui pendant la charge (pour suivre le relief, sans en être prisonnier).
    private const float ChargeGroundSearch = 6f;
    // Rayon (m) de recherche du NavMesh pour recoller l'agent après une téléportation (recentrage, fin de charge).
    private const float NavMeshRecoverRadius = 15f;
    // Animation de demi-tour : vitesse de rotation (°/s) au-dessus de laquelle il « tourne sur place », et maintien (s).
    private const float TurningYawRate = 40f;
    private const float TurningHold = 0.15f;
    // Recul : on place la « source » du coup loin derrière le joueur, dans l'axe de charge,
    // pour que la direction du recul reste celle de la charge malgré le décalage réseau des positions.
    private const float KnockbackSourceDistance = 50f;
    // Au choc contre un mur : marge (m) ajoutée derrière la surface du mur, pour le corps d'un joueur plaqué contre lui.
    private const float ImpactWallSlack = 1.5f;

    // État répliqué chez les clients (écrit par l'hôte seulement).
    private readonly NetworkVariable<byte> _netState = new NetworkVariable<byte>((byte)State.Init);
    // Demi-largeur du couloir et départ de la charge : servent aux clients à reconstruire le sillage (VFX).
    private readonly NetworkVariable<float> _netCorridorHalfWidth = new NetworkVariable<float>(0f);
    private readonly NetworkVariable<Vector3> _netChargeStart = new NetworkVariable<Vector3>(Vector3.zero);
    // Vrai si l'étourdissement en cours vient d'un choc contre un mur (animation Impact) ; faux : freinage (Brake).
    private readonly NetworkVariable<bool> _netWallImpact = new NetworkVariable<bool>(false);

    private NavMeshAgent _agent;
    private Collider _bodyCollider;
    private EnemyNightMode _nightMode;

    // Vrai là où l'IA tourne : chez l'hôte, ou partout sans réseau.
    private bool _runsAI;
    private State _state = State.Init;
    private float _stateTimer;
    private float _currentStunDuration;
    private bool _wallImpact;
    private float _bounceTimer; // < wallBounceDuration : rebond en arrière en cours

    // --- Rail ---
    private bool _isAxisX;              // true = couloir Est/Ouest (Z fixe), false = Nord/Sud (X fixe)
    private float _fixedAxisCoordinate; // coordonnée latérale du centre du couloir
    private float _corridorHalfWidth;   // demi-largeur du couloir (vision et zone de frappe)
    private Vector3 _patrolPointA, _patrolPointB; // A = bout côté +axe, B = bout côté -axe
    private float _patrolSign = 1f;               // +1 : vers A, -1 : vers B
    private float _nextRandomTurnTime;

    // --- Charge ---
    private Vector3 _chargeDir;
    private Vector3 _chargeRight;
    private Vector3 _chargeStart;
    private float _chargeRemaining;
    private float _currentChargeSpeed;
    // Joueurs déjà percutés ou ayant esquivé pendant la charge en cours (une seule résolution par charge).
    private readonly List<Transform> _resolvedThisCharge = new List<Transform>(8);

    // --- Animation ---
    private bool _hasSpeedParam, _hasPreparingParam, _hasChargingParam, _hasStunnedParam, _hasHitParam, _hasDeadParam;
    private bool _hasTurningParam, _hasWallImpactParam;
    private Vector3 _lastAnimPosition;
    private Quaternion _lastAnimRotation;
    private float _turningUntil;

    /// <summary>Vrai quand le taureau est mort.</summary>
    public bool IsDead => _state == State.Dead || NetState == State.Dead;

    /// <summary>Vrai pendant la charge (utile pour un bouclier : seul un taureau qui charge se pare).</summary>
    public bool IsCharging => (_runsAI ? _state : NetState) == State.Charging;

    private State NetState => (State)_netState.Value;

    /// <summary>Largeur (m) du couloir mesurée au verrouillage sur le rail (valable aussi chez les clients).</summary>
    public float CorridorWidth => 2f * (_runsAI ? _corridorHalfWidth : _netCorridorHalfWidth.Value);

    /// <summary>Longueur max (m) du sillage (champ wakeLength).</summary>
    public float WakeLength => wakeLength;

    /// <summary>Largeur (m) du sillage à l'avant du taureau, limitée à la largeur du couloir.</summary>
    public float WakeFrontWidth => Mathf.Min(wakeFrontWidth, CorridorWidth);

    /// <summary>Largeur (m) du sillage à wakeLength derrière l'avant, limitée à la largeur du couloir.</summary>
    public float WakeBackWidth => Mathf.Min(Mathf.Max(wakeBackWidth, wakeFrontWidth), CorridorWidth);

    /// <summary>Distance (m) entre le pivot et l'avant du taureau, où se trouve la pointe du sillage.</summary>
    public float FrontOffset => BodyRadius;

    /// <summary>Point de départ de la charge en cours ou de la dernière (valable aussi chez les clients).</summary>
    public Vector3 ChargeStartPosition => _runsAI ? _chargeStart : _netChargeStart.Value;

    /// <summary>Longueur (m) actuelle du sillage : il grandit depuis le départ de la charge jusqu'à WakeLength.</summary>
    public float CurrentWakeLength
    {
        get
        {
            Vector3 travelled = transform.position - ChargeStartPosition;
            travelled.y = 0f;
            return Mathf.Min(wakeLength, travelled.magnitude + 2f * BodyRadius);
        }
    }

    private float SpeedMultiplier => _nightMode != null ? _nightMode.SpeedMultiplier : 1f;
    private float AggroMultiplier => _nightMode != null ? _nightMode.AggroMultiplier : 1f;
    private float CurrentPatrolSpeed => patrolSpeed * SpeedMultiplier;
    private float CurrentChargeSpeed => chargeSpeed * SpeedMultiplier;
    private float BodyRadius => _agent.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);

    #region --- Cycle de vie ---
    private void Reset()
    {
        obstacleLayer = LayerMask.GetMask("Wall");
        groundLayer = LayerMask.GetMask("Default");
    }

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _bodyCollider = GetComponent<Collider>();
        _nightMode = GetComponent<EnemyNightMode>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        CacheAnimatorParameters();

        // Sans réseau, l'IA tourne ici ; en réseau, OnNetworkSpawn décide (hôte seulement).
        NetworkManager manager = NetworkManager.Singleton;
        _runsAI = manager == null || !manager.IsListening;

        _lastAnimPosition = transform.position;
        _lastAnimRotation = transform.rotation;
    }

    public override void OnNetworkSpawn()
    {
        _runsAI = IsServer;
        _netState.OnValueChanged += HandleNetStateChanged;
        _netWallImpact.OnValueChanged += HandleNetWallImpactChanged;

        // Client qui rejoint en cours de partie : on applique l'état actuel.
        if (!IsServer) ApplyStateVisuals(NetState);
    }

    public override void OnNetworkDespawn()
    {
        _runsAI = false;
        _netState.OnValueChanged -= HandleNetStateChanged;
        _netWallImpact.OnValueChanged -= HandleNetWallImpactChanged;
    }

    private void HandleNetStateChanged(byte previous, byte current)
    {
        // L'hôte a déjà appliqué l'état en y entrant.
        if (!IsServer) ApplyStateVisuals((State)current);
    }

    private void HandleNetWallImpactChanged(bool previous, bool current)
    {
        if (!IsServer) SetAnimatorBool(WallImpactHash, _hasWallImpactParam, current);
    }

    private void Update()
    {
        UpdateAnimatorSpeed();

        if (!_runsAI || _state == State.Dead) return;

        switch (_state)
        {
            case State.Init: TryInitializeAI(); break;
            case State.Patrol: UpdatePatrol(); break;
            case State.Preparing: UpdatePreparing(); break;
            case State.Charging: UpdateCharge(); break;
            case State.Stunned: UpdateStunned(); break;
            case State.Recovering: UpdateRecovering(); break;
        }
    }

    // L'agent est activé par EnemyNetworkSetup et placé par SpawnerManager (Warp) : on attend qu'il soit sur le NavMesh.
    private void TryInitializeAI()
    {
        if (!_agent.enabled || !_agent.isOnNavMesh) return;

        LockToCorridorAxis();
        EnterState(State.Patrol);
    }
    #endregion

    #region --- États ---
    private void EnterState(State newState)
    {
        // La charge déplace le transform sans l'agent : on le recolle au NavMesh en sortant (mur, bouclier, mort…).
        if (_state == State.Charging && newState != State.Charging) EndChargeMovement();

        _state = newState;
        _stateTimer = 0f;

        switch (newState)
        {
            case State.Patrol:
                // Patrouille « sur rail » avec agent.Move : pas de chemin NavMesh
                // (le pathfinding contournait les encoches du NavMesh et se battait avec le recentrage sur l'axe).
                StopAgent();
                _agent.updateRotation = false;
                _nextRandomTurnTime = Time.time + 1f;
                break;

            case State.Preparing:
                StopAgent();
                // Direction de charge choisie dès maintenant : il s'y tourne pendant qu'il gratte le sol
                // (il pouvait repérer un joueur en plein demi-tour et gratter le sol de profil).
                _chargeDir = FacingAxisDirection();
                break;

            case State.Stunned:
            case State.Recovering:
                StopAgent();
                break;

            case State.Charging:
                BeginCharge();
                break;
        }

        ApplyStateVisuals(newState);
        if (IsSpawned && IsServer) _netState.Value = (byte)newState;
    }

    // --- Patrouille : allers-retours sur le rail ---
    private void UpdatePatrol()
    {
        // Agent sorti du NavMesh (ne devrait pas arriver) : on le recolle au plus près au lieu d'appeler Move en boucle.
        if (!_agent.isOnNavMesh)
        {
            WarpToNavMesh(transform.position);
            return;
        }

        // Il ne charge que s'il a la place de s'élancer : sinon il s'étourdirait sur place, en boucle.
        if (TryFindPlayerInCorridor() && WallDistanceAhead(RailPosition(), FacingAxisDirection()) >= minChargeDistance)
        {
            EnterState(State.Preparing);
            return;
        }

        float dt = Time.deltaTime;
        Vector3 dir = AxisDirection * _patrolSign;

        // Demi-tour en cours : il pivote sur place (vitesse angulaire de l'agent) avant de repartir.
        Quaternion look = Quaternion.LookRotation(dir);
        if (Quaternion.Angle(transform.rotation, look) > 1f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, _agent.angularSpeed * dt);
            return;
        }
        transform.rotation = look;

        if (randomTurnChance > 0f && Time.time >= _nextRandomTurnTime)
        {
            _nextRandomTurnTime = Time.time + 1f;
            if (Random.Range(0f, 100f) < randomTurnChance)
            {
                TurnAround();
                return;
            }
        }

        Vector3 end = _patrolSign > 0f ? _patrolPointA : _patrolPointB;
        float remaining = Vector3.Dot(end - transform.position, dir);
        if (remaining <= ArrivalTolerance)
        {
            TurnAround();
            return;
        }

        float step = Mathf.Min(CurrentPatrolSpeed * dt, remaining);
        Vector3 before = transform.position;
        _agent.Move(dir * step + RailCorrection(dt));
        float moved = Vector3.Dot(transform.position - before, dir);

        // Bloqué par un bord du NavMesh (arbre, encoche…) : le rail s'arrête là, il fait demi-tour.
        if (step > 0.001f && moved < step * BlockedMoveRatio) TurnAround();
    }

    private void TurnAround()
    {
        _patrolSign = -_patrolSign;
    }

    // Petit déplacement latéral qui ramène le taureau sur l'axe du couloir.
    private Vector3 RailCorrection(float dt)
    {
        Vector3 position = transform.position;
        float offset = _isAxisX ? _fixedAxisCoordinate - position.z : _fixedAxisCoordinate - position.x;
        float correction = offset * Mathf.Min(1f, dt * RailRecenterRate);
        return _isAxisX ? new Vector3(0f, 0f, correction) : new Vector3(correction, 0f, 0f);
    }

    // --- Préparation : il se tourne face à l'axe de charge et gratte le sol ---
    private void UpdatePreparing()
    {
        float dt = Time.deltaTime;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(_chargeDir), PrepareTurnSpeed * dt);

        _stateTimer += dt;
        if (_stateTimer >= chargeWindup) EnterState(State.Charging);
    }

    // --- Étourdi : rebondit en arrière s'il a percuté un mur, reste immobile, puis se réveille ---
    private void UpdateStunned()
    {
        float dt = Time.deltaTime;
        UpdateWallBounce(dt);

        _stateTimer += dt;
        if (_stateTimer < _currentStunDuration) return;

        if (wakeUpDuration > 0f) EnterState(State.Recovering);
        else ResumePatrol();
    }

    // --- Réveil : il secoue la tête sur place (répit pour les joueurs), puis repart ---
    private void UpdateRecovering()
    {
        _stateTimer += Time.deltaTime;
        if (_stateTimer >= wakeUpDuration) ResumePatrol();
    }

    // Reprend la patrouille vers le bout de rail le plus éloigné.
    private void ResumePatrol()
    {
        float distA = (transform.position - _patrolPointA).sqrMagnitude;
        float distB = (transform.position - _patrolPointB).sqrMagnitude;
        _patrolSign = distA > distB ? 1f : -1f;
        EnterState(State.Patrol);
    }

    private void EnterStunned(float duration, bool wallImpact)
    {
        _currentStunDuration = duration;
        _wallImpact = wallImpact;
        _bounceTimer = wallImpact ? 0f : wallBounceDuration;
        // Avant l'état : l'Animator doit savoir quelle animation d'arrêt jouer (Impact contre un mur, Brake sinon).
        SetAnimatorBool(WallImpactHash, _hasWallImpactParam, wallImpact);
        if (IsSpawned && IsServer) _netWallImpact.Value = wallImpact;
        EnterState(State.Stunned);
    }

    // Rebond en arrière après le choc contre un mur : rapide puis amorti (ease-out), le long de l'axe de charge.
    private void UpdateWallBounce(float dt)
    {
        if (_bounceTimer >= wallBounceDuration || wallBounceDistance <= 0f) return;

        float before = EaseOut(_bounceTimer / wallBounceDuration);
        _bounceTimer = Mathf.Min(wallBounceDuration, _bounceTimer + dt);
        float after = EaseOut(_bounceTimer / wallBounceDuration);
        if (_agent.enabled && _agent.isOnNavMesh) _agent.Move(-_chargeDir * (wallBounceDistance * (after - before)));
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }
    #endregion

    #region --- Rail ---
    // Rails déjà pris par les taureaux de la partie (chez l'hôte) : deux taureaux évitent de patrouiller le même couloir.
    private static readonly List<BullAI> s_LockedBulls = new List<BullAI>(64);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_LockedBulls.Clear();

    private void OnDisable() => s_LockedBulls.Remove(this);

    /// <summary>
    /// Choix du rail à l'apparition : il mesure la distance aux murs dans les 4 directions et prend l'axe le plus
    /// long (la longueur du couloir, pas sa largeur), tourné vers son bout le plus éloigné. Si un autre taureau patrouille déjà ce
    /// couloir sur cet axe, il prend l'autre axe, mais seulement s'il s'agit d'un vrai couloir (carrefour) :
    /// sinon il partage le couloir plutôt que de patrouiller sa largeur.
    /// </summary>
    private void LockToCorridorAxis()
    {
        Vector3 origin = transform.position + Vector3.up * WallProbeHeight;
        float dN = GetRayDist(origin, Vector3.forward);
        float dS = GetRayDist(origin, Vector3.back);
        float dE = GetRayDist(origin, Vector3.right);
        float dW = GetRayDist(origin, Vector3.left);
        float y = transform.position.y;
        float margin = BodyRadius + 2.0f;

        // Les deux rails possibles, mesurés depuis le centre de leur couloir ; le plus long d'abord, l'autre s'il est déjà pris.
        MeasureRail(true, origin, dN, dS, out float zCenter, out float zHalfWidth, out float zMin, out float zMax);
        MeasureRail(false, origin, dE, dW, out float xCenter, out float xHalfWidth, out float xMin, out float xMax);
        bool preferZ = zMax - zMin >= xMax - xMin;
        float center = preferZ ? zCenter : xCenter, halfWidth = preferZ ? zHalfWidth : xHalfWidth;
        float min = preferZ ? zMin : xMin, max = preferZ ? zMax : xMax;
        float altCenter = preferZ ? xCenter : zCenter, altHalfWidth = preferZ ? xHalfWidth : zHalfWidth;
        float altMin = preferZ ? xMin : zMin, altMax = preferZ ? xMax : zMax;
        bool useZ = preferZ;
        // Repli sur l'autre axe seulement si c'est lui aussi un vrai couloir (carrefour) et non la largeur du couloir.
        if (altMax - altMin >= maxCorridorWidth * MinCrossCorridorRatio
            && IsRailTaken(preferZ, center, halfWidth, min, max) && !IsRailTaken(!preferZ, altCenter, altHalfWidth, altMin, altMax))
        {
            useZ = !preferZ;
            center = altCenter; halfWidth = altHalfWidth; min = altMin; max = altMax;
        }

        _isAxisX = !useZ;
        _fixedAxisCoordinate = center;
        _corridorHalfWidth = Mathf.Clamp(halfWidth, BodyRadius, maxCorridorWidth * 0.5f);
        float along = useZ ? transform.position.z : transform.position.x;
        along = Mathf.Clamp(along, min + margin, Mathf.Max(min + margin, max - margin));
        // Part vers le bout le plus éloigné de son axe.
        _patrolSign = max - along >= along - min ? 1f : -1f;

        Vector3 finalPos = useZ ? new Vector3(center, y, along) : new Vector3(along, y, center);
        _patrolPointA = useZ ? new Vector3(center, y, max - margin) : new Vector3(max - margin, y, center);
        _patrolPointB = useZ ? new Vector3(center, y, min + margin) : new Vector3(min + margin, y, center);
        transform.rotation = Quaternion.LookRotation(AxisDirection * _patrolSign);

        if (IsSpawned && IsServer) _netCorridorHalfWidth.Value = _corridorHalfWidth;
        // Le centre du couloir peut tomber dans un trou du NavMesh (arbre) : point du NavMesh le plus proche,
        // le recentrage progressif (RailCorrection) fait le reste.
        if (!WarpToNavMesh(finalPos)) WarpToNavMesh(transform.position);

        // Le rail s'arrête aussi au premier trou du NavMesh sur l'axe (arbre, bosse du terrain…).
        _patrolPointA = ClampToNavMeshAlongAxis(_patrolPointA);
        _patrolPointB = ClampToNavMeshAlongAxis(_patrolPointB);

        if (!s_LockedBulls.Contains(this)) s_LockedBulls.Add(this);
    }

    // Décalages (m) le long de l'axe où l'on mesure la largeur du couloir.
    private static readonly float[] RailWidthSamples = { 0f, 8f, -8f, 16f, -16f, 32f, -32f, 48f, -48f };

    /// <summary>
    /// Mesure le couloir le long d'un axe : sa largeur est prise là où elle est la plus étroite, à plusieurs points
    /// le long de l'axe (à un carrefour, les rayons latéraux partent dans l'autre couloir et donneraient sa longueur).
    /// Renvoie la coordonnée latérale du centre, la demi-largeur, et les murs des deux bouts (coordonnées le long de l'axe).
    /// </summary>
    private void MeasureRail(bool zAxis, Vector3 origin, float forwardDist, float backDist,
                             out float center, out float halfWidth, out float min, out float max)
    {
        Vector3 axis = zAxis ? Vector3.forward : Vector3.right;
        Vector3 lateral = zAxis ? Vector3.right : Vector3.forward;
        float bestWidth = float.MaxValue;
        center = zAxis ? origin.x : origin.z;

        for (int i = 0; i < RailWidthSamples.Length; i++)
        {
            float t = RailWidthSamples[i];
            if (t > forwardDist - 3f || t < -backDist + 3f) continue;
            Vector3 p = origin + axis * t;
            float left = GetRayDist(p, -lateral);
            float right = GetRayDist(p, lateral);
            if (left + right >= bestWidth) continue;
            bestWidth = left + right;
            center = (zAxis ? p.x : p.z) + (right - left) * 0.5f;
        }
        halfWidth = bestWidth < float.MaxValue ? bestWidth * 0.5f : maxCorridorWidth * 0.5f;

        // Bouts du rail, mesurés depuis le centre du couloir.
        Vector3 c = zAxis ? new Vector3(center, origin.y, origin.z) : new Vector3(origin.x, origin.y, center);
        float along = zAxis ? c.z : c.x;
        max = along + GetRayDist(c, axis);
        min = along - GetRayDist(c, -axis);
    }

    // Un autre taureau patrouille-t-il déjà ce couloir sur cet axe (même couloir, rails qui se chevauchent) ?
    private bool IsRailTaken(bool zAxis, float center, float halfWidth, float min, float max)
    {
        for (int i = 0; i < s_LockedBulls.Count; i++)
        {
            BullAI other = s_LockedBulls[i];
            if (other == null || other == this || other.IsDead || other._isAxisX == zAxis) continue;
            if (Mathf.Abs(other._fixedAxisCoordinate - center) > Mathf.Max(halfWidth, other._corridorHalfWidth)) continue;
            float otherMin = zAxis ? Mathf.Min(other._patrolPointA.z, other._patrolPointB.z) : Mathf.Min(other._patrolPointA.x, other._patrolPointB.x);
            float otherMax = zAxis ? Mathf.Max(other._patrolPointA.z, other._patrolPointB.z) : Mathf.Max(other._patrolPointA.x, other._patrolPointB.x);
            if (otherMax >= min && otherMin <= max) return true;
        }
        return false;
    }

    private Vector3 ClampToNavMeshAlongAxis(Vector3 endPoint)
    {
        Vector3 start = transform.position;
        endPoint.y = start.y;
        if (!NavMesh.Raycast(start, endPoint, out NavMeshHit hit, _agent.areaMask)) return endPoint;

        Vector3 dir = endPoint - start;
        dir.y = 0f;
        float distance = Mathf.Max(0f, hit.distance - 1f);
        return start + dir.normalized * distance;
    }

    private float GetRayDist(Vector3 origin, Vector3 dir)
    {
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxVisionDistance, obstacleLayer, QueryTriggerInteraction.Ignore))
            return hit.distance;
        return maxVisionDistance;
    }

    private Vector3 AxisDirection => _isAxisX ? Vector3.right : Vector3.forward;

    // Sens du rail vers lequel il regarde (+axe ou -axe), même s'il est en train de faire demi-tour.
    private Vector3 FacingAxisDirection()
    {
        Vector3 axis = AxisDirection;
        return Vector3.Dot(transform.forward, axis) >= 0f ? axis : -axis;
    }
    #endregion

    #region --- Vision ---
    // Un joueur devant lui, dans la largeur du couloir (×aggro la nuit), à portée et sans mur entre eux.
    private bool TryFindPlayerInCorridor()
    {
        Vector3 forward = FacingAxisDirection();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 origin = transform.position;
        float halfWidth = _corridorHalfWidth * AggroMultiplier;
        Vector3 eyes = origin + Vector3.up * (visionHeight * 0.8f);

        var players = PlayerRegistry.Players;
        for (int i = 0; i < players.Count; i++)
        {
            Transform player = players[i];
            if (player == null) continue;

            Vector3 delta = player.position - origin;
            float along = Vector3.Dot(delta, forward);
            if (along <= 0f || along > maxVisionDistance) continue;
            if (Mathf.Abs(Vector3.Dot(delta, right)) > halfWidth) continue;
            if (Mathf.Abs(delta.y) > visionHeight) continue;

            Vector3 target = player.position + Vector3.up * (visionHeight * 0.5f);
            if (Physics.Linecast(eyes, target, obstacleLayer, QueryTriggerInteraction.Ignore)) continue;

            return true;
        }
        return false;
    }
    #endregion

    #region --- Charge ---
    private void BeginCharge()
    {
        StopAgent();
        _agent.updateRotation = false;

        // _chargeDir a été choisi en entrant en préparation.
        _chargeRight = Vector3.Cross(Vector3.up, _chargeDir);
        transform.rotation = Quaternion.LookRotation(_chargeDir);

        // Recentre sur le rail avant de partir.
        Vector3 start = RailPosition();
        _agent.nextPosition = start;
        _chargeStart = start;
        if (IsSpawned && IsServer) _netChargeStart.Value = start;

        // Pendant la charge, c'est le script qui déplace le transform, sans l'agent : le NavMesh (troué par les
        // arbres et les bosses du terrain) ne l'arrête plus, seuls les murs le font. Recollé au NavMesh à la fin.
        _agent.updatePosition = false;
        transform.position = start;

        _chargeRemaining = WallDistanceAhead(start, _chargeDir);
        _currentChargeSpeed = 0f;
        _resolvedThisCharge.Clear();
    }

    // Fin de charge : l'agent reprend la main là où le taureau s'est arrêté.
    private void EndChargeMovement()
    {
        if (!_agent.enabled) return;
        if (!WarpToNavMesh(transform.position)) _agent.Warp(transform.position);
        _agent.updatePosition = true;
    }

    // Téléporte l'agent sur le point du NavMesh le plus proche de position. False si aucun NavMesh à portée.
    private bool WarpToNavMesh(Vector3 position)
    {
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, NavMeshRecoverRadius, _agent.areaMask)) return false;
        return _agent.Warp(hit.position);
    }

    // Position actuelle ramenée sur l'axe du couloir.
    private Vector3 RailPosition()
    {
        Vector3 position = transform.position;
        if (_isAxisX) position.z = _fixedAxisCoordinate;
        else position.x = _fixedAxisCoordinate;
        return position;
    }

    // Distance (m) que son pivot peut parcourir en ligne droite avant que ses cornes (hornReach) touchent un mur.
    // Seuls les murs (obstacleLayer) comptent : arbres et bords du NavMesh ne l'arrêtent pas.
    private float WallDistanceAhead(Vector3 start, Vector3 dir)
    {
        float distance = maxVisionDistance;
        if (Physics.Raycast(start + Vector3.up * WallProbeHeight, dir, out RaycastHit wallHit, maxVisionDistance, obstacleLayer, QueryTriggerInteraction.Ignore))
        {
            distance = wallHit.distance - Mathf.Max(hornReach, BodyRadius);
        }
        return Mathf.Max(0f, distance);
    }

    private void UpdateCharge()
    {
        float dt = Time.deltaTime;
        _currentChargeSpeed = Mathf.MoveTowards(_currentChargeSpeed, CurrentChargeSpeed, chargeAcceleration * dt);

        float step = Mathf.Min(_currentChargeSpeed * dt, _chargeRemaining);
        if (step > 0f)
        {
            Vector3 next = transform.position + _chargeDir * step;
            // Suit le relief : hauteur du NavMesh le plus proche (même à côté d'un trou laissé par un arbre).
            if (NavMesh.SamplePosition(next, out NavMeshHit ground, ChargeGroundSearch, _agent.areaMask)) next.y = ground.position.y;
            transform.position = next;
            _chargeRemaining -= step;
        }

        // Percussion des joueurs dans le sillage, balayé sur la portion parcourue cette frame (pas de traversée à grande vitesse).
        if (ResolvePlayerHits(step, -1f) && stopOnFirstPlayerHit)
        {
            EnterStunned(stunDuration, false);
            return;
        }

        if (_chargeRemaining <= 0.01f)
        {
            // Choc contre un mur proche : frappe aussi toute la tranche entre le taureau et le mur, sur toute la largeur du couloir.
            float wallGap = WallGapAhead();
            if (wallGap >= 0f && impactReach > 0f) ResolvePlayerHits(0f, Mathf.Min(wallGap, impactReach));
            // Fin de course sans mur (portée max atteinte, en terrain ouvert) : il freine au lieu de s'écraser.
            EnterStunned(stunDuration, wallGap >= 0f);
        }
    }

    // Arrêt contre un mur (cornes au contact) : distance entre l'avant du corps du taureau et le mur ; -1 sinon
    // (portée max atteinte en terrain ouvert).
    private float WallGapAhead()
    {
        Vector3 origin = transform.position + Vector3.up * WallProbeHeight;
        float maxDistance = Mathf.Max(hornReach, BodyRadius) + 0.5f;
        if (!Physics.Raycast(origin, _chargeDir, out RaycastHit hit, maxDistance, obstacleLayer, QueryTriggerInteraction.Ignore))
            return -1f;
        return Mathf.Max(0f, hit.distance - BodyRadius);
    }

    /// <summary>
    /// <paramref name="wallGap"/> négatif : résout les joueurs dans le sillage (trapèze derrière l'avant du taureau).
    /// Positif ou nul (choc contre un mur) : résout toute la tranche entre le centre du taureau et le mur
    /// (avant + wallGap), sur toute la largeur du couloir.
    /// Renvoie true si au moins un joueur a été percuté.
    /// </summary>
    private bool ResolvePlayerHits(float movedThisFrame, float wallGap)
    {
        bool hitSomeone = false;
        Vector3 origin = transform.position;
        float front = BodyRadius;
        float frontHalf = WakeFrontWidth * 0.5f;
        float backHalf = WakeBackWidth * 0.5f;
        // Le sillage ne remonte pas avant le point de départ de la charge (un joueur derrière le taureau au départ n'est pas touché).
        float travelled = Vector3.Dot(origin - _chargeStart, _chargeDir);
        float backLimit = Mathf.Max(front - wakeLength - Mathf.Max(0f, movedThisFrame), -travelled - BodyRadius);

        var players = PlayerRegistry.Players;
        for (int i = 0; i < players.Count; i++)
        {
            Transform player = players[i];
            if (player == null || _resolvedThisCharge.Contains(player)) continue;

            Vector3 delta = player.position - origin;
            float along = Vector3.Dot(delta, _chargeDir);
            float lateral = Mathf.Abs(Vector3.Dot(delta, _chargeRight));
            if (wallGap >= 0f)
            {
                // Choc : du centre du taureau jusqu'au mur, sur toute la largeur du couloir.
                if (along < 0f || along > front + wallGap + ImpactWallSlack || lateral > _corridorHalfWidth) continue;
            }
            else
            {
                if (along > front || along < backLimit) continue;
                // Largeur du sillage à cette distance de la pointe.
                float t = Mathf.Clamp01((front - along) / wakeLength);
                if (lateral > Mathf.Lerp(frontHalf, backHalf, t)) continue;
            }
            if (Mathf.Abs(delta.y) > visionHeight) continue; // Autre étage (ex. sommet des murs).

            // Résolu une seule fois : au moment où le taureau l'atteint, soit il esquive, soit il est percuté.
            _resolvedThisCharge.Add(player);
            if (IsAvoidingCharge(player)) continue;

            HitPlayer(player);
            hitSomeone = true;
            if (stopOnFirstPlayerHit) break;
        }
        return hitSomeone;
    }

    /// <summary>
    /// Le joueur évite-t-il la charge au moment où le taureau l'atteint ?
    /// Décidé chez l'hôte à partir de la position répliquée : les pieds du joueur (pivot du prefab) au-dessus
    /// du sol de plus de jumpClearHeight. Appelé une fois par joueur et par charge (un seul raycast).
    /// Point d'extension pour la roulade : ajouter ici un test d'invulnérabilité (ex. état « roulade »
    /// répliqué par le joueur) quand elle sera codée.
    /// </summary>
    private bool IsAvoidingCharge(Transform player)
    {
        return FeetHeightAboveGround(player) > jumpClearHeight;
    }

    private float FeetHeightAboveGround(Transform player)
    {
        Vector3 feet = player.position;
        // Le sol est cherché un peu au-dessus des pieds (pieds légèrement enfoncés) ; sans sol trouvé, on prend les pieds du taureau.
        // Le corps du taureau est en trigger pendant la charge : ignoré.
        if (Physics.Raycast(feet + Vector3.up, Vector3.down, out RaycastHit hit, visionHeight + 1f, groundLayer, QueryTriggerInteraction.Ignore))
            return feet.y - hit.point.y;
        return feet.y - transform.position.y;
    }

    /// <summary>
    /// Coins du sillage actuel en monde (avant gauche/droite à la pointe, arrière gauche/droite), à hauteur des pieds du taureau.
    /// Valable sur toutes les machines : sert à dessiner la fumée du sillage. Renvoie false hors charge.
    /// </summary>
    public bool GetWakeCorners(out Vector3 frontLeft, out Vector3 frontRight, out Vector3 backLeft, out Vector3 backRight)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        float length = CurrentWakeLength;
        float frontHalf = WakeFrontWidth * 0.5f;
        float backHalf = Mathf.Lerp(frontHalf, WakeBackWidth * 0.5f, wakeLength > 0f ? length / wakeLength : 1f);
        Vector3 apex = transform.position + forward * FrontOffset;
        Vector3 back = apex - forward * length;

        frontLeft = apex - right * frontHalf;
        frontRight = apex + right * frontHalf;
        backLeft = back - right * backHalf;
        backRight = back + right * backHalf;
        return IsCharging;
    }

    private void HitPlayer(Transform player)
    {
        // Appelé une fois par joueur et par charge : pas de GetComponent par frame.
        if (!player.TryGetComponent(out PlayerDamageReceiver receiver))
        {
            receiver = player.GetComponentInParent<PlayerDamageReceiver>();
            if (receiver == null) return;
        }

        Vector3 source = player.position - _chargeDir * KnockbackSourceDistance;
        receiver.ReceiveDamage(EnemyNightMode.ScaleDamage(_nightMode, chargeDamage), source, knockbackForce);
    }
    #endregion

    #region --- API publique (bouclier, combat à venir) ---
    /// <summary>
    /// Interrompt le taureau et l'étourdit (parade au bouclier…). Sans effet s'il est déjà étourdi ou mort.
    /// À appeler chez l'hôte (ou sans réseau) ; ignoré ailleurs.
    /// </summary>
    /// <param name="duration">Durée d'étourdissement (s) ; négatif = stunDuration.</param>
    public void Stun(float duration = -1f)
    {
        if (!_runsAI || _state == State.Init || _state == State.Dead || _state == State.Stunned) return;
        // Paré au bouclier : il rebondit comme contre un mur.
        EnterStunned(duration < 0f ? stunDuration : duration, true);
    }

    /// <summary>
    /// Le taureau encaisse un coup : anim « Hit » sur toutes les machines. Ne gère pas de points de vie
    /// et n'interrompt pas la charge. À appeler chez l'hôte (ou sans réseau) ; ignoré ailleurs.
    /// </summary>
    public void TakeHit()
    {
        if (!_runsAI || IsDead) return;
        PlayHitTrigger();
    }

    /// <summary>
    /// Mort du taureau : anim « Dead » sur toutes les machines et arrêt définitif de l'IA.
    /// À appeler chez l'hôte (ou sans réseau) ; ignoré ailleurs. Ne détruit pas l'objet.
    /// </summary>
    public void Die()
    {
        if (!_runsAI || IsDead) return;
        if (_agent.enabled)
        {
            if (_agent.isOnNavMesh) _agent.ResetPath();
            _agent.enabled = false;
        }
        EnterState(State.Dead);
    }
    #endregion

    #region --- Outils de déplacement ---
    private void StopAgent()
    {
        if (!_agent.isOnNavMesh) return;
        _agent.isStopped = true;
        _agent.ResetPath();
        _agent.velocity = Vector3.zero;
    }
    #endregion

    #region --- Animation et état visuel (toutes les machines) ---
    private void CacheAnimatorParameters()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        // Lu une seule fois (Awake) : animator.parameters alloue un tableau.
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == SpeedHash) _hasSpeedParam = true;
            else if (parameter.nameHash == PreparingHash) _hasPreparingParam = true;
            else if (parameter.nameHash == ChargingHash) _hasChargingParam = true;
            else if (parameter.nameHash == StunnedHash) _hasStunnedParam = true;
            else if (parameter.nameHash == HitHash) _hasHitParam = true;
            else if (parameter.nameHash == DeadHash) _hasDeadParam = true;
            else if (parameter.nameHash == TurningHash) _hasTurningParam = true;
            else if (parameter.nameHash == WallImpactHash) _hasWallImpactParam = true;
        }

        // C'est le NavMeshAgent (ou NetworkTransform) qui déplace le taureau, pas les clips.
        animator.applyRootMotion = false;
    }

    // Bools de l'Animator et collision du corps, déduits de l'état (hôte : en y entrant ; clients : état répliqué).
    private void ApplyStateVisuals(State state)
    {
        if (!_runsAI && IsSpawned) SetAnimatorBool(WallImpactHash, _hasWallImpactParam, _netWallImpact.Value);
        SetAnimatorBool(PreparingHash, _hasPreparingParam, state == State.Preparing);
        SetAnimatorBool(ChargingHash, _hasChargingParam, state == State.Charging);
        SetAnimatorBool(StunnedHash, _hasStunnedParam, state == State.Stunned);
        SetAnimatorBool(DeadHash, _hasDeadParam, state == State.Dead);

        // Pendant la charge, le corps ne bloque plus les joueurs : le CharacterController ne serait pas poussé
        // par un collider qui fonce sur lui (il se retrouverait dedans ou éjecté au-dessus), et le joueur qui saute
        // doit pouvoir « passer par-dessus ». Les dégâts sont gérés par la zone de frappe, pas par la collision.
        // Appliqué sur chaque machine : chaque joueur est simulé chez lui.
        if (_bodyCollider != null) _bodyCollider.isTrigger = state == State.Charging;
    }

    // Vitesse réelle déduite du déplacement du transform : identique chez l'hôte et chez les clients.
    private void UpdateAnimatorSpeed()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

        Vector3 position = transform.position;
        Vector3 delta = position - _lastAnimPosition;
        delta.y = 0f;
        _lastAnimPosition = position;

        // Demi-tour sur place : déduit de la vitesse de rotation (pas de réseau), maintenu un court instant
        // pour ne pas clignoter d'une frame à l'autre. Pendant la préparation, il se tourne en grattant le sol.
        Quaternion rotation = transform.rotation;
        float yawRate = Quaternion.Angle(rotation, _lastAnimRotation) / deltaTime;
        _lastAnimRotation = rotation;
        if (yawRate > TurningYawRate) _turningUntil = Time.time + TurningHold;

        if (animator == null) return;
        if (_hasSpeedParam)
        {
            float speed = IsDead ? 0f : delta.magnitude / deltaTime;
            animator.SetFloat(SpeedHash, speed, speedDampTime, deltaTime);
        }
        if (_hasTurningParam) animator.SetBool(TurningHash, !IsDead && Time.time < _turningUntil);
    }

    private void SetAnimatorBool(int hash, bool hasParam, bool value)
    {
        if (animator != null && hasParam) animator.SetBool(hash, value);
    }

    // Trigger Hit joué sur toutes les machines : par RPC en réseau, directement sinon.
    private void PlayHitTrigger()
    {
        if (IsSpawned)
        {
            if (IsServer) PlayHitRpc();
            return;
        }
        if (animator != null && _hasHitParam) animator.SetTrigger(HitHash);
    }

    [Rpc(SendTo.Everyone)]
    private void PlayHitRpc()
    {
        if (animator != null && _hasHitParam) animator.SetTrigger(HitHash);
    }
    #endregion

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || _state == State.Init) return;

        Vector3 forward = _state == State.Charging ? _chargeDir : FacingAxisDirection();
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.LookRotation(forward), Vector3.one);

        // Vision : bande de la largeur du couloir devant lui.
        float visionWidth = _corridorHalfWidth * 2f * AggroMultiplier;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(new Vector3(0f, visionHeight * 0.5f, maxVisionDistance * 0.5f), new Vector3(visionWidth, visionHeight, maxVisionDistance));

        Gizmos.matrix = old;

        // Sillage (zone de frappe) : trapèze derrière l'avant du taureau. Longueur max hors charge, longueur actuelle en charge.
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float length = _state == State.Charging ? CurrentWakeLength : wakeLength;
        float frontHalf = WakeFrontWidth * 0.5f;
        float backHalf = Mathf.Lerp(frontHalf, WakeBackWidth * 0.5f, length / wakeLength);
        Vector3 apex = transform.position + forward * FrontOffset + Vector3.up * 0.2f;
        Vector3 back = apex - forward * length;
        Vector3 fl = apex - right * frontHalf, fr = apex + right * frontHalf;
        Vector3 bl = back - right * backHalf, br = back + right * backHalf;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        Gizmos.DrawLine(fl, fr);
        Gizmos.DrawLine(fr, br);
        Gizmos.DrawLine(br, bl);
        Gizmos.DrawLine(bl, fl);
        // Seuil d'esquive au saut, à la pointe.
        Gizmos.DrawLine(fl + Vector3.up * jumpClearHeight, fr + Vector3.up * jumpClearHeight);

        // Zone de choc (si un mur est à moins de impactReach devant l'avant) : du centre du taureau jusqu'au mur, toute la largeur du couloir.
        Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
        float impactDepth = FrontOffset + impactReach;
        Vector3 impactCenter = transform.position + forward * (impactDepth * 0.5f) + Vector3.up * 0.2f;
        Gizmos.matrix = Matrix4x4.TRS(impactCenter, Quaternion.LookRotation(forward), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(_corridorHalfWidth * 2f, 0f, impactDepth));
        Gizmos.matrix = old;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(_patrolPointA, _patrolPointB);
    }
}
