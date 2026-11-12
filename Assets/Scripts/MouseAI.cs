using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// IA de la Souris, mob de base du labyrinthe (Souris.md, Bestiaire.md).
/// Machine à états du Bestiaire : Patrouille → Détection → Poursuite → Attaque → (Recherche) → Retour → Patrouille.
///
/// Réseau (ADR 002) : l'IA ne tourne que chez l'hôte (ou sans réseau). Ce composant ne doit PAS être mis
/// dans les serverOnlyComponents d'EnemyNetworkSetup : il reste actif sur toutes les machines pour animer
/// la souris. Chez un client, il ne fait que calculer « Speed » à partir du déplacement du transform
/// (répliqué par NetworkTransform) et rejouer les animations envoyées par l'hôte
/// (triggers Attack / Hit par RPC, bools Searching / Dead par NetworkVariable).
///
/// Contrat de l'Animator Controller : float Speed (m/s), trigger Attack, trigger Hit, bool Dead, bool Searching.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class MouseAI : NetworkBehaviour
{
    private enum State { Patrol, Detection, Chase, Attack, Search, Return, Hit, Dead }

    #region --- Paramètres Inspector ---
    [Header("1. Détection (vue)")]
    [Tooltip("Distance de détection en mètres (multipliée la nuit par EnemyNightMode.AggroMultiplier)")]
    [Min(0f)]
    [SerializeField] private float viewRadius = 30f;

    [Tooltip("Angle du cône de vision en degrés (en patrouille ; pendant la poursuite, seule la distance et les murs comptent)")]
    [Range(0f, 360f)]
    [SerializeField] private float viewAngle = 120f;

    [Tooltip("En poursuite, la souris perd le joueur au-delà de viewRadius × ce facteur")]
    [Min(1f)]
    [SerializeField] private float loseSightRadiusMultiplier = 1.5f;

    [Tooltip("Hauteur des yeux (m) au-dessus du pied de la souris, pour le test de ligne de vue")]
    [Min(0f)]
    [SerializeField] private float eyeHeight = 1f;

    [Tooltip("Intervalle (s) entre deux recherches de joueur visible (économise les raycasts)")]
    [Min(0.02f)]
    [SerializeField] private float perceptionInterval = 0.15f;

    [Header("2. Filtres")]
    [Tooltip("Calque des joueurs (Player)")]
    [SerializeField] private LayerMask playerMask;

    [Tooltip("Calques qui bloquent la vue (Wall)")]
    [SerializeField] private LayerMask obstacleMask;

    [Header("3. Déplacement")]
    [Tooltip("Vitesse en patrouille et au retour (m/s). Joueur : 15 en marche, 30 en sprint")]
    [Min(0f)]
    [SerializeField] private float patrolSpeed = 8f;

    [Tooltip("Vitesse en poursuite et en recherche (m/s) : un peu moins que le sprint du joueur")]
    [Min(0f)]
    [SerializeField] private float chaseSpeed = 22f;

    [Tooltip("Rayon (m) autour du point d'apparition dans lequel la souris choisit ses points de patrouille")]
    [Min(1f)]
    [SerializeField] private float patrolRange = 30f;

    [Tooltip("Pause (s) à chaque point de patrouille")]
    [Min(0f)]
    [SerializeField] private float waitTimeAtPoint = 2f;

    [Tooltip("Vitesse de rotation (×) quand elle se tourne vers sa cible à l'arrêt")]
    [Min(0f)]
    [SerializeField] private float turnSpeed = 10f;

    [Header("4. Comportement")]
    [Tooltip("Temps de réaction (s) entre le moment où elle repère un joueur et le début de la poursuite")]
    [Min(0f)]
    [SerializeField] private float reactionTime = 0.6f;

    [Tooltip("Temps (s) sans voir sa cible avant d'abandonner la poursuite et de la chercher")]
    [Min(0f)]
    [SerializeField] private float loseTargetDelay = 1.5f;

    [Tooltip("Durée (s) du reniflement sur le dernier endroit où le joueur a été vu")]
    [Min(0f)]
    [SerializeField] private float searchDuration = 4f;

    [Tooltip("Vitesse de rotation (degrés/s) pendant le reniflement")]
    [Min(0f)]
    [SerializeField] private float searchTurnSpeed = 90f;

    [Header("5. Attaque (morsure)")]
    [Tooltip("Dégâts d'une morsure en demi-cœurs (multipliés la nuit, arrondis au-dessus)")]
    [Min(0)]
    [SerializeField] private int damage = 1;

    [Tooltip("Portée de la morsure (m, distance horizontale entre la souris et le joueur)")]
    [Min(0.1f)]
    [SerializeField] private float attackRange = 4f;

    [Tooltip("Temps de préparation (s) entre le début de l'anim d'attaque et les dégâts : le joueur peut esquiver en sortant de la portée")]
    [Min(0f)]
    [SerializeField] private float attackWindup = 0.4f;

    [Tooltip("Délai minimal (s) entre deux morsures")]
    [Min(0f)]
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("6. Réaction aux coups")]
    [Tooltip("Durée (s) pendant laquelle la souris est immobilisée après TakeHit()")]
    [Min(0f)]
    [SerializeField] private float hitStunDuration = 0.4f;

    [Header("7. Animation")]
    [Tooltip("Animator du modèle ; si vide, cherché dans les enfants")]
    [SerializeField] private Animator animator;

    [Tooltip("Lissage (s) du paramètre Speed")]
    [Min(0f)]
    [SerializeField] private float speedDampTime = 0.1f;
    #endregion

    // --- Contrat de l'Animator ---
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DeadHash = Animator.StringToHash("Dead");
    private static readonly int SearchingHash = Animator.StringToHash("Searching");

    // Distance d'arrêt hors poursuite et marge pour considérer une destination atteinte.
    private const float DefaultStoppingDistance = 1f;
    private const float ArrivalTolerance = 0.5f;
    // En poursuite, l'agent s'arrête un peu avant la portée de morsure.
    private const float ChaseStoppingRatio = 0.8f;
    // Temps maximal pour atteindre le dernier endroit où le joueur a été vu.
    private const float MaxSearchTravelTime = 8f;
    // Intervalle de recalcul du chemin vers la cible pendant la poursuite.
    private const float RepathInterval = 0.2f;

    // États répliqués chez les clients (écrits par l'hôte seulement).
    private readonly NetworkVariable<bool> _netSearching = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<bool> _netDead = new NetworkVariable<bool>(false);

    private NavMeshAgent _agent;
    private EnemyNightMode _nightMode;

    // Vrai là où l'IA tourne : chez l'hôte, ou partout sans réseau.
    private bool _runsAI;
    private bool _aiInitialized;

    private State _state = State.Patrol;
    private State _stateBeforeDetection;
    private float _stateTimer;

    private Vector3 _homePosition;
    private bool _hasPatrolDestination;

    private Transform _target;
    private PlayerDamageReceiver _targetReceiver;
    private Vector3 _lastKnownTargetPosition;
    private bool _targetVisible;
    private float _lostTargetTimer;

    private float _nextPerceptionTime;
    private float _nextRepathTime;
    private float _nextAttackTime;
    private bool _attackDamageDone;

    private bool _searchArrived;
    private float _searchTurnDirection = 1f;
    private bool _searchTurnFlipped;

    // Paramètres présents dans l'Animator Controller (évite les avertissements s'il en manque).
    private bool _hasSpeedParam, _hasAttackParam, _hasHitParam, _hasDeadParam, _hasSearchingParam;
    private Vector3 _lastAnimPosition;

    /// <summary>Vrai quand la souris est morte.</summary>
    public bool IsDead => _state == State.Dead || _netDead.Value;

    private float SpeedMultiplier => _nightMode != null ? _nightMode.SpeedMultiplier : 1f;
    private float AggroMultiplier => _nightMode != null ? _nightMode.AggroMultiplier : 1f;
    private float CurrentViewRadius => viewRadius * AggroMultiplier;
    private float CurrentPatrolSpeed => patrolSpeed * SpeedMultiplier;
    private float CurrentChaseSpeed => chaseSpeed * SpeedMultiplier;

    #region --- Cycle de vie ---
    private void Reset()
    {
        playerMask = LayerMask.GetMask("Player");
        obstacleMask = LayerMask.GetMask("Wall");
    }

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _nightMode = GetComponent<EnemyNightMode>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        CacheAnimatorParameters();

        // Sans réseau, l'IA tourne ici ; en réseau, OnNetworkSpawn décide (hôte seulement).
        NetworkManager manager = NetworkManager.Singleton;
        _runsAI = manager == null || !manager.IsListening;

        _lastAnimPosition = transform.position;
    }

    private void OnEnable()
    {
        if (_nightMode != null) _nightMode.OnNightChanged += HandleNightChanged;
    }

    private void OnDisable()
    {
        if (_nightMode != null) _nightMode.OnNightChanged -= HandleNightChanged;
    }

    public override void OnNetworkSpawn()
    {
        _runsAI = IsServer;

        _netSearching.OnValueChanged += HandleSearchingChanged;
        _netDead.OnValueChanged += HandleDeadChanged;

        // Client qui rejoint en cours de partie : on applique l'état actuel.
        SetAnimatorBool(SearchingHash, _hasSearchingParam, _netSearching.Value);
        SetAnimatorBool(DeadHash, _hasDeadParam, _netDead.Value);
    }

    public override void OnNetworkDespawn()
    {
        _runsAI = false;
        _netSearching.OnValueChanged -= HandleSearchingChanged;
        _netDead.OnValueChanged -= HandleDeadChanged;
    }

    private void Update()
    {
        UpdateAnimatorSpeed();

        if (!_runsAI || _state == State.Dead) return;
        if (!_aiInitialized && !TryInitializeAI()) return;

        if (Time.time >= _nextPerceptionTime)
        {
            _nextPerceptionTime = Time.time + perceptionInterval;
            UpdatePerception();
        }

        switch (_state)
        {
            case State.Patrol: UpdatePatrol(); break;
            case State.Detection: UpdateDetection(); break;
            case State.Chase: UpdateChase(); break;
            case State.Attack: UpdateAttack(); break;
            case State.Search: UpdateSearch(); break;
            case State.Return: UpdateReturn(); break;
            case State.Hit: UpdateHit(); break;
        }
    }

    // L'agent est activé par EnemyNetworkSetup et placé par SpawnerManager : on attend qu'il soit sur le NavMesh.
    private bool TryInitializeAI()
    {
        if (!_agent.enabled || !_agent.isOnNavMesh) return false;

        _homePosition = transform.position;
        _agent.autoBraking = true;
        _aiInitialized = true;
        EnterState(State.Patrol);
        return true;
    }

    private void HandleNightChanged(bool isNight)
    {
        if (_aiInitialized && _agent.enabled) ApplySpeedForState();
    }
    #endregion

    #region --- Perception ---
    private void UpdatePerception()
    {
        switch (_state)
        {
            case State.Patrol:
            case State.Return:
            case State.Search:
                // Cherche le joueur visible le plus proche (multijoueur : PlayerRegistry).
                Transform seen = FindClosestVisiblePlayer();
                if (seen == null) break;
                SetTarget(seen);
                _lastKnownTargetPosition = seen.position;
                _targetVisible = true;
                // Déjà sur ses gardes pendant la recherche : elle reprend la poursuite tout de suite.
                if (_state == State.Search) EnterState(State.Chase);
                else EnterDetection();
                break;

            case State.Detection:
            case State.Chase:
            case State.Attack:
            case State.Hit:
                _targetVisible = _target != null && CanStillSee(_target);
                if (_targetVisible) _lastKnownTargetPosition = _target.position;
                break;
        }
    }

    private Transform FindClosestVisiblePlayer()
    {
        Transform best = null;
        float bestDistance = float.MaxValue;
        var players = PlayerRegistry.Players;
        for (int i = 0; i < players.Count; i++)
        {
            Transform candidate = players[i];
            if (candidate == null || !CanSeeInCone(candidate)) continue;

            float distance = (candidate.position - transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }
        return best;
    }

    // Vue normale : distance, cône et murs.
    private bool CanSeeInCone(Transform target)
    {
        Vector3 toTarget = target.position - transform.position;
        float radius = CurrentViewRadius;
        if (toTarget.sqrMagnitude > radius * radius) return false;

        Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
        if (flat.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, flat) > viewAngle * 0.5f) return false;

        return HasLineOfSight(target);
    }

    // Pendant la poursuite : distance élargie et murs, sans cône (elle a le joueur en ligne de mire).
    private bool CanStillSee(Transform target)
    {
        float radius = CurrentViewRadius * loseSightRadiusMultiplier;
        if ((target.position - transform.position).sqrMagnitude > radius * radius) return false;
        return HasLineOfSight(target);
    }

    private bool HasLineOfSight(Transform target)
    {
        Vector3 eyes = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPoint = target.position + Vector3.up * eyeHeight;
        return !Physics.Linecast(eyes, targetPoint, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    private void SetTarget(Transform target)
    {
        if (target == _target) return;
        _target = target;
        // Récupéré une seule fois par changement de cible, pas à chaque frame.
        _targetReceiver = null;
        if (target != null && !target.TryGetComponent(out _targetReceiver))
        {
            _targetReceiver = target.GetComponentInParent<PlayerDamageReceiver>();
        }
    }
    #endregion

    #region --- États ---
    private void EnterState(State newState)
    {
        _state = newState;
        _stateTimer = 0f;

        if (newState != State.Search) SetSearching(false);

        switch (newState)
        {
            case State.Patrol:
                SetTarget(null);
                _hasPatrolDestination = false;
                ResumeAgent(DefaultStoppingDistance);
                break;

            case State.Detection:
            case State.Attack:
            case State.Hit:
                StopAgent();
                break;

            case State.Chase:
                _lostTargetTimer = 0f;
                _nextRepathTime = 0f;
                ResumeAgent(attackRange * ChaseStoppingRatio);
                break;

            case State.Search:
                _searchArrived = false;
                _searchTurnFlipped = false;
                _searchTurnDirection = Random.value < 0.5f ? -1f : 1f;
                ResumeAgent(DefaultStoppingDistance);
                _agent.SetDestination(_lastKnownTargetPosition);
                break;

            case State.Return:
                SetTarget(null);
                ResumeAgent(DefaultStoppingDistance);
                _agent.SetDestination(_homePosition);
                break;
        }

        ApplySpeedForState();
    }

    private void EnterDetection()
    {
        _stateBeforeDetection = _state;
        EnterState(State.Detection);
    }

    private void ApplySpeedForState()
    {
        bool fast = _state == State.Chase || _state == State.Attack || _state == State.Search || _state == State.Hit;
        _agent.speed = fast ? CurrentChaseSpeed : CurrentPatrolSpeed;
    }

    // --- Patrouille : points aléatoires autour du point d'apparition ---
    private void UpdatePatrol()
    {
        if (!_hasPatrolDestination)
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f) return;

            if (TryGetPatrolPoint(out Vector3 point))
            {
                _agent.SetDestination(point);
                _hasPatrolDestination = true;
            }
            else
            {
                _stateTimer = 0.5f; // Pas de point valide : on réessaie un peu plus tard.
            }
            return;
        }

        if (HasArrived())
        {
            _hasPatrolDestination = false;
            _stateTimer = waitTimeAtPoint;
        }
    }

    private bool TryGetPatrolPoint(out Vector3 point)
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 random = _homePosition + Random.insideUnitSphere * patrolRange;
            if (!NavMesh.SamplePosition(random, out NavMeshHit hit, patrolRange, _agent.areaMask)) continue;

            // Comme l'araignée : on garde un point visible depuis la souris (pas de long détour par le labyrinthe).
            Vector3 origin = transform.position + Vector3.up * eyeHeight;
            Vector3 destination = hit.position + Vector3.up * eyeHeight;
            if (Physics.Linecast(origin, destination, obstacleMask, QueryTriggerInteraction.Ignore)) continue;

            point = hit.position;
            return true;
        }
        point = Vector3.zero;
        return false;
    }

    // --- Détection : elle s'arrête et fixe le joueur avant de foncer ---
    private void UpdateDetection()
    {
        if (_target == null || !_targetVisible)
        {
            // Fausse alerte : elle reprend ce qu'elle faisait.
            EnterState(_stateBeforeDetection == State.Return ? State.Return : State.Patrol);
            return;
        }

        FaceTowards(_target.position);
        _stateTimer += Time.deltaTime;
        if (_stateTimer >= reactionTime) EnterState(State.Chase);
    }

    // --- Poursuite ---
    private void UpdateChase()
    {
        if (_target == null)
        {
            EnterState(State.Search);
            return;
        }

        if (_targetVisible)
        {
            _lostTargetTimer = 0f;

            if (Time.time >= _nextRepathTime)
            {
                _nextRepathTime = Time.time + RepathInterval;
                _agent.SetDestination(_target.position);
            }

            if (HorizontalDistanceTo(_target.position) <= attackRange)
            {
                FaceTowards(_target.position);
                if (Time.time >= _nextAttackTime) EnterAttack();
            }
        }
        else
        {
            // Elle continue vers le dernier endroit connu, puis abandonne et cherche.
            _lostTargetTimer += Time.deltaTime;
            if (Time.time >= _nextRepathTime)
            {
                _nextRepathTime = Time.time + RepathInterval;
                _agent.SetDestination(_lastKnownTargetPosition);
            }
            if (_lostTargetTimer >= loseTargetDelay) EnterState(State.Search);
        }
    }

    // --- Attaque : préparation, puis morsure si le joueur est encore à portée ---
    private void EnterAttack()
    {
        _attackDamageDone = false;
        _nextAttackTime = Time.time + attackCooldown;
        EnterState(State.Attack);
        PlayTrigger(AttackHash, _hasAttackParam);
    }

    private void UpdateAttack()
    {
        _stateTimer += Time.deltaTime;
        if (_target != null) FaceTowards(_target.position);

        if (!_attackDamageDone && _stateTimer >= attackWindup)
        {
            _attackDamageDone = true;
            TryBite();
            EnterState(_target != null ? State.Chase : State.Search);
        }
    }

    private void TryBite()
    {
        if (_target == null || _targetReceiver == null) return;
        if (HorizontalDistanceTo(_target.position) > attackRange) return; // Esquivée.
        if (!HasLineOfSight(_target)) return;

        // Multijoueur : l'hôte envoie les dégâts à la machine du joueur touché (PlayerDamageReceiver).
        _targetReceiver.ReceiveDamage(EnemyNightMode.ScaleDamage(_nightMode, damage), transform.position);
    }

    // --- Recherche : va au dernier endroit connu, puis renifle en tournant sur place ---
    private void UpdateSearch()
    {
        _stateTimer += Time.deltaTime;

        if (!_searchArrived)
        {
            if (HasArrived() || _stateTimer >= MaxSearchTravelTime)
            {
                _searchArrived = true;
                _stateTimer = 0f;
                StopAgent();
                SetSearching(true);
            }
            return;
        }

        // Change de sens à mi-recherche pour regarder des deux côtés.
        if (!_searchTurnFlipped && _stateTimer >= searchDuration * 0.5f)
        {
            _searchTurnFlipped = true;
            _searchTurnDirection = -_searchTurnDirection;
        }
        transform.Rotate(0f, _searchTurnDirection * searchTurnSpeed * Time.deltaTime, 0f);

        if (_stateTimer >= searchDuration) EnterState(State.Return);
    }

    // --- Retour au point d'apparition, puis patrouille ---
    private void UpdateReturn()
    {
        if (HasArrived()) EnterState(State.Patrol);
    }

    // --- Touchée : immobilisée un court instant ---
    private void UpdateHit()
    {
        _stateTimer += Time.deltaTime;
        if (_stateTimer < hitStunDuration) return;
        EnterState(_target != null ? State.Chase : State.Return);
    }
    #endregion

    #region --- API publique (combat à venir) ---
    /// <summary>
    /// La souris encaisse un coup : anim « Hit » et courte immobilisation.
    /// À appeler chez l'hôte (ou sans réseau) ; ignoré ailleurs. Ne gère pas de points de vie.
    /// </summary>
    public void TakeHit()
    {
        if (!_runsAI || IsDead) return;
        PlayTrigger(HitHash, _hasHitParam);
        if (_aiInitialized) EnterState(State.Hit);
    }

    /// <summary>
    /// Mort de la souris : anim « Dead » sur toutes les machines et arrêt définitif de l'IA.
    /// À appeler chez l'hôte (ou sans réseau) ; ignoré ailleurs. Ne détruit pas l'objet.
    /// </summary>
    public void Die()
    {
        if (!_runsAI || IsDead) return;

        _state = State.Dead;
        SetSearching(false);
        SetTarget(null);
        if (_agent.enabled)
        {
            if (_agent.isOnNavMesh) _agent.ResetPath();
            _agent.enabled = false;
        }

        SetAnimatorBool(DeadHash, _hasDeadParam, true);
        if (IsSpawned) _netDead.Value = true;
    }
    #endregion

    #region --- Outils de déplacement ---
    private void StopAgent()
    {
        if (!_agent.isOnNavMesh) return;
        _agent.isStopped = true;
        _agent.ResetPath();
    }

    private void ResumeAgent(float stoppingDistance)
    {
        if (!_agent.isOnNavMesh) return;
        _agent.stoppingDistance = stoppingDistance;
        _agent.isStopped = false;
    }

    private bool HasArrived()
    {
        return !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + ArrivalTolerance;
    }

    private void FaceTowards(Vector3 point)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * turnSpeed);
    }

    private float HorizontalDistanceTo(Vector3 point)
    {
        Vector3 delta = point - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }
    #endregion

    #region --- Animation (toutes les machines) ---
    private void CacheAnimatorParameters()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        // Lu une seule fois (Awake) : animator.parameters alloue un tableau.
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == SpeedHash) _hasSpeedParam = true;
            else if (parameter.nameHash == AttackHash) _hasAttackParam = true;
            else if (parameter.nameHash == HitHash) _hasHitParam = true;
            else if (parameter.nameHash == DeadHash) _hasDeadParam = true;
            else if (parameter.nameHash == SearchingHash) _hasSearchingParam = true;
        }

        // C'est le NavMeshAgent (ou NetworkTransform) qui déplace la souris, pas les clips.
        animator.applyRootMotion = false;
    }

    // Vitesse réelle déduite du déplacement du transform : identique chez l'hôte et chez les clients.
    private void UpdateAnimatorSpeed()
    {
        Vector3 position = transform.position;
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

        Vector3 delta = position - _lastAnimPosition;
        delta.y = 0f;
        _lastAnimPosition = position;

        if (animator == null || !_hasSpeedParam) return;
        float speed = IsDead ? 0f : delta.magnitude / deltaTime;
        animator.SetFloat(SpeedHash, speed, speedDampTime, deltaTime);
    }

    private void SetSearching(bool searching)
    {
        SetAnimatorBool(SearchingHash, _hasSearchingParam, searching);
        if (IsSpawned && IsServer && _netSearching.Value != searching) _netSearching.Value = searching;
    }

    private void HandleSearchingChanged(bool previous, bool current) => SetAnimatorBool(SearchingHash, _hasSearchingParam, current);
    private void HandleDeadChanged(bool previous, bool current) => SetAnimatorBool(DeadHash, _hasDeadParam, current);

    private void SetAnimatorBool(int hash, bool hasParam, bool value)
    {
        if (animator != null && hasParam) animator.SetBool(hash, value);
    }

    // Trigger joué sur toutes les machines : par RPC en réseau, directement sinon.
    private void PlayTrigger(int hash, bool hasParam)
    {
        if (IsSpawned)
        {
            if (IsServer) PlayTriggerRpc(hash);
            return;
        }
        if (animator != null && hasParam) animator.SetTrigger(hash);
    }

    [Rpc(SendTo.Everyone)]
    private void PlayTriggerRpc(int hash)
    {
        if (animator == null) return;
        if ((hash == AttackHash && _hasAttackParam) || (hash == HitHash && _hasHitParam)) animator.SetTrigger(hash);
    }
    #endregion

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);
        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawLine(transform.position, transform.position + left * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + right * viewRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (Application.isPlaying && _aiInitialized)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_homePosition, patrolRange);
        }
    }
}
