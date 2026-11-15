using UnityEngine;

/// <summary>
/// Toutes les couleurs et intensités du cycle jour / nuit (ciel, nuages, lumières, ambiance, brouillard,
/// rayons, post-process, particules), lues par <see cref="DayNightCycle"/>.
/// Axe horizontal de chaque dégradé / courbe = HAUTEUR DU SOLEIL : 0 = minuit (soleil au plus bas),
/// 0,5 = soleil sur l'horizon (lever ou coucher), 1 = midi. Le matin et le soir ont donc le même rendu.
/// </summary>
[CreateAssetMenu(fileName = "DayNightLighting", menuName = "TeamTimo/Profil d'éclairage jour-nuit")]
public class DayNightLightingProfile : ScriptableObject
{
    // Repères de l'axe, utilisés par les valeurs par défaut ci-dessous.
    const float Night = 0f, BlueHour = 0.44f, Horizon = 0.5f, Golden = 0.6f, LateGolden = 0.7f, Day = 0.84f, Noon = 1f;

    [Header("Ciel (0 = minuit, 0,5 = horizon, 1 = midi)")]
    [Tooltip("Couleur du haut du ciel")]
    [SerializeField] private Gradient skyZenith = G((Night, "0C1438"), (BlueHour, "283C82"), (Horizon, "3C64AF"), (Golden, "3A7CD0"), (LateGolden, "3A86D6"), (Day, "2F8CE0"), (Noon, "2C8AE2"));
    [Tooltip("Couleur du ciel à l'horizon")]
    [SerializeField] private Gradient skyHorizon = G((Night, "2E4580"), (BlueHour, "8C82AA"), (Horizon, "F5B07A"), (Golden, "FCD9A2"), (LateGolden, "F0E2C4"), (Day, "B4DCF0"), (Noon, "AED9F0"));
    [Tooltip("Couleur sous l'horizon (rarement visible, derrière les murs)")]
    [SerializeField] private Gradient skyGround = G((Night, "141E3C"), (Horizon, "6E5A5A"), (Day, "8CA0AA"));
    [Tooltip("Couleur du halo du soleil (lueur à l'horizon et autour du disque), aussi utilisée pour le liseré des nuages")]
    [SerializeField] private Gradient sunGlowColor = G((Night, "FF9A50"), (Horizon, "FF9248"), (Golden, "FFC878"), (LateGolden, "FFDCA0"), (Day, "FFF0D2"), (Noon, "FFF6E6"));
    [Tooltip("Force du halo du soleil dans le ciel")]
    [SerializeField] private AnimationCurve sunGlowIntensity = C((0.4f, 0f), (Horizon, 1.3f), (Golden, 1.15f), (LateGolden, 0.9f), (Day, 0.4f), (Noon, 0.35f));
    [Tooltip("Couleur (HDR) du disque du soleil")]
    [GradientUsage(true)]
    [SerializeField] private Gradient sunDiscColor = G((Night, "FF7A30"), (Horizon, "FF9A4A"), (Golden, "FFD08A"), (Day, "FFF6E0"), (Noon, "FFFAF0"));
    [Tooltip("Visibilité des étoiles")]
    [SerializeField] private AnimationCurve starsVisibility = C((Night, 1f), (0.42f, 0.85f), (0.49f, 0f));

    [Header("Nuages")]
    [Tooltip("Face éclairée des nuages")]
    [SerializeField] private Gradient cloudLit = G((Night, "8C9CD0"), (BlueHour, "C8A4B4"), (Horizon, "FFC08C"), (Golden, "FFEED0"), (LateGolden, "FFF6E6"), (Day, "FFFFFF"), (Noon, "FFFFFF"));
    [Tooltip("Face à l'ombre des nuages")]
    [SerializeField] private Gradient cloudShadow = G((Night, "34437E"), (BlueHour, "5E5E92"), (Horizon, "8C7898"), (Golden, "A49EBC"), (LateGolden, "AEB2CA"), (Day, "AFC3E1"), (Noon, "B4C8E6"));

    [Header("Lune")]
    [Tooltip("Couleur de la lune (disque, halo, lumière)")]
    [SerializeField] private Color moonColor = new Color(0.62f, 0.72f, 1f);
    [Tooltip("Intensité de la lumière directionnelle de la lune")]
    [SerializeField] private AnimationCurve moonLightIntensity = C((Night, 0.75f), (0.4f, 0.6f), (0.47f, 0f));
    [Tooltip("Visibilité du disque et du halo de la lune dans le ciel")]
    [SerializeField] private AnimationCurve moonVisibility = C((Night, 1f), (0.44f, 0.9f), (0.52f, 0f));

    [Header("Soleil (lumière)")]
    [Tooltip("Couleur de la lumière du soleil")]
    [SerializeField] private Gradient sunLightColor = G((Night, "FF8040"), (Horizon, "FF9A58"), (Golden, "FFB874"), (LateGolden, "FFD49E"), (Day, "FFF2DC"), (Noon, "FFF6E8"));
    [Tooltip("Intensité de la lumière du soleil (0 = éteinte)")]
    [SerializeField] private AnimationCurve sunLightIntensity = C((0.48f, 0f), (0.52f, 0.9f), (Golden, 2.0f), (LateGolden, 2.1f), (Day, 2.1f), (Noon, 2.2f));

    [Header("Lumière ambiante (trois couleurs)")]
    [SerializeField] private Gradient ambientSky = G((Night, "2E4282"), (BlueHour, "50507E"), (Horizon, "7A6C80"), (Golden, "9696B0"), (Day, "A4B6D0"), (Noon, "A8BAD2"));
    [SerializeField] private Gradient ambientEquator = G((Night, "22305E"), (BlueHour, "4E4462"), (Horizon, "8A6A5A"), (Golden, "B09478"), (Day, "BCB09A"), (Noon, "C0B49E"));
    [SerializeField] private Gradient ambientGround = G((Night, "141A30"), (BlueHour, "2C2434"), (Horizon, "4A382E"), (Golden, "64503E"), (Day, "7A6A50"), (Noon, "7C6C52"));

    [Header("Brouillard")]
    [Tooltip("Couleur du brouillard (et de la brume du ciel à l'horizon)")]
    [SerializeField] private Gradient fogColor = G((Night, "1E2C5C"), (BlueHour, "5A5A88"), (Horizon, "D8A47E"), (Golden, "EEC898"), (LateGolden, "E6D6B8"), (Day, "CCDCE6"), (Noon, "C8DAE6"));
    [Tooltip("Densité du brouillard (0,005 = léger, 0,02 = épais)")]
    [SerializeField] private AnimationCurve fogDensity = C((Night, 0.0035f), (BlueHour, 0.0045f), (Horizon, 0.0045f), (LateGolden, 0.0036f), (Day, 0.003f), (Noon, 0.0028f));
    [Tooltip("Couleur de la lueur du brouillard en direction de l'astre")]
    [SerializeField] private Gradient scatterColor = G((Night, "6A82D2"), (0.45f, "3A3A50"), (Horizon, "FF9C50"), (Golden, "FFC070"), (LateGolden, "FFD898"), (Day, "FFF0D0"));
    [Tooltip("Force de la lueur du brouillard vers l'astre")]
    [SerializeField] private AnimationCurve scatterIntensity = C((Night, 0.35f), (0.45f, 0.05f), (Horizon, 0.8f), (LateGolden, 0.6f), (Day, 0.3f), (Noon, 0.25f));

    [Header("Rayons de lumière")]
    [Tooltip("Couleur des rayons (soleil puis lune)")]
    [SerializeField] private Gradient godRayColor = G((Night, "8CA0FF"), (0.45f, "404060"), (Horizon, "FFA860"), (Golden, "FFD08C"), (Day, "FFF4DC"));
    [Tooltip("Force des rayons de lumière")]
    [SerializeField] private AnimationCurve godRayIntensity = C((Night, 0.3f), (0.45f, 0f), (Horizon, 0.8f), (LateGolden, 0.7f), (Day, 0.35f), (Noon, 0.3f));

    [Header("Post-process (poids des volumes)")]
    [Tooltip("Poids du volume « heure dorée » (lever / coucher)")]
    [SerializeField] private AnimationCurve goldenHourWeight = C((BlueHour, 0f), (0.52f, 1f), (LateGolden, 1f), (Day, 0f));
    [Tooltip("Poids du volume « nuit »")]
    [SerializeField] private AnimationCurve nightWeight = C((0.38f, 1f), (0.48f, 0f));

    [Header("Particules d'ambiance")]
    [Tooltip("Quantité de poussières en suspension (jour)")]
    [SerializeField] private AnimationCurve dustAmount = C((0.48f, 0f), (0.56f, 1f), (Noon, 1f));
    [Tooltip("Quantité de lucioles (nuit)")]
    [SerializeField] private AnimationCurve firefliesAmount = C((Night, 1f), (0.42f, 1f), (0.5f, 0f));

    /// <summary>Toutes les valeurs du profil pour une hauteur de soleil donnée (0 = minuit, 0,5 = horizon, 1 = midi).</summary>
    public DayNightLightingState Evaluate(float sunHeight01)
    {
        float h = Mathf.Clamp01(sunHeight01);
        return new DayNightLightingState
        {
            SkyZenith = skyZenith.Evaluate(h),
            SkyHorizon = skyHorizon.Evaluate(h),
            SkyGround = skyGround.Evaluate(h),
            SunGlowColor = sunGlowColor.Evaluate(h),
            SunGlowIntensity = Mathf.Max(0f, sunGlowIntensity.Evaluate(h)),
            SunDiscColor = sunDiscColor.Evaluate(h),
            StarsVisibility = Mathf.Clamp01(starsVisibility.Evaluate(h)),
            CloudLit = cloudLit.Evaluate(h),
            CloudShadow = cloudShadow.Evaluate(h),
            MoonColor = moonColor,
            MoonLightIntensity = Mathf.Max(0f, moonLightIntensity.Evaluate(h)),
            MoonVisibility = Mathf.Clamp01(moonVisibility.Evaluate(h)),
            SunLightColor = sunLightColor.Evaluate(h),
            SunLightIntensity = Mathf.Max(0f, sunLightIntensity.Evaluate(h)),
            AmbientSky = ambientSky.Evaluate(h),
            AmbientEquator = ambientEquator.Evaluate(h),
            AmbientGround = ambientGround.Evaluate(h),
            FogColor = fogColor.Evaluate(h),
            FogDensity = Mathf.Max(0f, fogDensity.Evaluate(h)),
            ScatterColor = scatterColor.Evaluate(h),
            ScatterIntensity = Mathf.Max(0f, scatterIntensity.Evaluate(h)),
            GodRayColor = godRayColor.Evaluate(h),
            GodRayIntensity = Mathf.Max(0f, godRayIntensity.Evaluate(h)),
            GoldenHourWeight = Mathf.Clamp01(goldenHourWeight.Evaluate(h)),
            NightWeight = Mathf.Clamp01(nightWeight.Evaluate(h)),
            DustAmount = Mathf.Clamp01(dustAmount.Evaluate(h)),
            FirefliesAmount = Mathf.Clamp01(firefliesAmount.Evaluate(h)),
        };
    }

    // ---------- Aides pour les valeurs par défaut (C# pur : appelées pendant la construction de l'asset) ----------
    static Gradient G(params (float t, string hex)[] keys)
    {
        var colors = new GradientColorKey[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            int rgb = System.Convert.ToInt32(keys[i].hex, 16);
            var c = new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
            colors[i] = new GradientColorKey(c, keys[i].t);
        }
        var g = new Gradient { mode = GradientMode.Blend };
        g.SetKeys(colors, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    // Tangentes plates : transitions douces sans dépassement (pas d'intensité négative entre deux clés).
    static AnimationCurve C(params (float t, float v)[] keys)
    {
        var frames = new Keyframe[keys.Length];
        for (int i = 0; i < keys.Length; i++) frames[i] = new Keyframe(keys[i].t, keys[i].v, 0f, 0f);
        return new AnimationCurve(frames) { preWrapMode = WrapMode.ClampForever, postWrapMode = WrapMode.ClampForever };
    }
}

/// <summary>Valeurs d'éclairage évaluées pour une heure donnée.</summary>
public struct DayNightLightingState
{
    public Color SkyZenith, SkyHorizon, SkyGround, SunGlowColor, SunDiscColor, CloudLit, CloudShadow, MoonColor;
    public Color SunLightColor, AmbientSky, AmbientEquator, AmbientGround, FogColor, ScatterColor, GodRayColor;
    public float SunGlowIntensity, StarsVisibility, MoonLightIntensity, MoonVisibility, SunLightIntensity;
    public float FogDensity, ScatterIntensity, GodRayIntensity, GoldenHourWeight, NightWeight, DustAmount, FirefliesAmount;
}
