using UnityEngine;

[ExecuteAlways] 
public class DayNightCycle : MonoBehaviour
{
    [Header("Réglages Généraux")]
    [Tooltip("Durée d'un cycle complet en secondes (Mode Play uniquement)")]
    public float dayDurationInSeconds = 60f;
    
    [Tooltip("L'heure actuelle (0 = Midi, 0.5 = Minuit).")]
    [Range(0, 1)]
    public float timeOfDay = 0f;

    [Tooltip("Décoché : le temps est figé sur « Time Of Day » (0 = plein jour). Le cycle sera repris plus tard (timer côté hôte, 70 % jour / 30 % nuit).")]
    [SerializeField] private bool cycleEnabled = true;

    [Header("Horloge de la partie (Play Mode)")]
    [Tooltip("Si assignée et lancée, c'est elle qui donne l'heure (jour 70 % / nuit 30 %, synchronisée en réseau) : « Day Duration », « Time Of Day » et « Cycle Enabled » sont alors ignorés.")]
    [SerializeField] private GameClock gameClock;

    [Tooltip("Valeur de « Time Of Day » qui correspond à l'aube (lever du soleil) dans les gradients")]
    [Range(0, 1)]
    [SerializeField] private float visualDawn = 0.75f;

    [Tooltip("Valeur de « Time Of Day » qui correspond au crépuscule (coucher du soleil) dans les gradients")]
    [Range(0, 1)]
    [SerializeField] private float visualDusk = 0.25f;

    [Header("Réglages Shader (Skybox)")]
    public Material targetMaterial;
    public string variableName = "_DayNightBlend";
    
    [Header("Réglages Lumière (Soleil)")]
    public Light sunLight;
    [Tooltip("Gérez le lever/coucher via ce Gradient (Noir = Lumière éteinte)")]
    public Gradient sunColor;
    [Tooltip("La puissance fixe du soleil (ex: 1.5).")]
    public float sunIntensity = 1.5f;
    public float rotationOffset = 0f;

    [Header("Réglages Lumière (Lune)")]
    public Light moonLight;
    [Tooltip("Gérez le lever/coucher via ce Gradient (Noir = Lumière éteinte)")]
    public Gradient moonColor;
    [Tooltip("La puissance fixe de la lune (ex: 0.5).")]
    public float moonIntensity = 0.5f;

    [Header("Réglages Ambiance")]
    public Gradient ambientColor; 

    [Header("Réglages Brouillard")]
    public Gradient fogColor;
    
    [Tooltip("Densité du brouillard fixe (Attention c'est sensible : 0.005 = léger, 0.05 = épais)")]
    public float fogDensity = 0.005f; // C'est maintenant une valeur fixe (float)

    // Variables privées
    private int _variableID;
    private float _timeAccumulator;
    // Dernière heure appliquée : on ne réécrit lumières, ciel et brouillard que si elle change
    // (évite de modifier la scène et MySkybox.mat à chaque frame quand le temps est figé).
    private float _lastAppliedCycle = float.NaN;

    void OnEnable()
    {
        // Réapplique l'heure à l'activation (et après un changement dans l'Inspector).
        _lastAppliedCycle = float.NaN;
    }

    void OnValidate()
    {
        _lastAppliedCycle = float.NaN;
        if (targetMaterial != null) _variableID = Shader.PropertyToID(variableName);
    }

    void Start()
    {
        if (targetMaterial != null)
            _variableID = Shader.PropertyToID(variableName);

        if (Application.isPlaying)
        {
            _timeAccumulator = timeOfDay * dayDurationInSeconds;
        }
    }

    void Update()
    {
        if (dayDurationInSeconds <= 0) return;

        float currentCycle = 0f;

        if (Application.isPlaying && gameClock != null && gameClock.HasStarted)
        {
            currentCycle = ClockToVisualCycle(gameClock.DayProgress, gameClock.DayRatio);
            timeOfDay = currentCycle;
        }
        else if (Application.isPlaying && cycleEnabled)
        {
            _timeAccumulator += Time.deltaTime;
            currentCycle = (_timeAccumulator % dayDurationInSeconds) / dayDurationInSeconds;
            timeOfDay = currentCycle;
        }
        else
        {
            currentCycle = timeOfDay;
        }

        if (currentCycle == _lastAppliedCycle) return;
        _lastAppliedCycle = currentCycle;
        UpdateVisuals(currentCycle);
    }

    /// <summary>
    /// Convertit l'avancement du jour de l'horloge (0 = aube, dayRatio = crépuscule) en heure visuelle
    /// (0 = midi, 0,5 = minuit) : le jour couvre l'arc aube → crépuscule, la nuit l'arc crépuscule → aube.
    /// </summary>
    float ClockToVisualCycle(float dayProgress, float dayRatio)
    {
        float dayArc = Mathf.Repeat(visualDusk - visualDawn, 1f);
        float nightArc = 1f - dayArc;

        if (dayProgress < dayRatio)
            return Mathf.Repeat(visualDawn + dayProgress / dayRatio * dayArc, 1f);

        return Mathf.Repeat(visualDusk + (dayProgress - dayRatio) / (1f - dayRatio) * nightArc, 1f);
    }

    void UpdateVisuals(float cycle)
    {
        // 1. Shader (Ciel)
        if (targetMaterial != null)
            targetMaterial.SetFloat(_variableID, cycle);

        float baseAngle = (cycle * 360f) + rotationOffset;

        // --- GESTION DU SOLEIL ---
        if (sunLight != null)
        {
            sunLight.transform.rotation = Quaternion.Euler(baseAngle + 90f, 0, 0);
            
            Color sColor = sunColor.Evaluate(cycle);
            sunLight.color = sColor;
            
            // CORRECTION IMPORTANTE : 
            // On multiplie l'intensité max par la luminosité de la couleur.
            // Si la couleur est noire (Nuit), l'intensité devient 0.
            sunLight.intensity = sunIntensity * sColor.maxColorComponent;

            ManageShadows(sunLight);
        }

        // --- GESTION DE LA LUNE ---
        if (moonLight != null)
        {
            moonLight.transform.rotation = Quaternion.Euler(baseAngle + 180f + 90f, 0, 0);
            
            Color mColor = moonColor.Evaluate(cycle);
            moonLight.color = mColor;
            
            // Pareil pour la lune : si le gradient lune est noir le jour, intensité = 0
            moonLight.intensity = moonIntensity * mColor.maxColorComponent;

            ManageShadows(moonLight);
        }

        // --- CHANGEMENT DE "MAIN LIGHT" ---
        // On dit à Unity quelle lumière est la plus importante pour les shaders
        if (sunLight != null && moonLight != null)
        {
            if (sunLight.intensity > moonLight.intensity)
            {
                if (RenderSettings.sun != sunLight) RenderSettings.sun = sunLight;
            }
            else
            {
                if (RenderSettings.sun != moonLight) RenderSettings.sun = moonLight;
            }
        }

        // 4. Ambiance & Brouillard
        RenderSettings.ambientLight = ambientColor.Evaluate(cycle);
        RenderSettings.fogColor = fogColor.Evaluate(cycle);
        RenderSettings.fogDensity = fogDensity; 
    }

    void ManageShadows(Light lightSource)
    {
        // CORRECTION MAJEURE :
        // Au lieu d'utiliser 'grayscale' (qui pénalise le bleu de la lune),
        // on utilise 'maxColorComponent'. Cela prend la valeur R, G ou B la plus forte.
        // Si vous avez un bleu pur (0, 0, 0.5), grayscale donnerait ~0.05 (trop bas),
        // alors que maxColorComponent donne 0.5 (correct).
        float effectiveBrightness = lightSource.color.maxColorComponent * lightSource.intensity;

        // Seuil très bas pour ne couper les ombres que si c'est vraiment noir (Lumière éteinte)
        float shadowThreshold = 0.001f;

        if (effectiveBrightness <= shadowThreshold && lightSource.shadows != LightShadows.None)
        {
            lightSource.shadows = LightShadows.None;
        }
        else if (effectiveBrightness > shadowThreshold && lightSource.shadows == LightShadows.None)
        {
            lightSource.shadows = LightShadows.Soft;
        }
    }
}