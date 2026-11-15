using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Installe l'éclairage stylisé dans la scène ouverte : ciel (TeamTimo/StylizedSky), brouillard et rayons
/// (renderer feature « AtmosphericFog » sur tous les renderers URP), volumes de post-process jour / heure dorée / nuit,
/// particules d'ambiance, profil d'éclairage, et branche le tout sur le DayNightCycle de la scène.
/// Les assets déjà présents dans Assets/Art/Lighting ne sont pas écrasés (sauf avec « Réinitialiser »).
/// </summary>
public static class SkyLightingSetup
{
    const string Folder = "Assets/Art/Lighting";
    const string FogFeatureName = "AtmosphericFog";

    [MenuItem("TeamTimo/Éclairage/Installer ciel, brouillard et post-process dans la scène")]
    public static void InstallMenu() => Install(false);

    [MenuItem("TeamTimo/Éclairage/Réinitialiser les assets d'éclairage aux valeurs par défaut")]
    public static void ResetMenu()
    {
        if (EditorUtility.DisplayDialog("Réinitialiser l'éclairage",
                "Les profils de post-process, le profil d'éclairage et les matériaux de Assets/Art/Lighting vont être remis aux valeurs par défaut. Continuer ?",
                "Réinitialiser", "Annuler"))
            Install(true);
    }

    public static void Install(bool resetAssets)
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Art", "Lighting");

        Material skyMat = GetMaterial($"{Folder}/M_StylizedSky.mat", "TeamTimo/StylizedSky", resetAssets, null);
        Material fogMat = GetMaterial($"{Folder}/M_AtmosphericFog.mat", "Hidden/TeamTimo/AtmosphericFog", resetAssets, null);
        Material dustMat = GetMaterial($"{Folder}/M_AmbientDust.mat", "Universal Render Pipeline/Particles/Unlit", resetAssets,
            m => SetupAdditiveParticle(m, new Color(1.6f, 1.45f, 1.15f, 0.6f)));
        Material fireflyMat = GetMaterial($"{Folder}/M_Fireflies.mat", "Universal Render Pipeline/Particles/Unlit", resetAssets,
            m => SetupAdditiveParticle(m, new Color(5f, 4.2f, 1.1f, 1f)));

        var profile = GetAsset<DayNightLightingProfile>($"{Folder}/DayNightLighting_Default.asset", resetAssets);
        VolumeProfile ppBase = GetVolumeProfile($"{Folder}/PP_Base.asset", resetAssets, ConfigureBase);
        VolumeProfile ppGolden = GetVolumeProfile($"{Folder}/PP_GoldenHour.asset", resetAssets, ConfigureGolden);
        VolumeProfile ppNight = GetVolumeProfile($"{Folder}/PP_Night.asset", resetAssets, ConfigureNight);

        AddFogFeatureToRenderers(fogMat);
        SetupScene(skyMat, profile, ppBase, ppGolden, ppNight, dustMat, fireflyMat);

        AssetDatabase.SaveAssets();
        Debug.Log("[SkyLightingSetup] Ciel, brouillard et post-process installés.");
    }

    // ---------- Assets ----------
    static Material GetMaterial(string path, string shaderName, bool reset, System.Action<Material> configure)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null && !reset) return mat;
        var shader = Shader.Find(shaderName);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
            // Remet les propriétés du shader à leurs valeurs par défaut.
            var fresh = new Material(shader);
            mat.CopyPropertiesFromMaterial(fresh);
            Object.DestroyImmediate(fresh);
        }
        configure?.Invoke(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void SetupAdditiveParticle(Material m, Color color)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f); // additif
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", color);
        m.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
    }

    static T GetAsset<T>(string path, bool reset) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null && !reset) return asset;
        var fresh = ScriptableObject.CreateInstance<T>();
        if (asset == null)
        {
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }
        EditorUtility.CopySerialized(fresh, asset);
        asset.name = System.IO.Path.GetFileNameWithoutExtension(path); // CopySerialized efface le nom
        Object.DestroyImmediate(fresh);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static VolumeProfile GetVolumeProfile(string path, bool reset, System.Action<VolumeProfile> configure)
    {
        var vp = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (vp != null && !reset) return vp;
        if (vp == null)
        {
            vp = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(vp, path);
        }
        else
        {
            foreach (var c in vp.components.ToList())
            {
                vp.components.Remove(c);
                Object.DestroyImmediate(c, true);
            }
        }
        configure(vp);
        foreach (var c in vp.components)
        {
            c.name = c.GetType().Name;
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, vp);
        }
        EditorUtility.SetDirty(vp);
        return vp;
    }

    static void ConfigureBase(VolumeProfile vp)
    {
        var tone = vp.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);

        var bloom = vp.Add<Bloom>(true);
        bloom.threshold.Override(1.15f);
        bloom.intensity.Override(0.6f);
        bloom.scatter.Override(0.65f);
        bloom.tint.Override(new Color(1f, 0.97f, 0.92f));

        var color = vp.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.1f);
        color.contrast.Override(10f);
        color.saturation.Override(14f);

        var smh = vp.Add<ShadowsMidtonesHighlights>(true);
        // Ombres légèrement bleutées, hautes lumières chaudes : rendu « peint ».
        smh.shadows.Override(new Vector4(0.96f, 0.98f, 1.06f, 0f));
        smh.highlights.Override(new Vector4(1.04f, 1.01f, 0.95f, 0f));

        var vignette = vp.Add<Vignette>(true);
        vignette.intensity.Override(0.2f);
        vignette.smoothness.Override(0.45f);
    }

    static void ConfigureGolden(VolumeProfile vp)
    {
        var wb = vp.Add<WhiteBalance>(true);
        wb.temperature.Override(8f);
        wb.tint.Override(3f);

        var color = vp.Add<ColorAdjustments>(true);
        color.colorFilter.Override(new Color(1f, 0.97f, 0.92f));
        color.saturation.Override(25f);
        color.contrast.Override(14f);

        var bloom = vp.Add<Bloom>(true);
        bloom.intensity.Override(0.8f);
        bloom.threshold.Override(1.2f);
        bloom.tint.Override(new Color(1f, 0.86f, 0.66f));
    }

    static void ConfigureNight(VolumeProfile vp)
    {
        var wb = vp.Add<WhiteBalance>(true);
        wb.temperature.Override(-16f);

        var color = vp.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.1f);
        color.colorFilter.Override(new Color(0.86f, 0.92f, 1f));
        color.saturation.Override(4f);
        color.contrast.Override(6f);

        var lgg = vp.Add<LiftGammaGain>(true);
        lgg.lift.Override(new Vector4(0.97f, 0.99f, 1.06f, 0.02f));

        var bloom = vp.Add<Bloom>(true);
        bloom.intensity.Override(1.3f);
        bloom.threshold.Override(0.85f);
        bloom.tint.Override(new Color(0.85f, 0.9f, 1f));

        var vignette = vp.Add<Vignette>(true);
        vignette.intensity.Override(0.3f);
    }

    // ---------- Renderer feature ----------
    static void AddFogFeatureToRenderers(Material fogMat)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null) continue;

            var feature = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f.name == FogFeatureName);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = FogFeatureName;
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                var so = new SerializedObject(data);
                var map = so.FindProperty("m_RendererFeatureMap");
                map.arraySize = data.rendererFeatures.Count;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // Après les transparents : les contours (Outline) et le lierre sont embrumés comme le reste.
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth;
            feature.passMaterial = fogMat;
            feature.passIndex = 0;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
        }
    }

    // ---------- Scène ----------
    static void SetupScene(Material skyMat, DayNightLightingProfile profile, VolumeProfile ppBase, VolumeProfile ppGolden,
        VolumeProfile ppNight, Material dustMat, Material fireflyMat)
    {
        RenderSettings.skybox = skyMat;
        RenderSettings.fog = false; // remplacé par le brouillard atmosphérique plein écran
        RenderSettings.ambientMode = AmbientMode.Trilight;

        var cycle = Object.FindFirstObjectByType<DayNightCycle>();

        var root = GameObject.Find("Ambiance");
        if (root == null) root = new GameObject("Ambiance");

        Volume baseVol = GetVolume(root.transform, "PostProcess_Base", ppBase, 0f, 1f);
        Volume goldenVol = GetVolume(root.transform, "PostProcess_GoldenHour", ppGolden, 10f, 0f);
        Volume nightVol = GetVolume(root.transform, "PostProcess_Night", ppNight, 20f, 0f);

        AmbientParticles particles = root.GetComponentInChildren<AmbientParticles>();
        if (particles == null)
        {
            var go = new GameObject("AmbientParticles");
            go.transform.SetParent(root.transform, false);
            go.transform.position = LobbyCenter();
            particles = go.AddComponent<AmbientParticles>();
            ParticleSystem dust = CreateParticles(go.transform, "Dust", dustMat, false);
            ParticleSystem flies = CreateParticles(go.transform, "Fireflies", fireflyMat, true);
            var pso = new SerializedObject(particles);
            pso.FindProperty("dust").objectReferenceValue = dust;
            pso.FindProperty("fireflies").objectReferenceValue = flies;
            pso.ApplyModifiedPropertiesWithoutUndo();
        }

        if (cycle != null)
        {
            var so = new SerializedObject(cycle);
            so.FindProperty("profile").objectReferenceValue = profile;
            so.FindProperty("goldenHourVolume").objectReferenceValue = goldenVol;
            so.FindProperty("nightVolume").objectReferenceValue = nightVol;
            so.ApplyModifiedPropertiesWithoutUndo();

            var pso = new SerializedObject(particles);
            pso.FindProperty("dayNightCycle").objectReferenceValue = cycle;
            pso.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[SkyLightingSetup] Aucun DayNightCycle dans la scène : profil et volumes non branchés.");
        }

        // Post-process sur toutes les caméras de la scène.
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            EditorUtility.SetDirty(data);
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    static Volume GetVolume(Transform parent, string name, VolumeProfile profile, float priority, float weight)
    {
        Transform t = parent.Find(name);
        GameObject go = t != null ? t.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);
        var vol = go.GetComponent<Volume>();
        if (vol == null) vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = priority;
        vol.weight = weight;
        vol.sharedProfile = profile;
        return vol;
    }

    static Vector3 LobbyCenter()
    {
        var walls = GameObject.Find("LobbyWalls");
        if (walls == null) return Vector3.zero;
        var renderers = walls.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return walls.transform.position;
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return new Vector3(b.center.x, b.min.y, b.center.z);
    }

    static ParticleSystem CreateParticles(Transform parent, string name, Material mat, bool fireflies)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = fireflies ? 220 : 320;
        main.startLifetime = fireflies ? new ParticleSystem.MinMaxCurve(5f, 9f) : new ParticleSystem.MinMaxCurve(9f, 15f);
        main.startSpeed = fireflies ? new ParticleSystem.MinMaxCurve(0.05f, 0.35f) : new ParticleSystem.MinMaxCurve(0.02f, 0.15f);
        main.startSize = fireflies ? new ParticleSystem.MinMaxCurve(0.12f, 0.22f) : new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        main.startColor = Color.white;

        var emission = ps.emission;
        emission.rateOverTime = fireflies ? 10f : 18f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = fireflies ? new Vector3(80f, 5f, 80f) : new Vector3(90f, 16f, 90f);
        shape.position = fireflies ? new Vector3(0f, 2.8f, 0f) : new Vector3(0f, 8f, 0f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = fireflies ? 0.9f : 0.25f;
        noise.frequency = fireflies ? 0.35f : 0.12f;
        noise.scrollSpeed = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            fireflies
                // Les lucioles clignotent doucement.
                ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.15f, 0.4f),
                          new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0.2f, 0.8f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ps.Play();
        return ps;
    }
}
