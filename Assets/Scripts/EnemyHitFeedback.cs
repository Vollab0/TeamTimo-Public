using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Ressenti d'un coup reçu par un ennemi, joué sur toutes les machines (appelé par EnemyHealth) :
/// éclair rouge qui s'estompe (pas de clignotement), écrasement élastique du modèle, gel de l'animation,
/// petite fumée au point d'impact. À la mort : le modèle rétrécit dans un nuage de fumée.
/// </summary>
public class EnemyHitFeedback : MonoBehaviour
{
    [Header("Éclair de couleur")]
    [Tooltip("Renderers teintés au moment du coup ; vide = tous les maillages sous le modèle")]
    [SerializeField] private Renderer[] flashRenderers;

    [Tooltip("Teinte au moment du coup (multipliée par la couleur du matériau), qui revient à la normale")]
    [SerializeField] private Color flashColor = new Color(1f, 0.25f, 0.2f, 1f);

    [Tooltip("Durée (s) du retour à la couleur normale")]
    [Min(0.01f)]
    [SerializeField] private float flashDuration = 0.25f;

    [Header("Écrasement")]
    [Tooltip("Objet écrasé (le modèle, pas la racine réseau) ; vide = enfant « Model »")]
    [SerializeField] private Transform squashTarget;

    [Tooltip("Échelle relative au moment de l'impact (aplati et élargi), qui rebondit vers 1")]
    [SerializeField] private Vector3 squashScale = new Vector3(1.3f, 0.7f, 1.3f);

    [Tooltip("Durée (s) du rebond")]
    [Min(0.01f)]
    [SerializeField] private float squashDuration = 0.35f;

    [Tooltip("Fréquence du rebond (oscillations par seconde)")]
    [Min(0f)]
    [SerializeField] private float squashFrequency = 3.5f;

    [Header("Gel de l'animation")]
    [Tooltip("Durée (s) pendant laquelle l'animation de l'ennemi est figée à l'impact")]
    [Min(0f)]
    [SerializeField] private float hitStopDuration = 0.08f;

    [Tooltip("Animator du modèle ; vide = cherché dans les enfants")]
    [SerializeField] private Animator animator;

    [Header("Fumée")]
    [Tooltip("Fumée jouée au point d'impact (VFX de type LandSmoke)")]
    [SerializeField] private VisualEffect impactSmoke;

    [Tooltip("Événement envoyé à la fumée d'impact")]
    [SerializeField] private string impactSmokeEvent = "OnLand";

    [Tooltip("Nuage joué quand le corps disparaît ; vide = pas de nuage")]
    [SerializeField] private VisualEffect vanishSmoke;

    [Tooltip("Événement envoyé au nuage de disparition")]
    [SerializeField] private string vanishSmokeEvent = "OnLand";

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock[] _savedBlocks;
    private bool[] _hadBlock;
    private Color[] _baseColors;
    private int[] _colorIds;
    private MaterialPropertyBlock _workBlock;

    private Vector3 _baseScale;
    private float _flashTimer;
    private float _squashTime = -1f;
    private float _squashAmount = 1f;
    private float _hitStopTimer;
    private float _vanishTimer = -1f;
    private float _vanishDuration;
    private Vector3 _vanishStartScale;

    private void Awake()
    {
        if (squashTarget == null) squashTarget = transform.Find("Model");
        if (squashTarget == null) squashTarget = transform;
        _baseScale = squashTarget.localScale;
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (flashRenderers == null || flashRenderers.Length == 0)
        {
            // Seuls les maillages (pas les VFXRenderer des fumées).
            var found = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in squashTarget.GetComponentsInChildren<Renderer>(true))
            {
                if (r is SkinnedMeshRenderer || r is MeshRenderer) found.Add(r);
            }
            flashRenderers = found.ToArray();
        }

        int count = flashRenderers.Length;
        _savedBlocks = new MaterialPropertyBlock[count];
        _hadBlock = new bool[count];
        _baseColors = new Color[count];
        _colorIds = new int[count];
        for (int i = 0; i < count; i++) _savedBlocks[i] = new MaterialPropertyBlock();
        _workBlock = new MaterialPropertyBlock();
    }

    /// <summary>Coup reçu : éclair (si dégâts), écrasement, gel et fumée au point d'impact.</summary>
    public void PlayHit(Vector3 hitPoint, bool damaged)
    {
        if (damaged) StartFlash();

        _squashTime = 0f;
        // Une poussée sans dégâts écrase moins.
        _squashAmount = damaged ? 1f : 0.5f;

        if (hitStopDuration > 0f && animator != null)
        {
            animator.speed = 0f;
            _hitStopTimer = hitStopDuration;
        }

        if (impactSmoke != null)
        {
            impactSmoke.transform.position = hitPoint;
            impactSmoke.SendEvent(impactSmokeEvent);
        }
    }

    /// <summary>Disparition du corps : le modèle rétrécit jusqu'à 0 dans un nuage.</summary>
    public void PlayVanish(float duration)
    {
        _squashTime = -1f;
        _vanishDuration = Mathf.Max(0.01f, duration);
        _vanishTimer = 0f;
        _vanishStartScale = squashTarget.localScale;
        if (vanishSmoke != null) vanishSmoke.SendEvent(vanishSmokeEvent);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        if (_hitStopTimer > 0f)
        {
            _hitStopTimer -= Time.unscaledDeltaTime;
            if (_hitStopTimer <= 0f && animator != null) animator.speed = 1f;
        }

        if (_flashTimer > 0f)
        {
            _flashTimer -= deltaTime;
            if (_flashTimer <= 0f) EndFlash();
            else ApplyFlash(_flashTimer / flashDuration);
        }

        if (_vanishTimer >= 0f)
        {
            _vanishTimer += deltaTime;
            float t = Mathf.Clamp01(_vanishTimer / _vanishDuration);
            // Petit gonflement puis disparition.
            float scale = t < 0.25f ? Mathf.Lerp(1f, 1.15f, t / 0.25f) : Mathf.Lerp(1.15f, 0f, (t - 0.25f) / 0.75f);
            squashTarget.localScale = _vanishStartScale * scale;
            return;
        }

        if (_squashTime >= 0f)
        {
            _squashTime += deltaTime;
            float t = _squashTime / squashDuration;
            if (t >= 1f)
            {
                squashTarget.localScale = _baseScale;
                _squashTime = -1f;
            }
            else
            {
                // Oscillation amortie : aplati à l'impact, puis rebonds de plus en plus petits.
                float wave = Mathf.Cos(t * squashDuration * squashFrequency * 2f * Mathf.PI) * (1f - t) * (1f - t);
                Vector3 offset = (squashScale - Vector3.one) * (wave * _squashAmount);
                squashTarget.localScale = Vector3.Scale(_baseScale, Vector3.one + offset);
            }
        }
    }

    private void StartFlash()
    {
        // Déjà en cours : on repart de la teinte pleine sans écraser les blocs sauvegardés.
        if (_flashTimer <= 0f)
        {
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                Renderer r = flashRenderers[i];
                _colorIds[i] = -1;
                if (r == null) continue;

                // On garde le bloc existant (ex. teinte de nuit d'EnemyNightMode) pour le remettre après.
                _hadBlock[i] = r.HasPropertyBlock();
                _savedBlocks[i].Clear();
                if (_hadBlock[i]) r.GetPropertyBlock(_savedBlocks[i]);

                Material mat = r.sharedMaterial;
                if (mat == null) continue;
                int id = mat.HasProperty(BaseColorId) ? BaseColorId : (mat.HasProperty(ColorId) ? ColorId : -1);
                _colorIds[i] = id;
                if (id == -1) continue;
                _baseColors[i] = _hadBlock[i] && _savedBlocks[i].HasColor(id) ? _savedBlocks[i].GetColor(id) : mat.GetColor(id);
            }
        }
        _flashTimer = flashDuration;
        ApplyFlash(1f);
    }

    private void ApplyFlash(float strength)
    {
        Color tint = Color.Lerp(Color.white, flashColor, strength * strength);
        for (int i = 0; i < flashRenderers.Length; i++)
        {
            Renderer r = flashRenderers[i];
            int id = _colorIds[i];
            if (r == null || id == -1) continue;
            r.GetPropertyBlock(_workBlock);
            _workBlock.SetColor(id, _baseColors[i] * tint);
            r.SetPropertyBlock(_workBlock);
        }
    }

    private void EndFlash()
    {
        _flashTimer = 0f;
        for (int i = 0; i < flashRenderers.Length; i++)
        {
            Renderer r = flashRenderers[i];
            if (r == null || _colorIds[i] == -1) continue;
            r.SetPropertyBlock(_hadBlock[i] ? _savedBlocks[i] : null);
        }
    }

    private void OnDisable()
    {
        if (_flashTimer > 0f) EndFlash();
        if (animator != null) animator.speed = 1f;
        if (squashTarget != null && _vanishTimer < 0f) squashTarget.localScale = _baseScale;
    }
}
