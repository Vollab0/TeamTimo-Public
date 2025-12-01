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

        if (Application.isPlaying)
        {
            _timeAccumulator += Time.deltaTime;
            currentCycle = (_timeAccumulator % dayDurationInSeconds) / dayDurationInSeconds;
            timeOfDay = currentCycle;
        }
        else
        {
            currentCycle = timeOfDay;
        }

        UpdateVisuals(currentCycle);
    }

    void UpdateVisuals(float cycle)
    {
        // 1. Shader (Ciel)
        if (targetMaterial != null)
            targetMaterial.SetFloat(_variableID, cycle);

        float baseAngle = (cycle * 360f) + rotationOffset;

        // 2. Lumière (Soleil)
        if (sunLight != null)
        {
            sunLight.transform.rotation = Quaternion.Euler(baseAngle + 90f, 0, 0);
            
            // On applique la couleur et l'intensité fixe
            sunLight.color = sunColor.Evaluate(cycle);
            sunLight.intensity = sunIntensity;

            ManageShadows(sunLight);
        }

        // 3. Lumière (Lune)
        if (moonLight != null)
        {
            moonLight.transform.rotation = Quaternion.Euler(baseAngle + 180f + 90f, 0, 0);
            
            // On applique la couleur et l'intensité fixe
            moonLight.color = moonColor.Evaluate(cycle);
            moonLight.intensity = moonIntensity;

            ManageShadows(moonLight);
        }

        // 4. Ambiance & Brouillard
        RenderSettings.ambientLight = ambientColor.Evaluate(cycle);
        RenderSettings.fogColor = fogColor.Evaluate(cycle);
        
        // MODIFICATION ICI : On applique la valeur fixe directement
        RenderSettings.fogDensity = fogDensity; 
    }

    void ManageShadows(Light lightSource)
    {
        // Calcul de la luminosité perçue
        float effectiveBrightness = lightSource.color.grayscale * lightSource.intensity;

        // CORRECTION : On baisse le seuil de 0.01f à 0.0001f.
        // Cela permet d'avoir des ombres même si la lune est très sombre (bleu nuit).
        // On ne coupe les ombres que si la lumière est VRAIMENT éteinte (Noir absolu).
        if (effectiveBrightness <= 0.0001f && lightSource.shadows != LightShadows.None)
        {
            lightSource.shadows = LightShadows.None;
        }
        else if (effectiveBrightness > 0.0001f && lightSource.shadows == LightShadows.None)
        {
            lightSource.shadows = LightShadows.Soft;
        }
    }
}