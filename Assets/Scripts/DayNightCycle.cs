using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    [Header("Réglages Généraux")]
    [Tooltip("Durée d'un cycle complet en secondes")]
    public float dayDurationInSeconds = 60f;
    [Tooltip("L'heure de départ (0 = Midi, 0.5 = Minuit)")]
    [Range(0, 1)]
    public float startTime = 0f;

    [Header("Réglages Shader (Skybox)")]
    public Material targetMaterial;
    public string variableName = "_DayNightBlend";
    
    [Header("Réglages Lumière (Soleil)")]
    [Tooltip("Glisse ta Directional Light ici")]
    public Light sunLight;
    
    [Tooltip("Définit la couleur de la lumière selon l'heure (Blanc à midi, Rouge au coucher, Noir la nuit)")]
    public Gradient sunColor;
    
    [Tooltip("Définit la force de la lumière (Fort à midi, 0 la nuit)")]
    public AnimationCurve sunIntensity;

    [Tooltip("Permet de caler la rotation de la lumière avec celle du Shader")]
    public float rotationOffset = 0f;

    // Variables privées
    private int _variableID;
    private float _timeAccumulator;

    void Start()
    {
        // Initialisation Shader
        if (targetMaterial != null)
            _variableID = Shader.PropertyToID(variableName);
        else
            Debug.LogError("Pas de Matériau assigné !");

        // Initialisation Temps
        // On initialise l'accumulateur pour commencer à l'heure voulue
        _timeAccumulator = startTime * dayDurationInSeconds;
    }

    void Update()
    {
        if (dayDurationInSeconds <= 0) return;

        // 1. Calcul du temps
        _timeAccumulator += Time.deltaTime;
        // Le modulo (%) permet de boucler indéfiniment
        float currentCycle = (_timeAccumulator % dayDurationInSeconds) / dayDurationInSeconds;

        // 2. Mise à jour du Shader (Le Ciel)
        if (targetMaterial != null)
        {
            targetMaterial.SetFloat(_variableID, currentCycle);
        }

        // 3. Mise à jour de la Lumière (Le Soleil Physique)
        if (sunLight != null)
        {
            // A. Rotation
            // On fait tourner la lumière sur l'axe X (360 degrés * le cycle)
            // L'offset permet de synchroniser avec ton shader si besoin
            float angle = (currentCycle * 360f) + rotationOffset;
            
            // Note : 90 degrés est souvent le zénith (Midi) dans Unity par défaut pour une lumière
            sunLight.transform.rotation = Quaternion.Euler(angle + 90f, 0, 0);

            // B. Couleur
            sunLight.color = sunColor.Evaluate(currentCycle);

            // C. Intensité
            sunLight.intensity = sunIntensity.Evaluate(currentCycle);
            
            // D. Gestion des ombres (Optimisation)
            // Si l'intensité est quasi nulle, on coupe les ombres pour gagner des FPS
            if (sunLight.intensity <= 0.01f && sunLight.shadows != LightShadows.None)
                sunLight.shadows = LightShadows.None;
            else if (sunLight.intensity > 0.01f && sunLight.shadows == LightShadows.None)
                sunLight.shadows = LightShadows.Soft;
        }
    }
}