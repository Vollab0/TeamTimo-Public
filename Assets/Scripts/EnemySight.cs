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

    private NavMeshAgent _agent;
    private Transform _player;
    private bool _isChasing = false;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = patrolSpeed;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _player = p.transform;
        
        StartCoroutine(PatrolRoutine());
    }

    void Update()
    {
        if (_player == null) return;

        if (CanSeePlayer())
        {
            if (!_isChasing) StartChase();
            _agent.SetDestination(_player.position);
        }
        else if (_isChasing)
        {
            StopChase();
        }
    }

    bool CanSeePlayer()
    {
        Vector3 dirToPlayer = (_player.position - transform.position).normalized;
        float dstToPlayer = Vector3.Distance(transform.position, _player.position);

        if (dstToPlayer > viewRadius) return false;

        if (Vector3.Angle(transform.forward, dirToPlayer) < viewAngle / 2)
        {
            // Raycast qui ignore le joueur dans le masque d'obstacle
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up, dirToPlayer, out hit, dstToPlayer, obstacleMask))
            {
                 // Si on touche le joueur (nécessite que le joueur soit dans un layer inclus dans obstacleMask ou traité à part)
                 // Méthode simplifiée si Layers bien configurés (Player layer exclu de obstacleMask):
                 return false; 
            }
            return true;
        }
        return false;
    }

    void StartChase()
    {
        _isChasing = true;
        _agent.speed = chaseSpeed;
        StopCoroutine("PatrolRoutine"); 
    }

    void StopChase()
    {
        _isChasing = false;
        _agent.speed = patrolSpeed;
        StartCoroutine(PatrolRoutine()); 
    }

    IEnumerator PatrolRoutine()
    {
        while (!_isChasing)
        {
            // On cherche un point valide
            Vector3 targetPoint = GetRandomPointOnNavMesh();
            
            // Si on n'a rien trouvé (ex: coincé), on attend un peu et on réessaie
            if (targetPoint == Vector3.zero) 
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            _agent.SetDestination(targetPoint);

            while (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance)
            {
                if (_isChasing) yield break; 
                yield return null;
            }

            yield return new WaitForSeconds(waitTimeAtPoint);
        }
    }

    // --- C'EST ICI QUE LA MAGIE DES ZONES OPERE ---
    Vector3 GetRandomPointOnNavMesh()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRange;
        randomDirection += transform.position;
        NavMeshHit hit;
        
        // CORRECTION MAJEURE :
        // Au lieu d'utiliser "1" ou "AllAreas", on utilise "_agent.areaMask".
        // Cela force le script à ne choisir QUE des points dans les zones cochées dans l'inspecteur.
        if (NavMesh.SamplePosition(randomDirection, out hit, patrolRange, _agent.areaMask))
        {
            return hit.position;
        }
        
        return Vector3.zero; // Aucun point valide trouvé
    }

    // --- DEBUG VISUEL ---
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);
        Vector3 viewAngleA = DirFromAngle(-viewAngle / 2, false);
        Vector3 viewAngleB = DirFromAngle(viewAngle / 2, false);
        Gizmos.DrawLine(transform.position, transform.position + viewAngleA * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + viewAngleB * viewRadius);
    }

    Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal) angleInDegrees += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}