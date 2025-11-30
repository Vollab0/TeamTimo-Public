using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemySight : MonoBehaviour
{
    [Header("1. Paramètres de Vision")]
    public float viewRadius = 15f;
    [Range(0, 360)] public float viewAngle = 90f;
    
    [Header("2. Filtres")]
    public LayerMask playerMask;   
    public LayerMask obstacleMask; 

    [Header("3. Déplacement")]
    public float patrolSpeed = 3.5f;
    public float chaseSpeed = 6.0f;
    public float waitTimeAtPoint = 2f; 
    public float patrolRange = 30f;   
    
    [Header("4. Comportement IA")]
    public float reactionTime = 1.0f; 
    public float searchTurnSpeed = 2.0f; 

    private NavMeshAgent _agent;
    private Transform _player;
    
    // États
    private bool _isChasing = false;
    private bool _isAlerted = false;
    private bool _isSearching = false; 

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        
        // --- CONFIGURATION MOUVEMENT ---
        _agent.acceleration = 1000f; 
        _agent.autoBraking = true;
        _agent.speed = patrolSpeed;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _player = p.transform;
        
        StartCoroutine(PatrolRoutine());
    }

    void Update()
    {
        if (_player == null) return;

        bool canSee = CanSeePlayer();

        if (canSee)
        {
            if (_isSearching) 
            {
                StopCoroutine("SearchRoutine");
                _isSearching = false;
                StartChase();
            }

            if (_isChasing)
            {
                _agent.SetDestination(_player.position);
            }
            else if (!_isAlerted)
            {
                StartCoroutine(AlertRoutine());
            }
            else if (_isAlerted)
            {
                LookAtPlayer();
            }
        }
        else 
        {
            if (_isChasing)
            {
                StopChase(); 
            }
        }
    }

    // --- LOGIQUE DE VISION ---
    bool CanSeePlayer()
    {
        Vector3 dirToPlayer = (_player.position - transform.position).normalized;
        float dstToPlayer = Vector3.Distance(transform.position, _player.position);

        if (dstToPlayer > viewRadius) return false;

        if (Vector3.Angle(transform.forward, dirToPlayer) < viewAngle / 2)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up, dirToPlayer, out hit, dstToPlayer, obstacleMask))
            {
                 return false; 
            }
            return true;
        }
        return false;
    }

    void LookAtPlayer()
    {
        if (_player == null) return;
        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0; 
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 10f); 
    }

    // --- GESTION DES ÉTATS ---

    IEnumerator AlertRoutine()
    {
        _isAlerted = true;
        StopCoroutine("PatrolRoutine");
        StopCoroutine("SearchRoutine"); 
        _agent.isStopped = true; 

        yield return new WaitForSeconds(reactionTime);

        if (CanSeePlayer())
        {
            StartChase();
        }
        else
        {
            _isAlerted = false;
            _agent.isStopped = false;
            StartCoroutine(PatrolRoutine());
        }
    }

    void StartChase()
    {
        _isAlerted = false;
        _isSearching = false;
        _isChasing = true;
        
        _agent.isStopped = false;
        _agent.speed = chaseSpeed;
        
        StopCoroutine("PatrolRoutine");
        StopCoroutine("SearchRoutine");
        StopCoroutine("AlertRoutine");
    }

    void StopChase()
    {
        _isChasing = false;
        _agent.speed = patrolSpeed;
        StartCoroutine(SearchRoutine());
    }

    IEnumerator SearchRoutine()
    {
        _isSearching = true;
        _agent.isStopped = true; 

        yield return new WaitForSeconds(reactionTime);

        Quaternion startRotation = transform.rotation;
        Quaternion lookLeft = startRotation * Quaternion.Euler(0, -45, 0);
        yield return RotateToTarget(lookLeft);
        yield return new WaitForSeconds(0.5f); 

        Quaternion lookRight = startRotation * Quaternion.Euler(0, 45, 0);
        yield return RotateToTarget(lookRight);
        yield return new WaitForSeconds(0.5f); 

        yield return RotateToTarget(startRotation);
        yield return new WaitForSeconds(reactionTime);

        _isSearching = false;
        _agent.isStopped = false;
        StartCoroutine(PatrolRoutine());
    }

    IEnumerator RotateToTarget(Quaternion targetRot)
    {
        float t = 0;
        Quaternion startRot = transform.rotation;
        while (t < 1)
        {
            if (CanSeePlayer()) yield break; 

            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            t += Time.deltaTime * searchTurnSpeed;
            yield return null;
        }
        transform.rotation = targetRot;
    }

    IEnumerator PatrolRoutine()
    {
        _agent.isStopped = false;

        while (!_isChasing && !_isAlerted && !_isSearching)
        {
            Vector3 targetPoint = GetRandomPointOnNavMesh();
            
            // Si on ne trouve pas de point valide (Vector3.zero), on attend un peu et on réessaie
            if (targetPoint == Vector3.zero) 
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            _agent.SetDestination(targetPoint);

            while (!_isAlerted && !_isChasing && !_isSearching && 
                   (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance + 0.1f))
            {
                yield return null;
            }

            if (_isAlerted || _isChasing || _isSearching) yield break;

            yield return new WaitForSeconds(waitTimeAtPoint);
        }
    }

    // --- MODIFICATION ICI ---
    Vector3 GetRandomPointOnNavMesh()
    {
        // On tente 10 fois de trouver un point valide
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * patrolRange;
            randomDirection += transform.position;
            
            NavMeshHit hit;
            
            // On vérifie le NavMesh
            if (NavMesh.SamplePosition(randomDirection, out hit, patrolRange, _agent.areaMask))
            {
                // ON AJOUTE LE RAYCAST ICI
                // On vérifie qu'il n'y a pas de mur entre l'IA et le point
                Vector3 origin = transform.position + Vector3.up; 
                Vector3 target = hit.position + Vector3.up;
                Vector3 direction = target - origin;
                
                // Si le raycast NE touche PAS d'obstacle, c'est bon
                if (!Physics.Raycast(origin, direction, direction.magnitude, obstacleMask))
                {
                    return hit.position;
                }
            }
        }
        return Vector3.zero;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);
        Vector3 viewAngleA = DirFromAngle(-viewAngle / 2, false);
        Vector3 viewAngleB = DirFromAngle(viewAngle / 2, false);
        Gizmos.DrawLine(transform.position, transform.position + viewAngleA * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + viewAngleB * viewRadius);
        
        if(_isAlerted || _isSearching) 
        {
            Gizmos.color = _isSearching ? Color.blue : Color.red; 
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2, 0.5f);
        }
    }

    Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal) angleInDegrees += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}