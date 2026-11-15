using UnityEngine;

/// <summary>
/// Fumée de la charge du taureau (Taureau.md) : un sillage de poussière en « V » derrière lui, sur toute la
/// longueur du couloir parcourue, qui dessine la zone où la charge blesse (le sillage de BullAI).
/// Les bouffées partent de l'avant du taureau et s'écartent vers les murs : étroit devant, large derrière.
/// Même style que la fumée de course du renard (VFX SprintSmoke) : des boules blanches éclairées qui gonflent
/// puis rétrécissent. C'est un ParticleSystem (et non le VFX Graph) car chaque bouffée doit recevoir sa propre
/// position et sa propre vitesse d'écartement.
/// Tourne sur chaque machine à partir de l'état répliqué de BullAI (IsCharging, dimensions du sillage) et du
/// déplacement du transform : aucun RPC.
/// </summary>
public class BullChargeSmoke : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("IA du taureau. Si vide, cherchée sur cet objet")]
    [SerializeField] private BullAI bull;

    [Tooltip("Système de particules de la fumée (espace de simulation : Monde). Si vide, cherché dans l'enfant « ChargeSmoke »")]
    [SerializeField] private ParticleSystem smoke;

    [Header("Sillage")]
    [Tooltip("Nombre de bouffées émises par mètre parcouru en charge")]
    [Min(0f)]
    [SerializeField] private float particlesPerMeter = 3f;

    [Tooltip("Temps (s) que met la fumée pour s'écarter de l'avant du taureau jusqu'aux bords du sillage. " +
             "À 100 m/s, 0,3 s ≈ 30 m de sillage (wakeLength de BullAI)")]
    [Min(0.05f)]
    [SerializeField] private float spreadDuration = 0.3f;

    [Tooltip("Durée de vie (s) d'une bouffée : min / max")]
    [SerializeField] private Vector2 lifetimeRange = new Vector2(0.7f, 1.1f);

    [Tooltip("Diamètre (m) maximal d'une bouffée : min / max (la courbe de taille du système la fait gonfler puis rétrécir)")]
    [SerializeField] private Vector2 sizeRange = new Vector2(1f, 2.2f);

    [Tooltip("Hauteur (m) au-dessus du sol où naît la fumée")]
    [SerializeField] private float heightOffset = 1f;

    [Tooltip("Vitesse (m/s) à laquelle la fumée s'élève au départ : min / max")]
    [SerializeField] private Vector2 riseSpeedRange = new Vector2(0f, 4f);

    [Tooltip("Couleur de la fumée (blanche comme celle du renard ; le matériau est éclairé, elle s'assombrit donc la nuit)")]
    [SerializeField] private Color smokeColor = Color.white;

    [Header("Choc en fin de charge")]
    [Tooltip("Nombre de bouffées projetées quand la charge s'arrête (mur, joueur percuté)")]
    [Min(0)]
    [SerializeField] private int impactBurstCount = 30;

    [Tooltip("Vitesse (m/s) maximale des bouffées du choc")]
    [Min(0f)]
    [SerializeField] private float impactBurstSpeed = 35f;

    // Au-delà (m) en une frame, le déplacement est une téléportation (apparition, replacement) : ignoré.
    private const float TeleportDistance = 40f;
    // La traînée freine comme exp(-k t) : avec k = 3 / spreadDuration, 95 % de l'écartement est fait en spreadDuration.
    private const float DragFactor = 3f;

    private Vector3 _lastPosition;
    private float _distanceBudget;
    private bool _wasCharging;
    private float _drag;

    private void Awake()
    {
        if (bull == null) bull = GetComponent<BullAI>();
        if (smoke == null)
        {
            Transform child = transform.Find("ChargeSmoke");
            if (child != null) smoke = child.GetComponent<ParticleSystem>();
        }

        _drag = DragFactor / spreadDuration;
        if (smoke != null)
        {
            // Le freinage est porté par le système (module Limit Velocity over Lifetime).
            ParticleSystem.LimitVelocityOverLifetimeModule limit = smoke.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = _drag;
            // Freinage identique pour toutes les bouffées : ni leur taille ni leur vitesse ne le modifient.
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }
    }

    private void OnEnable()
    {
        _lastPosition = transform.position;
        _distanceBudget = 0f;
        _wasCharging = false;
    }

    private void Update()
    {
        if (bull == null || smoke == null) return;

        Vector3 position = transform.position;
        bool charging = bull.IsCharging;

        if (charging)
        {
            Vector3 delta = position - _lastPosition;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance > 0f && distance < TeleportDistance) EmitWake(_lastPosition, position, distance);
        }
        else if (_wasCharging)
        {
            EmitImpact();
            _distanceBudget = 0f;
        }

        _wasCharging = charging;
        _lastPosition = position;
    }

    /// <summary>Bouffées réparties sur le tronçon parcouru pendant la frame, lancées vers les côtés.</summary>
    private void EmitWake(Vector3 from, Vector3 to, float distance)
    {
        _distanceBudget += distance * particlesPerMeter;
        int count = Mathf.FloorToInt(_distanceBudget);
        if (count <= 0) return;
        _distanceBudget -= count;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        float frontHalf = bull.WakeFrontWidth * 0.5f;
        float backHalf = Mathf.Max(frontHalf, bull.WakeBackWidth * 0.5f);
        Vector3 nose = forward * bull.FrontOffset + Vector3.up * heightOffset;

        ParticleSystem.EmitParams emit = default;
        for (int i = 0; i < count; i++)
        {
            // u dans [-1 ; 1] : place de la bouffée dans la largeur. Elle naît dans la pointe étroite
            // et finit à u × demi-largeur arrière : l'ensemble remplit le triangle du sillage.
            float u = Random.Range(-1f, 1f);
            float travel = u * (backHalf - frontHalf);

            emit.position = Vector3.Lerp(from, to, Random.value) + nose + right * (u * frontHalf);
            // Trajet total d'une particule freinée = vitesse initiale / freinage.
            emit.velocity = right * (travel * _drag) + Vector3.up * Random.Range(riseSpeedRange.x, riseSpeedRange.y);
            FillLook(ref emit);
            smoke.Emit(emit, 1);
        }
    }

    /// <summary>Nuage projeté sur toute la largeur du sillage quand la charge s'arrête.</summary>
    private void EmitImpact()
    {
        if (impactBurstCount <= 0) return;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 nose = transform.position + forward * bull.FrontOffset + Vector3.up * heightOffset;
        float frontHalf = bull.WakeFrontWidth * 0.5f;

        ParticleSystem.EmitParams emit = default;
        for (int i = 0; i < impactBurstCount; i++)
        {
            float u = Random.Range(-1f, 1f);
            emit.position = nose + right * (u * frontHalf);
            // Rebondit contre l'obstacle : vers les côtés, vers le haut et un peu vers l'arrière.
            emit.velocity = right * (u * impactBurstSpeed)
                            - forward * Random.Range(0f, impactBurstSpeed * 0.5f)
                            + Vector3.up * Random.Range(riseSpeedRange.y, riseSpeedRange.y * 3f);
            FillLook(ref emit);
            smoke.Emit(emit, 1);
        }
    }

    private void FillLook(ref ParticleSystem.EmitParams emit)
    {
        emit.startLifetime = Random.Range(lifetimeRange.x, lifetimeRange.y);
        emit.startSize = Random.Range(sizeRange.x, sizeRange.y);
        emit.startColor = smokeColor;
    }
}
