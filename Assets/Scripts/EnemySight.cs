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
    public float reactionTime = 1.0f; // Temps de pause pour Alerte ET Recherche
    public float searchTurnSpeed = 2.0f; // Vitesse de rotation de la tête lors de la recherche

    private NavMeshAgent _agent;
    private Transform _player;
    
    // États
    private bool _isChasing = false;
    private bool _isAlerted = false;
    private bool _isSearching = false; // Nouvel état pour savoir si on cherche le joueur

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
            // Si on voit le joueur, on arrête toute recherche ou patrouille et on attaque
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
            // Si on perd le joueur DE VUE alors qu'on chassait
            if (_isChasing)
            {
                StopChase(); // Ceci va maintenant lancer la recherche
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
        StopCoroutine("SearchRoutine"); // Sécurité
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
        
        // AU LIEU DE REPARTIR EN PATROUILLE DIRECTEMENT :
        StartCoroutine(SearchRoutine());
    }

    // --- NOUVELLE ROUTINE DE RECHERCHE ---
    IEnumerator SearchRoutine()
    {
        _isSearching = true;
        _agent.isStopped = true; // On s'arrête sur place

        // 1. Attente initiale (Reaction Time)
        yield return new WaitForSeconds(reactionTime);

        // Sauvegarde de la rotation où l'ennemi a perdu le joueur
        Quaternion startRotation = transform.rotation;

        // 2. Regarder à GAUCHE (-45 degrés)
        Quaternion lookLeft = startRotation * Quaternion.Euler(0, -45, 0);
        yield return RotateToTarget(lookLeft);
        yield return new WaitForSeconds(0.5f); // Petite pause regard gauche

        // 3. Regarder à DROITE (+45 degrés par rapport au centre)
        Quaternion lookRight = startRotation * Quaternion.Euler(0, 45, 0);
        yield return RotateToTarget(lookRight);
        yield return new WaitForSeconds(0.5f); // Petite pause regard droite

        // 4. Regarder DEVANT (Retour au centre)
        yield return RotateToTarget(startRotation);

        // 5. Attente finale (Reaction Time)
        yield return new WaitForSeconds(reactionTime);

        // Fin de la recherche -> Retour Patrouille
        _isSearching = false;
        _agent.isStopped = false;
        StartCoroutine(PatrolRoutine());
    }

    // Petite fonction utilitaire pour tourner la tête proprement
    IEnumerator RotateToTarget(Quaternion targetRot)
    {
        float t = 0;
        Quaternion startRot = transform.rotation;
        while (t < 1)
        {
            // Si le joueur réapparaît pendant qu'on tourne la tête, on coupe court
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

        // Tant qu'on ne chasse pas, qu'on n'est pas alerté et qu'on ne cherche pas
        while (!_isChasing && !_isAlerted && !_isSearching)
        {
            Vector3 targetPoint = GetRandomPointOnNavMesh();
            
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

    Vector3 GetRandomPointOnNavMesh()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRange;
        randomDirection += transform.position;
        NavMeshHit hit;
        
        if (NavMesh.SamplePosition(randomDirection, out hit, patrolRange, _agent.areaMask))
        {
            return hit.position;
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
            Gizmos.color = _isSearching ? Color.blue : Color.red; // Bleu si recherche, Rouge si alerté
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2, 0.5f);
        }
    }

    Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal) angleInDegrees += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}