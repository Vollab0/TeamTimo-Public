using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Cycle jour / nuit visuel : course du soleil et de la lune, lumières, ambiance, ciel stylisé
/// (shader TeamTimo/StylizedSky), brouillard atmosphérique et rayons (Hidden/TeamTimo/AtmosphericFog),
/// poids des volumes de post-process. Les couleurs viennent d'un <see cref="DayNightLightingProfile"/>.
/// Le ciel et le brouillard sont pilotés par des variables globales de shader : aucun matériau n'est modifié.
/// </summary>
[ExecuteAlways]
public class DayNightCycle : MonoBehaviour
{
    [Header("Réglages généraux")]
    [Tooltip("Durée d'un cycle complet en secondes (Play Mode, sans horloge de partie)")]
    [SerializeField] private float dayDurationInSeconds = 60f;

    [Tooltip("L'heure actuelle : 0 = midi, 0,25 = coucher, 0,5 = minuit, 0,75 = lever.")]
    [Range(0, 1)]
    [SerializeField] private float timeOfDay = 0f;

    [Tooltip("Décoché : le temps est figé sur « Time Of Day » (sans horloge de partie).")]
    [SerializeField] private bool cycleEnabled = true;

    [Header("Horloge de la partie (Play Mode)")]
    [Tooltip("Si assignée et lancée, c'est elle qui donne l'heure (jour 70 % / nuit 30 %, synchronisée en réseau) : « Day Duration », « Time Of Day » et « Cycle Enabled » sont alors ignorés.")]
    [SerializeField] private GameClock gameClock;

    [Tooltip("Valeur de « Time Of Day » qui correspond à l'aube (lever du soleil)")]
    [Range(0, 1)]
    [SerializeField] private float visualDawn = 0.75f;

    [Tooltip("Valeur de « Time Of Day » qui correspond au crépuscule (coucher du soleil)")]
    [Range(0, 1)]
    [SerializeField] private float visualDusk = 0.25f;

    [Header("Profil d'éclairage")]
    [Tooltip("Couleurs et intensités du ciel, des lumières, du brouillard et du post-process selon l'heure")]
    [SerializeField] private DayNightLightingProfile profile;

    [Header("Course du soleil et de la lune (comme dans la réalité)")]
    [Tooltip("Direction du nord dans la scène, en degrés (rotation Y). 0 = +Z, comme les portes du Lobby (+Z = Nord, +X = Est). Le soleil se lève à l'est, culmine au sud à midi et se couche à l'ouest.")]
    [SerializeField] private float northAzimuth = 0f;
    [Tooltip("Latitude du lieu (degrés). Hauteur du soleil à midi = 90 − latitude + déclinaison. 55 → soleil à 35° à midi à l'équinoxe.")]
    [Range(0, 89)]
    [SerializeField] private float latitude = 55f;
    [Tooltip("Déclinaison du soleil (degrés) = saison. 0 = équinoxe : lever plein est, coucher plein ouest. + = été (plus haut, lever au nord-est), − = hiver.")]
    [Range(-23.5f, 23.5f)]
    [SerializeField] private float sunDeclination = 0f;
    [Tooltip("Déclinaison de la lune (degrés). Hauteur de la lune à minuit = 90 − latitude + déclinaison.")]
    [Range(-28.5f, 28.5f)]
    [SerializeField] private float moonDeclination = -15f;

    [Header("Lumières")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Light moonLight;

    [Header("Post-process")]
    [Tooltip("Volume global appliqué au lever / coucher (son poids suit le profil)")]
    [SerializeField] private Volume goldenHourVolume;
    [Tooltip("Volume global appliqué la nuit (son poids suit le profil)")]
    [SerializeField] private Volume nightVolume;

    /// <summary>Hauteur du soleil normalisée : 0 = minuit, 0,5 = horizon, 1 = midi.</summary>
    public float SunHeight01 { get; private set; } = 1f;
    /// <summary>Valeurs d'éclairage actuellement appliquées.</summary>
    public DayNightLightingState CurrentState { get; private set; }
    public float TimeOfDay => timeOfDay;

    /// <summary>Appelé à chaque changement d'éclairage (hauteur du soleil 0-1).</summary>
    public event Action<float> OnLightingUpdated;

    private float _timeAccumulator;
    // On ne réécrit l'éclairage que si l'heure change (la scène ne repasse pas « modifiée » toute seule).
    private float _lastAppliedCycle = float.NaN;

    static readonly int SunDirId = Shader.PropertyToID("_TT_SunDir");
    static readonly int MoonDirId = Shader.PropertyToID("_TT_MoonDir");
    static readonly int SkyZenithId = Shader.PropertyToID("_TT_SkyZenith");
    static readonly int SkyHorizonId = Shader.PropertyToID("_TT_SkyHorizon");
    static readonly int SkyGroundId = Shader.PropertyToID("_TT_SkyGround");
    static readonly int SunGlowColorId = Shader.PropertyToID("_TT_SunGlowColor");
    static readonly int SunDiscColorId = Shader.PropertyToID("_TT_SunDiscColor");
    static readonly int MoonColorId = Shader.PropertyToID("_TT_MoonColor");
    static readonly int CloudLitId = Shader.PropertyToID("_TT_CloudLit");
    static readonly int CloudShadowId = Shader.PropertyToID("_TT_CloudShadow");
    static readonly int FogColorId = Shader.PropertyToID("_TT_FogColor");
    static readonly int SunGlowId = Shader.PropertyToID("_TT_SunGlow");
    static readonly int SunVisibilityId = Shader.PropertyToID("_TT_SunVisibility");
    static readonly int MoonVisibilityId = Shader.PropertyToID("_TT_MoonVisibility");
    static readonly int StarsVisibilityId = Shader.PropertyToID("_TT_StarsVisibility");
    static readonly int CloudLightFromMoonId = Shader.PropertyToID("_TT_CloudLightFromMoon");
    static readonly int FogDensityId = Shader.PropertyToID("_TT_FogDensity");
    static readonly int LightDirId = Shader.PropertyToID("_TT_LightDir");
    static readonly int ScatterColorId = Shader.PropertyToID("_TT_ScatterColor");
    static readonly int ScatterIntensityId = Shader.PropertyToID("_TT_ScatterIntensity");
    static readonly int GodRayColorId = Shader.PropertyToID("_TT_GodRayColor");
    static readonly int GodRayIntensityId = Shader.PropertyToID("_TT_GodRayIntensity");

    void OnEnable()
    {
        _lastAppliedCycle = float.NaN;
    }

    void OnValidate()
    {
        _lastAppliedCycle = float.NaN;
    }

    void Start()
    {
        if (Application.isPlaying)
            _timeAccumulator = timeOfDay * dayDurationInSeconds;
    }

    void Update()
    {
        float currentCycle;

        if (Application.isPlaying && gameClock != null && gameClock.HasStarted)
        {
            currentCycle = ClockToVisualCycle(gameClock.DayProgress, gameClock.DayRatio);
            timeOfDay = currentCycle;
        }
        else if (Application.isPlaying && cycleEnabled && dayDurationInSeconds > 0f)
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
        // Angle horaire : 0 = midi, 90° = coucher (ouest), 180° = minuit, 270° = lever (est).
        float hourAngle = cycle * 360f;
        Vector3 sunDir = CelestialDirection(hourAngle, sunDeclination);
        // La lune est à l'opposé du soleil (pleine lune) : elle se lève à l'est le soir, culmine au sud à minuit.
        Vector3 moonDir = CelestialDirection(hourAngle + 180f, moonDeclination);
        float sunElevation = Mathf.Asin(Mathf.Clamp(sunDir.y, -1f, 1f)) * Mathf.Rad2Deg;
        float moonElevation = Mathf.Asin(Mathf.Clamp(moonDir.y, -1f, 1f)) * Mathf.Rad2Deg;
        // La lune n'éclaire que levée (elle se lève un peu après le coucher du soleil selon sa déclinaison).
        float moonUp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1f, 6f, moonElevation));

        // Hauteur normalisée : 0,5 + 0,5 × sin(hauteur) / sin(hauteur à midi). À l'équinoxe : 0,5 + 0,5 × cos(angle horaire).
        float noonSin = Mathf.Max(Mathf.Sin((90f - latitude + sunDeclination) * Mathf.Deg2Rad), 0.05f);
        SunHeight01 = Mathf.Clamp01(0.5f + 0.5f * sunDir.y / noonSin);
        if (profile == null)
        {
            Shader.SetGlobalVector(SunDirId, sunDir);
            Shader.SetGlobalVector(MoonDirId, moonDir);
            return;
        }

        DayNightLightingState s = profile.Evaluate(SunHeight01);
        CurrentState = s;

        // --- Lumières : une seule directionnelle active à la fois (une seule ombre à calculer) ---
        bool sunActive = ApplyLight(sunLight, sunDir, s.SunLightColor, s.SunLightIntensity);
        bool moonActive = ApplyLight(moonLight, moonDir, s.MoonColor, sunActive ? 0f : s.MoonLightIntensity * moonUp);
        Light main = sunActive ? sunLight : (moonActive ? moonLight : sunLight);
        if (main != null && RenderSettings.sun != main) RenderSettings.sun = main;

        // --- Ambiance (trois couleurs) ---
        if (RenderSettings.ambientMode != AmbientMode.Trilight) RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = s.AmbientSky;
        RenderSettings.ambientEquatorColor = s.AmbientEquator;
        RenderSettings.ambientGroundColor = s.AmbientGround;

        // --- Ciel ---
        bool night = SunHeight01 < 0.47f;
        Shader.SetGlobalVector(SunDirId, sunDir);
        Shader.SetGlobalVector(MoonDirId, moonDir);
        Shader.SetGlobalColor(SkyZenithId, s.SkyZenith);
        Shader.SetGlobalColor(SkyHorizonId, s.SkyHorizon);
        Shader.SetGlobalColor(SkyGroundId, s.SkyGround);
        Shader.SetGlobalColor(SunGlowColorId, s.SunGlowColor);
        Shader.SetGlobalColor(SunDiscColorId, s.SunDiscColor);
        Shader.SetGlobalColor(MoonColorId, s.MoonColor);
        Shader.SetGlobalColor(CloudLitId, s.CloudLit);
        Shader.SetGlobalColor(CloudShadowId, s.CloudShadow);
        Shader.SetGlobalFloat(SunGlowId, s.SunGlowIntensity);
        Shader.SetGlobalFloat(SunVisibilityId, Mathf.Clamp01(sunElevation / 2f + 1f));
        Shader.SetGlobalFloat(MoonVisibilityId, s.MoonVisibility);
        Shader.SetGlobalFloat(StarsVisibilityId, s.StarsVisibility);
        Shader.SetGlobalFloat(CloudLightFromMoonId, Mathf.InverseLerp(0.5f, 0.44f, SunHeight01));

        // --- Brouillard et rayons ---
        Shader.SetGlobalColor(FogColorId, s.FogColor);
        Shader.SetGlobalFloat(FogDensityId, s.FogDensity);
        Shader.SetGlobalVector(LightDirId, night ? moonDir : sunDir);
        Shader.SetGlobalColor(ScatterColorId, s.ScatterColor);
        Shader.SetGlobalFloat(ScatterIntensityId, s.ScatterIntensity * (night ? moonUp : 1f));
        Shader.SetGlobalColor(GodRayColorId, s.GodRayColor);
        Shader.SetGlobalFloat(GodRayIntensityId, s.GodRayIntensity * (night ? moonUp : 1f));

        // --- Post-process ---
        if (goldenHourVolume != null) goldenHourVolume.weight = s.GoldenHourWeight;
        if (nightVolume != null) nightVolume.weight = s.NightWeight;

        OnLightingUpdated?.Invoke(SunHeight01);
    }

    /// <summary>
    /// Direction vers un astre (repère monde) pour un angle horaire (0 = au sud, plus haut ; 90 = à l'ouest)
    /// et une déclinaison, à la latitude réglée. Formule de la sphère céleste : lever à l'est, coucher à l'ouest.
    /// </summary>
    Vector3 CelestialDirection(float hourAngleDeg, float declinationDeg)
    {
        float h = hourAngleDeg * Mathf.Deg2Rad;
        float d = declinationDeg * Mathf.Deg2Rad;
        float phi = latitude * Mathf.Deg2Rad;
        float east = -Mathf.Cos(d) * Mathf.Sin(h);
        float north = Mathf.Cos(phi) * Mathf.Sin(d) - Mathf.Sin(phi) * Mathf.Cos(d) * Mathf.Cos(h);
        float up = Mathf.Sin(phi) * Mathf.Sin(d) + Mathf.Cos(phi) * Mathf.Cos(d) * Mathf.Cos(h);
        // Repère local (x = est, z = nord) tourné selon la direction du nord dans la scène.
        return Quaternion.Euler(0f, northAzimuth, 0f) * new Vector3(east, up, north);
    }

    /// <summary>Oriente et règle une lumière ; l'éteint (et coupe son ombre) si son intensité est nulle.</summary>
    static bool ApplyLight(Light light, Vector3 towardLight, Color color, float intensity)
    {
        if (light == null) return false;
        bool on = intensity > 0.001f;
        if (light.enabled != on) light.enabled = on;
        if (!on) return false;

        light.transform.rotation = Quaternion.LookRotation(-towardLight);
        light.color = color;
        light.intensity = intensity;
        if (light.shadows == LightShadows.None) light.shadows = LightShadows.Soft;
        return true;
    }

    // ---------- Aperçus rapides (clic droit sur le composant) ----------
    [ContextMenu("Aperçu : midi")] void PreviewNoon() => SetPreview(0f);
    [ContextMenu("Aperçu : soir (soleil bas à l'ouest)")] void PreviewDusk() => SetPreview(0.18f);
    [ContextMenu("Aperçu : coucher (soleil sur l'horizon)")] void PreviewSunset() => SetPreview(0.24f);
    [ContextMenu("Aperçu : nuit")] void PreviewNight() => SetPreview(0.5f);
    [ContextMenu("Aperçu : matin (soleil bas à l'est)")] void PreviewMorning() => SetPreview(0.82f);

    void SetPreview(float cycle)
    {
        timeOfDay = cycle;
        _lastAppliedCycle = float.NaN;
        UpdateVisuals(cycle);
    }
}
