using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BullAI : MonoBehaviour
{
    [Header("1. Vision & Cibles")]
    [Tooltip("Layer du Joueur")]
    public LayerMask playerLayer;
    [Tooltip("Layer des Murs")]
    public LayerMask obstacleLayer;
    [Tooltip("Distance de vision max (doit être assez grande pour voir le bout du couloir)")]
    public float maxVisionDistance = 100f;
    [Tooltip("Largeur du rayon de vision (SphereCast) pour ne pas rater le joueur de peu")]
    public float visionWidth = 1.0f;
    [Tooltip("Hauteur de la zone de détection (depuis le sol)")]
    public float visionHeight = 2.0f; // Par défaut 2 mètres

    [Header("2. Patrouille")]
    public float patrolSpeed = 8f;
    [Tooltip("Chance (en %) de se retourner brutalement chaque seconde")]
    [Range(0, 100)] public float randomTurnChance = 10f;

    [Header("3. Charge")]
    public float chargeSpeed = 40f;     // Très rapide
    public float chargeAcceleration = 200f;
    public float preChargeDelay = 1.0f; // Temps de pause avant départ
    public float stunDuration = 2.0f;   // Temps de pause au mur

    // Composants
    private NavMeshAgent _agent;
    private Transform _player;

    // État interne
    private Vector3 _patrolPointA;
    private Vector3 _patrolPointB;
    private Vector3 _currentTarget;
    private bool _isCharging = false;
    private bool _isStunned = false;
    private bool _preparingCharge = false;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = patrolSpeed;

        // Configuration pour éviter que le taureau ne glisse
        _agent.autoBraking = true;

        DefineCorridor(); // Étape 1 : Trouver le couloir
        _currentTarget = _patrolPointA;
        _agent.SetDestination(_currentTarget);

        StartCoroutine(RandomTurnRoutine());
    }

    void Update()
    {
        // Si on est en train de charger, d'attendre ou d'être étourdi, on ne fait rien d'autre
        if (_preparingCharge || _isStunned) return;

        // Comportement de Charge
        if (_isCharging)
        {
            CheckImpact();
            return;
        }

        // Comportement de Patrouille
        PatrolLogic();

        // Pendant la patrouille, on surveille devant
        ScanForPlayer();
    }

    // --- LOGIQUE DE PATROUILLE ---

    void DefineCorridor()
    {
        // On lance des rayons dans les 4 directions pour trouver l'axe le plus long
        float distForward = GetDistanceInDir(transform.forward);
        float distBack = GetDistanceInDir(-transform.forward);
        float distRight = GetDistanceInDir(transform.right);
        float distLeft = GetDistanceInDir(-transform.right);

        float zAxisLength = distForward + distBack;
        float xAxisLength = distRight + distLeft;

        Vector3 pos = transform.position;

        // On choisit l'axe le plus long comme "Couloir"
        if (zAxisLength > xAxisLength)
        {
            _patrolPointA = pos + transform.forward * (distForward - 2f); // -2m pour ne pas coller au mur
            _patrolPointB = pos - transform.forward * (distBack - 2f);
        }
        else
        {
            _patrolPointA = pos + transform.right * (distRight - 2f);
            _patrolPointB = pos - transform.right * (distLeft - 2f);
        }
    }

    float GetDistanceInDir(Vector3 dir)
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up, dir, out hit, maxVisionDistance, obstacleLayer))
        {
            return hit.distance;
        }
        return maxVisionDistance;
    }

    void PatrolLogic()
    {
        // Si on est arrivé à destination
        if (!_agent.pathPending && _agent.remainingDistance < 2.0f)
        {
            // On inverse la cible
            _currentTarget = (_currentTarget == _patrolPointA) ? _patrolPointB : _patrolPointA;
            _agent.SetDestination(_currentTarget);
        }
    }

    // Routine pour le retournement brutal aléatoire
    IEnumerator RandomTurnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1.0f);
            if (!_isCharging && !_preparingCharge && !_isStunned)
            {
                if (Random.Range(0, 100) < randomTurnChance)
                {
                    // Brutal turn : on change immédiatement de cible
                    _currentTarget = (_currentTarget == _patrolPointA) ? _patrolPointB : _patrolPointA;
                    _agent.SetDestination(_currentTarget);
                }
            }
        }
    }

    // --- LOGIQUE DE COMBAT ---

    void ScanForPlayer()
    {
        RaycastHit hit;

        // Le centre de départ du tir (au milieu du corps en hauteur)
        Vector3 center = transform.position + (Vector3.up * (visionHeight / 2f));

        // La demi-taille de la boite (Unity demande les "Half Extents", soit la moitié des dimensions)
        // X = Largeur (visionWidth)
        // Y = Hauteur (visionHeight / 2)
        // Z = Épaisseur (0.1f, très fin car on projette cette face vers l'avant)
        Vector3 halfExtents = new Vector3(visionWidth, visionHeight / 2f, 0.1f);

        // Tir de la "Boite"
        // On utilise la rotation du Taureau (transform.rotation) pour que la boite tourne avec lui
        bool hasHit = Physics.BoxCast(center, halfExtents, transform.forward, out hit, transform.rotation, maxVisionDistance, playerLayer);

        if (hasHit)
        {
            // On lance la charge !
            StartCoroutine(ExecuteChargeSequence());
        }
    }

    IEnumerator ExecuteChargeSequence()
    {
        _preparingCharge = true;
        _agent.isStopped = true; // On s'arrête net
        _agent.velocity = Vector3.zero;

        // 1. Pré-charge (Le taureau souffle, gratte le sol...)
        // Debug.Log("BULL: Vu le joueur ! Préparation...");
        yield return new WaitForSeconds(preChargeDelay);

        // 2. Calcul du point d'impact (Le mur DERRIERE le joueur)
        _preparingCharge = false;
        _isCharging = true;

        RaycastHit wallHit;
        Vector3 chargeDir = transform.forward;
        Vector3 targetPos = transform.position + (chargeDir * maxVisionDistance);

        if (Physics.Raycast(transform.position + Vector3.up, chargeDir, out wallHit, maxVisionDistance, obstacleLayer))
        {
            targetPos = wallHit.point;
        }

        // Configuration NavMesh pour la charge
        _agent.isStopped = false;
        _agent.speed = chargeSpeed;
        _agent.acceleration = chargeAcceleration;
        _agent.autoBraking = false; // Important : il ne doit pas ralentir à l'arrivée
        _agent.SetDestination(targetPos);
    }

    void CheckImpact()
    {
        // On vérifie si on a tapé le mur (ou si on est bloqué)
        if (!_agent.pathPending && _agent.remainingDistance < 1.5f)
        {
            StartCoroutine(StunSequence());
        }
    }

    IEnumerator StunSequence()
    {
        _isCharging = false;
        _isStunned = true;

        // Arrêt brutal
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;

        // Debug.Log("BULL: BOUM ! Étourdi...");

        // TODO: Ici, vous pouvez ajouter un ScreenShake ou un son d'impact

        yield return new WaitForSeconds(stunDuration);

        // Reprise de la patrouille
        _isStunned = false;
        _agent.isStopped = false;
        _agent.speed = patrolSpeed;
        _agent.acceleration = 200f; // Remettre une accélération normale si besoin
        _agent.autoBraking = true;

        // On cherche le point de patrouille le plus proche pour repartir
        float distToA = Vector3.Distance(transform.position, _patrolPointA);
        float distToB = Vector3.Distance(transform.position, _patrolPointB);
        _currentTarget = (distToA < distToB) ? _patrolPointB : _patrolPointA; // On va vers l'opposé

        _agent.SetDestination(_currentTarget);
    }

    // Debug visuel
    void OnDrawGizmos()
    {
        if (_patrolPointA != Vector3.zero)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_patrolPointA, 1f);
            Gizmos.DrawWireSphere(_patrolPointB, 1f);
            Gizmos.DrawLine(_patrolPointA, _patrolPointB);
        }

        Gizmos.color = Color.red;
        // Visualisation du rayon de charge
        Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 5f);
    }

    // --- DEBUG VISUEL DANS L'ÉDITEUR ---

    private void OnDrawGizmosSelected()
    {
        // 1. VISUALISER LE CORPS (NavMeshAgent Radius)
        // On récupère l'agent même en mode édition
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f); // Vert transparent
            // Dessine une sphère représentant la zone de collision/déplacement
            Gizmos.DrawSphere(transform.position, agent.radius);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, agent.radius);
        }

        // 2. VISUALISER LA VISION (BOXCAST)
        Gizmos.color = Color.red;

        // On définit la matrice de rotation pour que les Gizmos suivent l'orientation du Taureau
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        // Centre local de la boite (relativement au pivot du taureau)
        Vector3 localCenter = Vector3.up * (visionHeight / 2f);

        // Taille totale de la boite
        Vector3 size = new Vector3(visionWidth * 2, visionHeight, 0); // Z est 0 pour dessiner juste le cadre de départ

        // Dessiner le cadre de départ (sur le nez du taureau)
        Gizmos.DrawWireCube(localCenter, size);

        // Dessiner le cadre d'arrivée (au bout de la vision)
        Vector3 localEndCenter = localCenter + (Vector3.forward * maxVisionDistance);
        Gizmos.DrawWireCube(localEndCenter, size);

        // Relier les coins pour former le tunnel
        // Coin Haut-Gauche
        Vector3 p1 = localCenter + new Vector3(-visionWidth, visionHeight / 2, 0);
        Vector3 p1End = p1 + Vector3.forward * maxVisionDistance;
        Gizmos.DrawLine(p1, p1End);

        // Coin Haut-Droite
        Vector3 p2 = localCenter + new Vector3(visionWidth, visionHeight / 2, 0);
        Vector3 p2End = p2 + Vector3.forward * maxVisionDistance;
        Gizmos.DrawLine(p2, p2End);

        // Coin Bas-Gauche
        Vector3 p3 = localCenter + new Vector3(-visionWidth, -visionHeight / 2, 0);
        Vector3 p3End = p3 + Vector3.forward * maxVisionDistance;
        Gizmos.DrawLine(p3, p3End);

        // Coin Bas-Droite
        Vector3 p4 = localCenter + new Vector3(visionWidth, -visionHeight / 2, 0);
        Vector3 p4End = p4 + Vector3.forward * maxVisionDistance;
        Gizmos.DrawLine(p4, p4End);

        // Rétablir la matrice d'origine pour ne pas casser les autres Gizmos
        Gizmos.matrix = oldMatrix;

        // 3. VISUALISER LES POINTS DE PATROUILLE (Si définis)
        if (_patrolPointA != Vector3.zero && _patrolPointB != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_patrolPointA, 0.5f);
            Gizmos.DrawWireSphere(_patrolPointB, 0.5f);
            Gizmos.DrawLine(_patrolPointA, _patrolPointB);
        }
    }
}