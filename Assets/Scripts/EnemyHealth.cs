using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Points de vie d'un ennemi et réaction aux coups du joueur (GDD Combat_Attaque).
/// Réseau (ADR 002) : les PV, le recul et la mort sont décidés chez l'hôte ; un client qui frappe
/// envoie son coup à l'hôte par RPC. Le ressenti (éclair, écrasement, fumée) est joué sur toutes
/// les machines par EnemyHitFeedback.
/// L'IA du mob s'abonne à OnDamaged (étourdissement, anim « Hit ») et à OnDied (anim de mort, IA coupée).
/// </summary>
public class EnemyHealth : NetworkBehaviour
{
    [Header("Points de vie")]
    [Tooltip("PV de départ. Dégâts des armes : Bâton 1, Pelle 2, Fourche 3")]
    [Min(1)]
    [SerializeField] private int maxHealth = 3;

    [Header("Recul")]
    [Tooltip("Durée (s) du recul quand l'ennemi est frappé ; la vitesse décroît jusqu'à 0")]
    [Min(0.01f)]
    [SerializeField] private float knockbackDuration = 0.25f;

    [Tooltip("Multiplicateur du recul reçu (0 = inamovible, ex. un gros mob ou un gardien)")]
    [Min(0f)]
    [SerializeField] private float knockbackMultiplier = 1f;

    [Header("Mort")]
    [Tooltip("Temps (s) pendant lequel le corps reste au sol avant de disparaître")]
    [Min(0f)]
    [SerializeField] private float corpseDuration = 2.5f;

    [Tooltip("Durée (s) de la disparition (rétrécissement dans un nuage de fumée)")]
    [Min(0f)]
    [SerializeField] private float vanishDuration = 0.4f;

    /// <summary>Coup reçu, chez l'hôte : dégâts (0 = poussée) et position de l'attaquant.</summary>
    public event Action<int, Vector3> OnDamaged;

    /// <summary>PV tombés à 0, chez l'hôte.</summary>
    public event Action OnDied;

    private readonly NetworkVariable<int> _netHealth = new NetworkVariable<int>(-1);

    private NavMeshAgent _agent;
    private EnemyHitFeedback _feedback;
    private Collider[] _colliders;

    private int _health;
    private bool _dead;
    private Vector3 _knockbackVelocity;
    private float _knockbackTimer;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => IsSpawned && _netHealth.Value >= 0 ? _netHealth.Value : _health;
    public bool IsDead => _dead || (IsSpawned && _netHealth.Value == 0);

    private bool IsAuthority => !IsSpawned || IsServer;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _feedback = GetComponent<EnemyHitFeedback>();
        _colliders = GetComponentsInChildren<Collider>();
        _health = maxHealth;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) _netHealth.Value = _health;
    }

    /// <summary>
    /// Frappe l'ennemi. Appelable par n'importe quelle machine : en réseau, le coup est transmis à l'hôte.
    /// </summary>
    /// <param name="damage">Dégâts (0 = poussée sans dégâts).</param>
    /// <param name="sourcePosition">Position de l'attaquant (direction du recul).</param>
    /// <param name="hitPoint">Point d'impact (fumée).</param>
    /// <param name="knockbackSpeed">Vitesse de recul (m/s) au moment du coup.</param>
    public void TakeHit(int damage, Vector3 sourcePosition, Vector3 hitPoint, float knockbackSpeed)
    {
        if (IsDead) return;
        if (IsAuthority) ApplyHit(damage, sourcePosition, hitPoint, knockbackSpeed);
        else TakeHitServerRpc(damage, sourcePosition, hitPoint, knockbackSpeed);
    }

    [Rpc(SendTo.Server)]
    private void TakeHitServerRpc(int damage, Vector3 sourcePosition, Vector3 hitPoint, float knockbackSpeed)
    {
        ApplyHit(damage, sourcePosition, hitPoint, knockbackSpeed);
    }

    private void ApplyHit(int damage, Vector3 sourcePosition, Vector3 hitPoint, float knockbackSpeed)
    {
        if (_dead) return;

        damage = Mathf.Max(0, damage);
        _health = Mathf.Max(0, _health - damage);
        if (IsSpawned) _netHealth.Value = _health;

        // Recul horizontal, de l'attaquant vers l'ennemi.
        Vector3 direction = transform.position - sourcePosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = -transform.forward;
        _knockbackVelocity = direction.normalized * (knockbackSpeed * knockbackMultiplier);
        _knockbackTimer = knockbackDuration;

        bool damaged = damage > 0;
        if (IsSpawned) PlayHitRpc(hitPoint, damaged);
        else PlayHit(hitPoint, damaged);

        OnDamaged?.Invoke(damage, sourcePosition);

        if (_health <= 0) Die();
    }

    private void Die()
    {
        _dead = true;
        OnDied?.Invoke();

        if (IsSpawned) PlayDeathRpc();
        else PlayDeath();

        StartCoroutine(CorpseRoutine());
    }

    private IEnumerator CorpseRoutine()
    {
        yield return new WaitForSeconds(corpseDuration);

        if (IsSpawned) PlayVanishRpc();
        else PlayVanish();

        yield return new WaitForSeconds(vanishDuration);

        if (IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }

    private void Update()
    {
        if (_knockbackTimer <= 0f || !IsAuthority) return;

        float deltaTime = Time.deltaTime;
        _knockbackTimer -= deltaTime;
        // La vitesse décroît linéairement jusqu'à 0 à la fin du recul.
        float ratio = Mathf.Clamp01(_knockbackTimer / knockbackDuration);
        Vector3 step = _knockbackVelocity * (ratio * deltaTime);

        // L'agent garde l'ennemi sur le NavMesh : il ne traverse pas les murs.
        if (_agent != null && _agent.enabled)
        {
            if (_agent.isOnNavMesh) _agent.Move(step);
            return;
        }

        // Corps (agent coupé à la mort) : il glisse, mais s'arrête au bord du NavMesh (murs).
        Vector3 from = transform.position;
        Vector3 to = from + step;
        if (NavMesh.Raycast(from, to, out NavMeshHit edge, NavMesh.AllAreas))
        {
            to = new Vector3(edge.position.x, from.y, edge.position.z);
            _knockbackTimer = 0f;
        }
        transform.position = to;
    }

    // --- Ressenti, joué sur toutes les machines ---
    [Rpc(SendTo.Everyone)]
    private void PlayHitRpc(Vector3 hitPoint, bool damaged) => PlayHit(hitPoint, damaged);

    [Rpc(SendTo.Everyone)]
    private void PlayDeathRpc() => PlayDeath();

    [Rpc(SendTo.Everyone)]
    private void PlayVanishRpc() => PlayVanish();

    private void PlayHit(Vector3 hitPoint, bool damaged)
    {
        if (_feedback != null) _feedback.PlayHit(hitPoint, damaged);
    }

    private void PlayDeath()
    {
        _dead = true;
        // Le corps ne bloque plus le joueur et ne reçoit plus de coups.
        foreach (Collider col in _colliders)
        {
            if (col != null) col.enabled = false;
        }
    }

    private void PlayVanish()
    {
        if (_feedback != null) _feedback.PlayVanish(vanishDuration);
    }
}
