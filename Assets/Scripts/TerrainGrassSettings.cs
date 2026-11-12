using System;
using UnityEngine;

/// <summary>
/// Réglages de l'herbe des terrains (détails en meshes instanciés, mode « Coverage »).
/// Utilisé par <see cref="TerrainGenerator"/> pour les tuiles procédurales et par l'outil
/// d'éditeur « TeamTimo > Terrain > Appliquer l'herbe au Lobby » pour le terrain fait main.
/// La répartition suit la texture du sol : l'herbe pousse sur la couche d'herbe, les touffes
/// sèches sur la terre et en lisière.
/// </summary>
[CreateAssetMenu(fileName = "TerrainGrassSettings", menuName = "TeamTimo/Réglages de l'herbe du terrain")]
public class TerrainGrassSettings : ScriptableObject
{
    [Serializable]
    public class GrassDetail
    {
        [Tooltip("Nom pour s'y retrouver dans la liste.")]
        [SerializeField] private string label = "Herbe";
        [Tooltip("Prefab de la touffe (MeshFilter + MeshRenderer, matériau TeamTimo/StylizedGrass avec GPU Instancing).")]
        [SerializeField] private GameObject prefab;

        [Header("Taille (multiplie le mesh, haut de 1 m)")]
        [SerializeField] private float minWidth = 1.3f;
        [SerializeField] private float maxWidth = 2f;
        [SerializeField] private float minHeight = 1.1f;
        [SerializeField] private float maxHeight = 1.8f;
        [Tooltip("Taille du bruit qui fait varier la taille des touffes.")]
        [SerializeField] private float noiseSpread = 0.3f;

        [Header("Densité")]
        [Tooltip("Densité Unity du détail (mode Coverage) : nombre de touffes pour une couverture de 100 %.")]
        [SerializeField] private float density = 1f;
        [Tooltip("Couverture (0-1) là où le sol est de l'herbe.")]
        [Range(0, 1)] [SerializeField] private float coverageOnGrass = 1f;
        [Tooltip("Couverture (0-1) là où le sol est de la terre.")]
        [Range(0, 1)] [SerializeField] private float coverageOnDirt;
        [Tooltip("Couverture ajoutée (0-1) en lisière herbe / terre.")]
        [Range(0, 1)] [SerializeField] private float coverageOnEdge;

        [Header("Regroupement en taches")]
        [Tooltip("Taille des taches (m).")]
        [SerializeField] private float clumpScale = 25f;
        [Tooltip("0 = réparti partout, 1 = seulement en petites taches isolées.")]
        [Range(0, 1)] [SerializeField] private float clumpiness = 0.3f;

        [Header("Placement")]
        [Tooltip("0 = touffes verticales, 1 = alignées sur la pente.")]
        [Range(0, 1)] [SerializeField] private float alignToGround = 0.3f;
        [Tooltip("Décalage aléatoire de la position (0-1), évite l'effet de grille.")]
        [Range(0, 1)] [SerializeField] private float positionJitter = 0.9f;

        public string Label => label;
        public GameObject Prefab => prefab;
        public float Density => density;
        public float CoverageOnGrass => coverageOnGrass;
        public float CoverageOnDirt => coverageOnDirt;
        public float CoverageOnEdge => coverageOnEdge;
        public float ClumpScale => clumpScale;
        public float Clumpiness => clumpiness;

        public DetailPrototype ToPrototype(int seed)
        {
            return new DetailPrototype
            {
                prototype = prefab,
                usePrototypeMesh = true,
                renderMode = DetailRenderMode.VertexLit,
                useInstancing = true,
                minWidth = minWidth,
                maxWidth = maxWidth,
                minHeight = minHeight,
                maxHeight = maxHeight,
                noiseSpread = noiseSpread,
                noiseSeed = seed,
                density = density,
                alignToGround = alignToGround,
                positionJitter = positionJitter,
                useDensityScaling = true,
                healthyColor = Color.white,
                dryColor = Color.white,
            };
        }
    }

    [Header("Touffes")]
    [SerializeField] private GrassDetail[] details = Array.Empty<GrassDetail>();

    [Header("Résolution (par tuile de terrain)")]
    [Tooltip("Résolution de la carte d'herbe (cases par côté). 512 sur 400 m = une case tous les ~0,8 m.")]
    [SerializeField] private int detailResolution = 512;
    [Tooltip("Cases par bloc de rendu. Plus petit = meilleur culling, plus de draw calls.")]
    [SerializeField] private int detailResolutionPerPatch = 32;

    [Header("Affichage")]
    [Tooltip("Distance d'affichage de l'herbe (m). Les matériaux d'herbe rétrécissent les touffes avant cette distance.")]
    [Range(10, 250)] [SerializeField] private float detailDistance = 75f;
    [Tooltip("Multiplicateur global de densité (réglage de qualité, 0-1).")]
    [Range(0, 1)] [SerializeField] private float densityScale = 1f;
    [Tooltip("Début du rétrécissement des touffes, en part de la distance d'affichage.")]
    [Range(0.3f, 0.98f)] [SerializeField] private float fadeStartRatio = 0.72f;

    public int DetailResolution => detailResolution;
    public float DetailDistance => detailDistance;

    /// <summary>Déclare les touffes et la résolution de l'herbe sur une TerrainData (efface la carte d'herbe).</summary>
    public void ApplyPrototypes(TerrainData data)
    {
        data.SetDetailScatterMode(DetailScatterMode.CoverageMode);
        data.SetDetailResolution(detailResolution, detailResolutionPerPatch);
        var prototypes = new DetailPrototype[details.Length];
        for (int i = 0; i < details.Length; i++) prototypes[i] = details[i].ToPrototype(1000 + i * 77);
        data.detailPrototypes = prototypes;
    }

    /// <summary>Réglages d'affichage du composant Terrain (distance, densité, instancing).</summary>
    public void ApplyRendering(Terrain terrain)
    {
        terrain.detailObjectDistance = detailDistance;
        terrain.detailObjectDensity = densityScale;
        terrain.drawInstanced = true;
    }

    /// <summary>
    /// Remplit la carte d'herbe d'après la couche d'herbe de la splatmap.
    /// <paramref name="grassWeight"/> : poids de l'herbe par texel de splatmap [z, x], 0..1 (le reste = terre).
    /// <paramref name="buffers"/> : tableaux réutilisables (un par touffe, taille detailResolution²), alloués si null.
    /// </summary>
    public void PaintDetails(TerrainData data, float[,] grassWeight, Vector3 terrainWorldPos, int seed, ref int[][,] buffers)
    {
        int res = data.detailResolution;
        int splatRes = grassWeight.GetLength(0);
        Vector3 size = data.size;
        if (buffers == null || buffers.Length != details.Length || buffers[0].GetLength(0) != res)
        {
            buffers = new int[details.Length][,];
            for (int i = 0; i < details.Length; i++) buffers[i] = new int[res, res];
        }

        var rng = new System.Random(seed);
        var offsets = new Vector2[details.Length];
        for (int i = 0; i < details.Length; i++) offsets[i] = new Vector2(rng.Next(-10000, 10000), rng.Next(-10000, 10000));

        float cellX = size.x / res;
        float cellZ = size.z / res;
        for (int z = 0; z < res; z++)
        {
            int sz = Mathf.Min(splatRes - 1, (int)((z + 0.5f) / res * splatRes));
            float worldZ = terrainWorldPos.z + (z + 0.5f) * cellZ;
            for (int x = 0; x < res; x++)
            {
                int sx = Mathf.Min(splatRes - 1, (int)((x + 0.5f) / res * splatRes));
                float g = grassWeight[sz, sx];
                float d = 1f - g;
                float edge = 4f * g * d;
                float worldX = terrainWorldPos.x + (x + 0.5f) * cellX;
                for (int i = 0; i < details.Length; i++)
                {
                    GrassDetail detail = details[i];
                    float coverage = g * detail.CoverageOnGrass + d * detail.CoverageOnDirt + edge * detail.CoverageOnEdge;
                    if (coverage > 0.001f && detail.Clumpiness > 0f)
                    {
                        float scale = Mathf.Max(0.1f, detail.ClumpScale);
                        float n = Mathf.PerlinNoise(worldX / scale + offsets[i].x, worldZ / scale + offsets[i].y);
                        float lo = Mathf.Lerp(-0.2f, 0.55f, detail.Clumpiness);
                        coverage *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lo, lo + 0.25f, n));
                    }
                    buffers[i][z, x] = Mathf.RoundToInt(255f * Mathf.Clamp01(coverage));
                }
            }
        }
        for (int i = 0; i < details.Length; i++) data.SetDetailLayer(0, 0, i, buffers[i]);
    }

#if UNITY_EDITOR
    // Garde le rétrécissement des matériaux d'herbe synchronisé avec la distance d'affichage.
    private void OnValidate()
    {
        foreach (GrassDetail detail in details)
        {
            if (detail?.Prefab == null) continue;
            var renderer = detail.Prefab.GetComponentInChildren<Renderer>();
            Material mat = renderer != null ? renderer.sharedMaterial : null;
            if (mat == null || !mat.HasProperty("_FadeEnd")) continue;
            float end = detailDistance * 0.97f;
            float start = detailDistance * fadeStartRatio;
            if (Mathf.Approximately(mat.GetFloat("_FadeEnd"), end) && Mathf.Approximately(mat.GetFloat("_FadeStart"), start)) continue;
            mat.SetFloat("_FadeEnd", end);
            mat.SetFloat("_FadeStart", start);
            UnityEditor.EditorUtility.SetDirty(mat);
        }
    }
#endif
}
