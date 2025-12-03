using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BullAI : MonoBehaviour
{
    #region --- PARAMETRES INSPECTOR ---
    [Header("1. Vision")]
    [Tooltip("Largeur de vision = Rayon du corps * ce ratio")]
    [SerializeField] private float visionWidthRatio = 2.0f;
    [SerializeField] private float visionHeight = 2.0f;
    [SerializeField] private float maxVisionDistance = 100f;

    [Header("Layers")]
    [SerializeField] private LayerMask playerLayer;
    [Tooltip("Doit inclure 'Wall' et les Piliers")]
    [SerializeField] private LayerMask obstacleLayer;

    [Header("2. Mouvement")]
    [SerializeField] private float patrolSpeed = 8f;
    [SerializeField] private float chargeSpeed = 40f;
    [SerializeField] private float stunDuration = 2.0f;
    [Range(0, 100)][SerializeField] private float randomTurnChance = 10f;
    #endregion

    private NavMeshAgent _agent;
    private Vector3 _patrolPointA, _patrolPointB, _currentTarget;
    private bool _isCharging = false, _isStunned = false, _preparingCharge = false;
    private float _actualBodyWidth, _actualVisionWidth;

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

        // 1. Sécurité Sol
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 5.0f, _agent.areaMask))
            _agent.Warp(hit.position);

        // 2. Centrage strict (Rail)
        LockToCorridorAxis();

        // 3. Patrouille
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
            return;
        }

        PatrolLogic();
        ScanForPlayer();
    }

    // --- CENTRAGE (RAIL) ---
    void LockToCorridorAxis()
    {
        Vector3 origin = transform.position + Vector3.up * 2.0f;

        float dN = GetRayDist(origin, Vector3.forward);
        float dS = GetRayDist(origin, Vector3.back);
        float dE = GetRayDist(origin, Vector3.right);
        float dW = GetRayDist(origin, Vector3.left);

        float zLen = dN + dS;
        float xLen = dE + dW;

        Vector3 finalPos = transform.position;
        float y = transform.position.y;
        float margin = _agent.radius + 2.0f;

        if (zLen > xLen) // Axe NORD-SUD
        {
            float centerX = ((origin.x + dE) + (origin.x - dW)) / 2f;
            finalPos = new Vector3(centerX, y, transform.position.z);

            _patrolPointA = new Vector3(centerX, y, origin.z + dN - margin);
            _patrolPointB = new Vector3(centerX, y, origin.z - dS + margin);
            // On force l'orientation initiale
            transform.rotation = Quaternion.LookRotation(Vector3.forward);
        }
        else // Axe EST-OUEST
        {
            float centerZ = ((origin.z + dN) + (origin.z - dS)) / 2f;
            finalPos = new Vector3(transform.position.x, y, centerZ);

            _patrolPointA = new Vector3(origin.x + dE - margin, y, centerZ);
            _patrolPointB = new Vector3(origin.x - dW + margin, y, centerZ);
            // On force l'orientation initiale
            transform.rotation = Quaternion.LookRotation(Vector3.right);
        }

        _agent.Warp(finalPos);
    }

    float GetRayDist(Vector3 origin, Vector3 dir)
    {
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxVisionDistance, obstacleLayer))
            return hit.distance;
        return maxVisionDistance;
    }

    // --- PATROUILLE ---
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

    // --- VISION & CHARGE ---
    void ScanForPlayer()
    {
        RaycastHit hit;
        Vector3 center = transform.position + (Vector3.up * (visionHeight / 2f));
        Vector3 halfExtents = new Vector3(_actualVisionWidth / 2f, visionHeight / 2f, 0.1f);

        // Si le joueur est dans le couloir devant nous
        if (Physics.BoxCast(center, halfExtents, transform.forward, out hit, transform.rotation, maxVisionDistance, playerLayer))
        {
            // Verif obstacle
            Vector3 eyes = transform.position + Vector3.up * (visionHeight * 0.8f);
            if (!Physics.Linecast(eyes, hit.collider.bounds.center, obstacleLayer))
            {
                StartCoroutine(ExecuteChargeSequence());
            }
        }
    }

    IEnumerator ExecuteChargeSequence()
    {
        _preparingCharge = true;
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;

        // MODIFICATION ICI : On ne tourne PAS vers le joueur.
        // Le Bull reste figé sur son axe (LookAt supprimé).

        yield return new WaitForSeconds(1.0f); // Animation de "grattage de sol"

        _preparingCharge = false;
        _isCharging = true;

        RaycastHit wallHit;
        // On charge STRICTEMENT tout droit (transform.forward)
        if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out wallHit, maxVisionDistance, obstacleLayer))
        {
            _agent.isStopped = false;
            _agent.speed = chargeSpeed;
            _agent.autoBraking = false;
            _agent.SetDestination(wallHit.point);
        }
        else
        {
            // Sécurité si pas de mur (ne devrait pas arriver dans le maze)
            _agent.isStopped = false;
            _agent.speed = chargeSpeed;
            _agent.SetDestination(transform.position + transform.forward * 50f);
        }
    }

    void CheckImpact()
    {
        // Distance d'impact un peu augmentée pour compenser la vitesse élevée
        if (!_agent.pathPending && _agent.remainingDistance < 3.5f)
        {
            StartCoroutine(StunSequence());
        }
    }

    IEnumerator StunSequence()
    {
        _isCharging = false;
        _isStunned = true;
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;

        // Recul important pour sortir de la zone de collision du mur
        _agent.Warp(transform.position - transform.forward * 4.0f);

        yield return new WaitForSeconds(stunDuration);

        // Reprise immédiate
        _isStunned = false;
        _agent.isStopped = false;
        _agent.speed = patrolSpeed;
        _agent.autoBraking = true;

        // On repart vers l'autre bout du couloir
        float distA = Vector3.Distance(transform.position, _patrolPointA);
        float distB = Vector3.Distance(transform.position, _patrolPointB);
        _currentTarget = (distA > distB) ? _patrolPointA : _patrolPointB;

        _agent.SetDestination(_currentTarget);
    }

    private void OnDrawGizmosSelected()
    {
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        float w = (_agent != null) ? _agent.radius * 2 * visionWidthRatio : 1f;

        Gizmos.color = Color.red;
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Vector3 center = Vector3.up * visionHeight / 2;
        Gizmos.DrawWireCube(center, new Vector3(w, visionHeight, 0));
        Gizmos.DrawWireCube(center + Vector3.forward * maxVisionDistance, new Vector3(w, visionHeight, 0));
        Gizmos.matrix = old;
    }
}