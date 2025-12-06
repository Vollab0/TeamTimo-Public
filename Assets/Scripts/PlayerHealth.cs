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

    [Header("Physique (Knockback & Mort)")]
    public float knockbackForce = 15f;
    public float deathForceMultiplier = 1.5f; 
    
    // Références internes
    private RagdollController _ragdollController;
    private PlayerController _playerController;
    private VisualElement _bloodOverlayElement; // La référence vers l'UI

    // Events
    public event Action<int> OnSanteChangee;

    // États
    private int vieActuelle;
    private Coroutine _flashRoutine;
    private Color[] _originalColors;
    private Material[] _materials;
    private bool _isDead = false; 

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
        if (_isDead) return;

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
            _playerController.ApplyKnockback(sourcePosition, knockbackForce);
        }

        // 4. MORT
        if (vieActuelle <= 0)
        {
            Die(sourcePosition);
        }
    }

    // --- Méthodes Utilitaires ---

    public void Blesser(int degats)
    {
        Blesser(degats, transform.position + transform.forward);
    }

    public void Soigner(int soin)
    {
        if (_isDead) return;
        vieActuelle += soin;
        int maxVie = nombreDeCoeurs * 2;
        if (vieActuelle > maxVie) vieActuelle = maxVie;
        NotifierLeChangement();
    }

    private void NotifierLeChangement()
    {
        OnSanteChangee?.Invoke(vieActuelle);
    }

    private void Die(Vector3 fatalHitSource)
    {
        if (_isDead) return;
        _isDead = true;
        Debug.Log("💀 Mort du joueur");

        // Activation du Ragdoll
        if (_ragdollController != null)
        {
            Vector3 pushDirection = (transform.position - fatalHitSource).normalized;
            pushDirection += Vector3.up * 0.3f;
            
            Vector3 finalForce = pushDirection.normalized * knockbackForce * deathForceMultiplier;
            
            _ragdollController.EnableRagdoll(finalForce, transform.position);
        }
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