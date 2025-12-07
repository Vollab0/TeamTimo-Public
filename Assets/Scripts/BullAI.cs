using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BullAI : MonoBehaviour
{
    #region --- PARAMETRES INSPECTOR ---
    [Header("1. Vision")]
    [SerializeField] private float visionWidthRatio = 30.0f;
    [SerializeField] private float visionHeight = 5.0f;
    [SerializeField] private float maxVisionDistance = 300f;

    [Header("Layers")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("2. Mouvement")]
    [SerializeField] private float patrolSpeed = 8f;
    [SerializeField] private float chargeSpeed = 100f;
    [SerializeField] private float stunDuration = 5.0f;
    [Range(0, 100)][SerializeField] private float randomTurnChance = 0f;

    [Header("3. Dégâts (Cumulatifs)")]
    [Tooltip("Rayon de la zone extérieure (Vapeur)")]
    [SerializeField] private float zoneRadius = 15.0f;

    [Tooltip("Rayon de la zone intérieure (Corps à corps)")]
    [SerializeField] private float contactThreshold = 5.0f;

    [Tooltip("Dégâts de la zone extérieure")]
    [SerializeField] private int zoneDamage = 1;

    [Tooltip("Dégâts SUPPLÉMENTAIRES du contact")]
    [SerializeField] private int contactDamage = 1;

    [Tooltip("Temps avant de reprendre des dégâts de ZONE")]
    [SerializeField] private float zoneCooldown = 1.0f;

    [Tooltip("Temps avant de reprendre des dégâts de CONTACT")]
    [SerializeField] private float contactCooldown = 1.5f;
    #endregion

    // --- NOUVEAU : VARIABLES RAIL ---
    private bool _isAxisX; // True = Couloir Est/Ouest, False = Nord/Sud
    private float _fixedAxisCoordinate; // La coordonnée "sacrée" qui ne doit jamais changer
    private bool _railInitialized = false;

    private NavMeshAgent _agent;
    private Vector3 _patrolPointA, _patrolPointB, _currentTarget;
    private bool _isCharging = false, _isStunned = false, _preparingCharge = false;
    private float _actualBodyWidth, _actualVisionWidth;

    private float _nextZoneHitTime = 0f;
    private float _nextContactHitTime = 0f;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = patrolSpeed;
        _agent.autoBraking = true;
        _actualBodyWidth = _agent.radius * 2f;
        _actualVisionWidth = _actualBodyWidth * visionWidthRatio;

        StartCoroutine(InitRoutine());
    }

    IEnumerator InitRoutine()
    {
        yield return null;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 5.0f, _agent.areaMask))
            _agent.Warp(hit.position);

        LockToCorridorAxis();
        _currentTarget = _patrolPointA;
        _agent.SetDestination(_currentTarget);
        StartCoroutine(RandomTurnRoutine());
    }

    void Update()
    {
        if (_preparingCharge || _isStunned) return;

        if (_isCharging)
        {
            CheckImpact();
            ProcessCombat(); // Gestion des dégâts
            return;
        }

        PatrolLogic();
        ScanForPlayer();
        EnforceRailPosition();
    }

    void EnforceRailPosition()
    {
        if (!_railInitialized || _isStunned) return;

        Vector3 currentPos = transform.position;
        bool needCorrection = false;

        // Si je suis censé être fixe en X mais que j'ai bougé...
        if (!_isAxisX && Mathf.Abs(currentPos.x - _fixedAxisCoordinate) > 0.1f)
        {
            currentPos.x = Mathf.Lerp(currentPos.x, _fixedAxisCoordinate, Time.deltaTime * 5f);
            needCorrection = true;
        }
        // Si je suis censé être fixe en Z mais que j'ai bougé...
        else if (_isAxisX && Mathf.Abs(currentPos.z - _fixedAxisCoordinate) > 0.1f)
        {
            currentPos.z = Mathf.Lerp(currentPos.z, _fixedAxisCoordinate, Time.deltaTime * 5f);
            needCorrection = true;
        }

        if (needCorrection) _agent.nextPosition = currentPos;
    }

    // --- LOGIQUE DE DÉGÂTS CUMULATIFS ---
    void ProcessCombat()
    {
        // On cherche le joueur dans la plus grande zone (Zone Radius)
        Collider[] hits = Physics.OverlapSphere(transform.position, zoneRadius, playerLayer);

        foreach (var hit in hits)
        {
            PlayerHealth playerHP = hit.GetComponent<PlayerHealth>();

            if (playerHP != null)
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);

                // --- COUCHE 1 : Dégâts de ZONE (Extérieur) ---
                // Si on est dans le rayon de zone ET que le cooldown de zone est fini
                if (distance <= zoneRadius && Time.time >= _nextZoneHitTime)
                {
                    // Debug.Log("Hit Zone !");
                    playerHP.Blesser(zoneDamage, transform.position);
                    _nextZoneHitTime = Time.time + zoneCooldown;
                }

                // --- COUCHE 2 : Dégâts de CONTACT (Intérieur) ---
                // Si on est assez proche pour le contact ET que le cooldown de contact est fini
                // Note : C'est une condition indépendante. On peut prendre les deux dans la même frame.
                if (distance <= contactThreshold && Time.time >= _nextContactHitTime)
                {
                    // Debug.Log("Hit Contact !");
                    playerHP.Blesser(contactDamage, transform.position);
                    _nextContactHitTime = Time.time + contactCooldown;
                }

                // On s'arrête au premier joueur trouvé
                break;
            }
        }
    }

    void LockToCorridorAxis()
    {
        Vector3 origin = transform.position + Vector3.up * 2.0f;
        float dN = GetRayDist(origin, Vector3.forward);
        float dS = GetRayDist(origin, Vector3.back);
        float dE = GetRayDist(origin, Vector3.right);
        float dW = GetRayDist(origin, Vector3.left);
        float zLen = dN + dS; float xLen = dE + dW;
        Vector3 finalPos = transform.position;
        float y = transform.position.y; float margin = _agent.radius + 2.0f;

        if (zLen > xLen)
        {
            _isAxisX = false; // On note qu'on est sur l'axe Z (donc X est fixe)
            float centerX = ((origin.x + dE) + (origin.x - dW)) / 2f;
            _fixedAxisCoordinate = centerX; // <--- ON SAUVEGARDE LE CENTRE ICI
            finalPos = new Vector3(centerX, y, transform.position.z);
            _patrolPointA = new Vector3(centerX, y, origin.z + dN - margin);
            _patrolPointB = new Vector3(centerX, y, origin.z - dS + margin);
            transform.rotation = Quaternion.LookRotation(Vector3.forward);
        }
        else
        {
            _isAxisX = true; // On note qu'on est sur l'axe X (donc Z est fixe)
            float centerZ = ((origin.z + dN) + (origin.z - dS)) / 2f;
            _fixedAxisCoordinate = centerZ; // <--- ON SAUVEGARDE LE CENTRE ICI
            finalPos = new Vector3(transform.position.x, y, centerZ);
            _patrolPointA = new Vector3(origin.x + dE - margin, y, centerZ);
            _patrolPointB = new Vector3(origin.x - dW + margin, y, centerZ);
            transform.rotation = Quaternion.LookRotation(Vector3.right);
        }
        _agent.Warp(finalPos);
        _railInitialized = true; // On valide que le rail est prêt
    }

    float GetRayDist(Vector3 origin, Vector3 dir)
    {
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxVisionDistance, obstacleLayer))
            return hit.distance;
        return maxVisionDistance;
    }

    void PatrolLogic()
    {
        if (!_agent.pathPending && _agent.remainingDistance < 2.0f)
        {
            _currentTarget = (_currentTarget == _patrolPointA) ? _patrolPointB : _patrolPointA;
            _agent.SetDestination(_currentTarget);
        }
    }

    IEnumerator RandomTurnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1.0f);
            if (!_isCharging && !_preparingCharge && !_isStunned && _agent.velocity.magnitude > 0.5f)
            {
                if (Random.Range(0, 100) < randomTurnChance)
                {
                    _currentTarget = (_currentTarget == _patrolPointA) ? _patrolPointB : _patrolPointA;
                    _agent.SetDestination(_currentTarget);
                }
            }
        }
    }

    void ScanForPlayer()
    {
        RaycastHit hit;
        Vector3 center = transform.position + (Vector3.up * (visionHeight / 2f));
        Vector3 halfExtents = new Vector3(_actualVisionWidth / 2f, visionHeight / 2f, 0.1f);
        if (Physics.BoxCast(center, halfExtents, transform.forward, out hit, transform.rotation, maxVisionDistance, playerLayer))
        {
            Vector3 eyes = transform.position + Vector3.up * (visionHeight * 0.8f);
            if (!Physics.Linecast(eyes, hit.collider.bounds.center, obstacleLayer))
            {
                StartCoroutine(ExecuteChargeSequence());
            }
        }
    }

    IEnumerator ExecuteChargeSequence()
    {
        _preparingCharge = true; _agent.isStopped = true; _agent.velocity = Vector3.zero;

        // On s'assure qu'on regarde parfaitement droit avant de partir
        SnapRotationToAxis();

        yield return new WaitForSeconds(1.0f);

        _preparingCharge = false;
        _isCharging = true;

        // IMPORTANT : On interdit au NavMesh de tourner pendant la charge
        _agent.updateRotation = false;

        // Calcul de la destination STRICTE (Sur le rail)
        Vector3 chargeDir = transform.forward;
        Vector3 targetPos = transform.position + (chargeDir * maxVisionDistance);

        // Raycast pour s'arrêter au mur
        if (Physics.Raycast(transform.position + Vector3.up, chargeDir, out RaycastHit wallHit, maxVisionDistance, obstacleLayer))
        {
            targetPos = wallHit.point;
        }

        // CORRECTION DE DESTINATION : On écrase la coordonnée latérale pour qu'elle soit égale au rail
        if (!_isAxisX) targetPos.x = _fixedAxisCoordinate; // Force X
        else targetPos.z = _fixedAxisCoordinate; // Force Z

        _agent.isStopped = false;
        _agent.speed = chargeSpeed;
        _agent.autoBraking = false;
        _agent.SetDestination(targetPos);
    }

    void SnapRotationToAxis()
    {
        // On force la rotation aux 4 cardinaux (0, 90, 180, 270)
        float y = transform.eulerAngles.y;
        float snappedY = Mathf.Round(y / 90f) * 90f;
        transform.rotation = Quaternion.Euler(0, snappedY, 0);
    }
    void CheckImpact()
    {
        if (!_agent.pathPending && _agent.remainingDistance < 3.5f) StartCoroutine(StunSequence());
    }

    IEnumerator StunSequence()
    {
        _isCharging = false; _isStunned = true;
        _agent.isStopped = true; _agent.velocity = Vector3.zero;
        _agent.Warp(transform.position - transform.forward * 4.0f);
        yield return new WaitForSeconds(stunDuration);
        _isStunned = false; _agent.isStopped = false; _agent.speed = patrolSpeed; _agent.autoBraking = true;
        _agent.updateRotation = true;
        float distA = Vector3.Distance(transform.position, _patrolPointA);
        float distB = Vector3.Distance(transform.position, _patrolPointB);
        _currentTarget = (distA > distB) ? _patrolPointA : _patrolPointB;
        _agent.SetDestination(_currentTarget);
    }

    private void OnDrawGizmosSelected()
    {
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        float w = (_agent != null) ? _agent.radius * 2 * visionWidthRatio : 1f;

        // Vision
        Gizmos.color = Color.red;
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.up * visionHeight / 2, new Vector3(w, visionHeight, 0));
        Gizmos.DrawWireCube((Vector3.up * visionHeight / 2) + Vector3.forward * maxVisionDistance, new Vector3(w, visionHeight, 0));
        Gizmos.matrix = old;

        // Zone 1 PV (Orange)
        Gizmos.color = new Color(1, 0.5f, 0, 0.4f);
        Gizmos.DrawWireSphere(transform.position, zoneRadius);

        // Zone 2 PV (Rouge Sang - Contact)
        Gizmos.color = new Color(1, 0, 0, 0.8f);
        Gizmos.DrawWireSphere(transform.position, contactThreshold);
    }
}