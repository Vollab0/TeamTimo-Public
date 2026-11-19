using UnityEngine;
using UnityEngine.UIElements; // Indispensable pour UI Toolkit
using System;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("Configuration Santé")]
    public int nombreDeCoeurs = 3;

    [Header("Feedback Visuel (Personnage 3D)")]
    public Renderer targetRenderer;
    public Color damageColor = Color.red;
    public float flashDuration = 0.1f;
    public int numberOfFlashes = 2;

    [Header("Feedback Visuel (UI Toolkit)")]
    [Tooltip("Glisse ici le GameObject qui a le composant UIDocument")]
    public UIDocument hudDocument; 
    
    [Tooltip("Le nom exact de ton élément dans UI Builder (ex: DamageOverlay)")]
    public string overlayName = "DamageOverlay";
    
    [Space(10)]
    [Tooltip("Vitesse de disparition de l'image (plus c'est haut, plus ça part vite)")]
    public float fadeSpeed = 0.5f; 
    
    [Tooltip("Opacité maximum quand on prend un coup (1 = image opaque, 0.5 = semi-transparente)")]
    [Range(0f, 1f)]
    public float maxOpacity = 1f;

    [Header("Physique (Knockback)")]
    public float knockbackForce = 15f;

    [Header("À terre et mort (GDD Santé et dégâts)")]
    [Tooltip("Durée de l'état « à terre » à 0 cœur (s) : le joueur rampe et appelle à l'aide ; un allié peut le relever")]
    [SerializeField] private float downedDuration = 30f;
    [Tooltip("Durée de l'animation de mort (s) avant que le corps passe en ragdoll (corps transportable)")]
    [SerializeField] private float deathAnimationDuration = 1.3f;
    [Tooltip("Vie rendue quand un allié relève le joueur (en demi-cœurs)")]
    [SerializeField] private int reviveHealth = 2;
    
    // Références internes
    private RagdollController _ragdollController;
    private PlayerController _playerController;
    private VisualElement _bloodOverlayElement; // La référence vers l'UI

    // Events
    public event Action<int> OnSanteChangee;
    /// <summary>Le joueur tombe à 0 cœur et passe « à terre ».</summary>
    public event Action OnDowned;
    /// <summary>Le délai « à terre » est écoulé : le joueur est mort.</summary>
    public event Action OnDied;
    /// <summary>Un allié a relevé le joueur « à terre ».</summary>
    public event Action OnRevived;

    // États
    private int vieActuelle;
    public int VieActuelle => vieActuelle;
    private Coroutine _flashRoutine;
    private Color[] _originalColors;
    private Material[] _materials;
    private bool _isDead = false; 
    private bool _isDowned = false;
    private Coroutine _downedRoutine;
    public bool IsDowned => _isDowned;
    public bool IsDead => _isDead;

    void Start()
    {
        vieActuelle = nombreDeCoeurs * 2;
        NotifierLeChangement();

        _playerController = GetComponent<PlayerController>();
        _ragdollController = GetComponent<RagdollController>();

        // --- CONFIGURATION UI TOOLKIT ---
        if (hudDocument != null)
        {
            // 1. On récupère la racine
            var root = hudDocument.rootVisualElement;

            // 2. On cherche l'élément par son nom
            _bloodOverlayElement = root.Q<VisualElement>(overlayName);

            if (_bloodOverlayElement != null)
            {
                // On s'assure qu'il est invisible au début
                _bloodOverlayElement.style.opacity = 0f;
                
                // IMPORTANT : On ignore les clics de souris sur l'image
                // pour pouvoir cliquer sur le jeu à travers le sang
                _bloodOverlayElement.pickingMode = PickingMode.Ignore; 
            }
            else
            {
                Debug.LogError($"❌ UI ERREUR : Impossible de trouver l'élément nommé '{overlayName}' dans l'UI Document !");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ UI WARNING : Le champ 'Hud Document' n'est pas assigné !");
        }

        // Setup du clignotement 3D (Renderer)
        if (targetRenderer != null)
        {
            _materials = targetRenderer.materials;
            _originalColors = new Color[_materials.Length];
            for (int i = 0; i < _materials.Length; i++)
            {
                if (_materials[i].HasProperty("_Color"))
                    _originalColors[i] = _materials[i].color;
            }
        }
    }

void Update()
    {
        // 1. ON GÈRE L'UI EN PREMIER (Même si on est mort)
        // --- GESTION DU FADE OUT (Disparition douce) ---
        if (_bloodOverlayElement != null)
        {
            float currentOpacity = _bloodOverlayElement.style.opacity.value;

            if (currentOpacity > 0)
            {
                // Note : J'ai ajouté Mathf.Max(0f, ...) pour être sûr de ne pas descendre sous 0
                float newOpacity = Mathf.Max(0f, currentOpacity - (fadeSpeed * Time.deltaTime));
                _bloodOverlayElement.style.opacity = new StyleFloat(newOpacity);
            }
        }

        // 2. MAINTENANT, SI ON EST MORT, ON ARRÊTE LÀ.
        // On empêche les inputs de fonctionner après la mort.
        if (_isDead) return;

        // Touches de Debug pour tester (ne fonctionnent que si vivant)
        if (Input.GetKeyDown(KeyCode.K)) Blesser(1, transform.position + transform.forward);
        if (Input.GetKeyDown(KeyCode.L)) Blesser(1, transform.position - transform.forward);
        if (Input.GetKeyDown(KeyCode.H)) Soigner(1);
    }

    public void Blesser(int degats, Vector3 sourcePosition)
    {
        Blesser(degats, sourcePosition, knockbackForce);
    }

    /// <summary>Dégâts avec une force de recul propre à l'attaque (ex. charge du taureau) au lieu de knockbackForce.</summary>
    public void Blesser(int degats, Vector3 sourcePosition, float forceRecul)
    {
        if (_isDead || _isDowned) return;
        // Frames d'invincibilité de la roulade
        if (_playerController != null && _playerController.IsInvulnerable) return;

        vieActuelle -= degats;
        if (vieActuelle < 0) vieActuelle = 0;

        NotifierLeChangement();

        // 1. EFFET SANG (UI Toolkit)
        if (_bloodOverlayElement != null)
        {
            // On met l'opacité au niveau max défini
            _bloodOverlayElement.style.opacity = new StyleFloat(maxOpacity);
        }

        // 2. EFFET CLIGNOTEMENT (3D)
        if (targetRenderer != null && gameObject.activeInHierarchy)
        {
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(DamageFlashRoutine());
        }

        // 3. PHYSIQUE (Knockback)
        if (_playerController != null && vieActuelle > 0)
        {
            _playerController.ApplyKnockback(sourcePosition, forceRecul);
        }

        // 4. À TERRE (puis mort au bout de downedDuration)
        if (vieActuelle <= 0)
        {
            EnterDowned();
        }
    }

    // --- Méthodes Utilitaires ---

    public void Blesser(int degats)
    {
        Blesser(degats, transform.position + transform.forward);
    }

    public void Soigner(int soin)
    {
        if (_isDead || _isDowned) return;
        vieActuelle += soin;
        int maxVie = nombreDeCoeurs * 2;
        if (vieActuelle > maxVie) vieActuelle = maxVie;
        NotifierLeChangement();
    }

    private void NotifierLeChangement()
    {
        OnSanteChangee?.Invoke(vieActuelle);
    }

    private void EnterDowned()
    {
        if (_isDowned || _isDead) return;
        _isDowned = true;
        Debug.Log("Joueur à terre");
        if (_playerController != null) _playerController.SetDowned(true);
        OnDowned?.Invoke();
        _downedRoutine = StartCoroutine(DownedRoutine());
    }

    private IEnumerator DownedRoutine()
    {
        yield return new WaitForSeconds(downedDuration);
        _downedRoutine = null;
        Die();
    }

    /// <summary>Relève le joueur « à terre » (Kit de réanimation d'un allié). Sans effet s'il n'est pas à terre.</summary>
    public void Reanimer()
    {
        if (!_isDowned || _isDead) return;
        if (_downedRoutine != null) StopCoroutine(_downedRoutine);
        _downedRoutine = null;
        _isDowned = false;
        vieActuelle = Mathf.Clamp(reviveHealth, 1, nombreDeCoeurs * 2);
        NotifierLeChangement();
        if (_playerController != null) _playerController.SetDowned(false);
        OnRevived?.Invoke();
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;
        _isDowned = false;
        Debug.Log("💀 Mort du joueur");
        OnDied?.Invoke();
        StartCoroutine(RagdollAfterDeathAnimation());
    }

    private IEnumerator RagdollAfterDeathAnimation()
    {
        // L'animation de mort couche le renard, puis le corps devient physique (transportable par les alliés)
        yield return new WaitForSeconds(deathAnimationDuration);
        if (_ragdollController != null) _ragdollController.EnableRagdoll(Vector3.zero, transform.position);
    }

    private IEnumerator DamageFlashRoutine()
    {
        for (int n = 0; n < numberOfFlashes; n++)
        {
            for (int i = 0; i < _materials.Length; i++)
            {
                if (_materials[i].HasProperty("_Color")) _materials[i].color = damageColor;
            }
            yield return new WaitForSeconds(flashDuration);
            for (int i = 0; i < _materials.Length; i++)
            {
                if (_materials[i].HasProperty("_Color")) _materials[i].color = _originalColors[i];
            }
            yield return new WaitForSeconds(flashDuration);
        }
        _flashRoutine = null;
    }
}