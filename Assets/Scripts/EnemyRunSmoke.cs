using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Traînée de fumée derrière un mob quand il court (Souris.md), sur le modèle du SprintSmoke du joueur :
/// le même VFX Graph reçoit les événements « StartSprint » / « StopSprint ».
/// La vitesse est déduite du déplacement du transform : le composant marche donc aussi chez les clients
/// (position répliquée par NetworkTransform), sans RPC.
/// </summary>
public class EnemyRunSmoke : MonoBehaviour
{
    [Header("Fumée de course")]
    [Tooltip("VFX de fumée (même graphe que le SprintSmoke du joueur). Si vide, cherché dans l'enfant « SprintSmoke »")]
    [SerializeField] private VisualEffect smoke;

    [Tooltip("Vitesse (m/s) au-dessus de laquelle le mob « court » et laisse sa traînée. Souris : patrouille 8, poursuite 22")]
    [Min(0f)]
    [SerializeField] private float runSpeedThreshold = 15f;

    [Tooltip("La traînée s'arrête quand la vitesse retombe sous le seuil × ce facteur (évite le clignotement)")]
    [Range(0.1f, 1f)]
    [SerializeField] private float stopThresholdRatio = 0.7f;

    [Tooltip("Lissage (s) de la vitesse mesurée (les positions répliquées des clients sont un peu saccadées)")]
    [Min(0f)]
    [SerializeField] private float speedSmoothTime = 0.15f;

    // Événements du VFX Graph SprintSmoke (cf. PlayerController).
    private static readonly int StartEventId = Shader.PropertyToID("StartSprint");
    private static readonly int StopEventId = Shader.PropertyToID("StopSprint");

    // Au-delà (m/s), le déplacement est une téléportation, ignorée.
    private const float TeleportSpeed = 150f;

    private Vector3 _lastPosition;
    private float _smoothedSpeed;
    private bool _smokeActive;

    private void Awake()
    {
        if (smoke == null)
        {
            Transform child = transform.Find("SprintSmoke");
            if (child != null) smoke = child.GetComponentInChildren<VisualEffect>();
        }
    }

    private void OnEnable()
    {
        _lastPosition = transform.position;
        _smoothedSpeed = 0f;
    }

    private void OnDisable()
    {
        SetSmoke(false);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

        Vector3 position = transform.position;
        Vector3 delta = position - _lastPosition;
        delta.y = 0f;
        _lastPosition = position;

        float speed = delta.magnitude / deltaTime;
        // Téléportation (apparition, replacement par le spawner) : pas une vraie course.
        if (speed > TeleportSpeed) speed = 0f;
        float t = speedSmoothTime > 0f ? Mathf.Clamp01(deltaTime / speedSmoothTime) : 1f;
        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, speed, t);

        if (!_smokeActive && _smoothedSpeed >= runSpeedThreshold) SetSmoke(true);
        else if (_smokeActive && _smoothedSpeed < runSpeedThreshold * stopThresholdRatio) SetSmoke(false);
    }

    private void SetSmoke(bool active)
    {
        if (active == _smokeActive) return;
        _smokeActive = active;
        if (smoke != null) smoke.SendEvent(active ? StartEventId : StopEventId);
    }
}
