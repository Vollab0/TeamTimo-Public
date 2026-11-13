using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class TerrainGenerator : MonoBehaviour
{
    [Header("Design Manuel")]
    public TerrainData manualCenterData;

    [Header("Dépendances")]
    [SerializeField] private MazeGenerator mazeGenerator;

    [Header("Design Visuel (Textures & Végétation)")]
    // Créez un Layer dans le projet (Clic droit -> Create -> Terrain Layer) et glissez-le ici
    public TerrainLayer grassLayer;
    [Tooltip("Couche de terre peinte en taches sur les tuiles procédurales (laisser vide = herbe partout).")]
    [SerializeField] private TerrainLayer dirtLayer;
    [Tooltip("Réglages de l'herbe (touffes, densité, distance), partagés avec le terrain du Lobby.")]
    [SerializeField] private TerrainGrassSettings grassSettings;
    // Votre prefab d'arbre
    public GameObject treePrefab;
    [Tooltip("Autres essences d'arbres, tirées au hasard avec treePrefab (même graine pour tous les joueurs).")]
    [SerializeField] private GameObject[] treeVariants = Array.Empty<GameObject>();

    [Header("Densité de la Végétation")]
    [Range(0, 1)] public float treeDensity = 0.02f; // Chance qu'un arbre apparaisse
    public float treeScaleFactor = 1f;
    [Tooltip("Variation aléatoire de la taille des arbres (± part de treeScaleFactor).")]
    [Range(0, 0.5f)] [SerializeField] private float treeScaleJitter = 0.15f;

    [Header("Taches de terre (tuiles procédurales)")]
    [Tooltip("Échelle du bruit des taches de terre (1 / taille en m). 0,012 = taches d'environ 80 m.")]
    [SerializeField] private float dirtNoiseScale = 0.012f;
    [Tooltip("Seuil du bruit au-dessus duquel le sol devient de la terre (0-1). Plus haut = moins de terre.")]
    [Range(0, 1)] [SerializeField] private float dirtThreshold = 0.62f;
    [Tooltip("Douceur de la transition herbe / terre (en unités de bruit).")]
    [Range(0.01f, 0.3f)] [SerializeField] private float dirtSoftness = 0.06f;
    [Tooltip("Résolution de la splatmap des tuiles procédurales (texels par côté).")]
    [SerializeField] private int alphamapResolution = 256;

    [Header("Paramètres de Fusion (Lobby)")]
    public float safeZoneHalfExtent = 50f; // "Rayon" du carré (demi-largeur)
    public float transitionDistance = 30f;

    [Header("Paramètres Généraux")]
    public Vector3 terrainSize = new Vector3(100, 50, 100);
    public int heightmapResolution = 129;

    [Header("Paramètres du Bruit (Noise)")]
    public float noiseScale = 0.05f;
    public float heightMultiplier = 10.0f;
    public Vector2 noiseOffset;

    [Header("Chargement progressif")]
    [Tooltip("Temps de calcul maximum par frame (ms) avant de laisser tourner le jeu et le réseau. Une tuile n'est jamais coupée en deux : une tuile lourde peut dépasser ce budget.")]
    [SerializeField] private float maxMillisecondsPerFrame = 30f;

    /// <summary>Vrai quand toutes les tuiles de terrain sont générées.</summary>
    public bool IsReady { get; private set; }

    /// <summary>Tuiles procédurales déjà générées / à générer (pour l'écran de chargement).</summary>
    public int TilesBuilt { get; private set; }
    public int TilesTotal { get; private set; }

    /// <summary>Déclenché quand toutes les tuiles de terrain sont générées.</summary>
    public event Action OnTerrainReady;

    private Vector2 worldCenterPoint;
    private int gridWidth;
    private int gridDepth;

    // Même graine que le labyrinthe : les arbres (qui ont des colliders) sont au même endroit chez tous les joueurs.
    private System.Random _random;
    private const string ProceduralTilePrefix = "Terrain_Procedural_";

    private Coroutine _buildRoutine;
    // Tampons réutilisés d'une tuile à l'autre (évite d'allouer plusieurs Mo par tuile).
    private int[][,] _detailBuffers;
    private float[,,] _alphamapBuffer;
    private float[,] _grassWeightBuffer;
    private Vector2 _dirtOffset;
    private int _grassSeed;

    void OnEnable()
    {
        if (mazeGenerator != null) mazeGenerator.OnGenerationStarted += Build;
    }

    void OnDisable()
    {
        if (mazeGenerator != null) mazeGenerator.OnGenerationStarted -= Build;
    }

    void Start()
    {
        // Si le labyrinthe a déjà commencé sa génération avant notre abonnement, on se rattrape.
        if (mazeGenerator != null && mazeGenerator.Seed != 0 && _random == null) Build(mazeGenerator.Seed);
    }

    /// <summary>Génère les tuiles de terrain à partir de la graine du labyrinthe, sur plusieurs frames.</summary>
    public void Build(int seed)
    {
        if (_buildRoutine != null) StopCoroutine(_buildRoutine);
        _buildRoutine = StartCoroutine(BuildRoutine(seed));
    }

    private IEnumerator BuildRoutine(int seed)
    {
        IsReady = false;
        _random = new System.Random(seed ^ 0x5EED);
        // Graines séparées : ne décale pas le tirage des arbres (qui ont des colliders).
        var decorRandom = new System.Random(seed ^ 0xD127);
        _dirtOffset = new Vector2(decorRandom.Next(-10000, 10000), decorRandom.Next(-10000, 10000));
        _grassSeed = decorRandom.Next();
        ClearProceduralTiles();

        if (manualCenterData != null)
        {
            terrainSize = manualCenterData.size;
            heightmapResolution = manualCenterData.heightmapResolution;
        }

        // --- Calcul de la grille ---
        int totalCellsX = mazeGenerator.GetMazeWidth(); 
        int totalCellsZ = mazeGenerator.GetMazeDepth();
        int cellSize = mazeGenerator.GetCellSize();

        float totalMazeMetersX = totalCellsX * cellSize;
        float totalMazeMetersZ = totalCellsZ * cellSize;

        gridWidth = Mathf.CeilToInt(totalMazeMetersX / terrainSize.x);
        gridDepth = Mathf.CeilToInt(totalMazeMetersZ / terrainSize.z);

        worldCenterPoint = Vector2.zero; 

        TilesBuilt = 0;
        TilesTotal = gridWidth * gridDepth - 1; // la tuile centrale est posée à la main
        Stopwatch total = Stopwatch.StartNew();
        yield return GenerateGrid();

        // libère la mémoire des tampons
        _detailBuffers = null;
        _alphamapBuffer = null;
        _grassWeightBuffer = null;
        _buildRoutine = null;
        IsReady = true;
        Debug.Log($"Terrain : {gridWidth * gridDepth - 1} tuiles générées en {total.Elapsed.TotalSeconds:F1} s");
        OnTerrainReady?.Invoke();
    }

    void ClearProceduralTiles()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith(ProceduralTilePrefix)) Destroy(child.gameObject);
        }
    }

    IEnumerator GenerateGrid()
    {
        Stopwatch frameTimer = Stopwatch.StartNew();
        int centerX = gridWidth / 2;
        int centerZ = gridDepth / 2;
        float pivotOffsetX = terrainSize.x / 2f;
        float pivotOffsetZ = terrainSize.z / 2f;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                float xPos = ((x - centerX) * terrainSize.x) - pivotOffsetX;
                float zPos = ((z - centerZ) * terrainSize.z) - pivotOffsetZ;
                Vector3 tilePos = new Vector3(xPos, 0, zPos);

                // TUILE CENTRALE (MANUELLE)
                if (x == centerX && z == centerZ)
                {
                    MoveExistingManualTile(tilePos);
                    continue; 
                }

                // TUILES PROCÉDURALES
                CreateProceduralTile(x, z, tilePos);
                TilesBuilt++;

                // On rend la main au jeu (et au réseau) dès que le budget de la frame est dépassé.
                if (frameTimer.Elapsed.TotalMilliseconds > maxMillisecondsPerFrame)
                {
                    yield return null;
                    frameTimer.Restart();
                }
            }
        }
    }

    void MoveExistingManualTile(Vector3 pos)
    {
        if (manualCenterData != null)
        {
            GameObject existingManualTile = GameObject.Find("Terrain_CENTRAL_MANUEL");
            if(existingManualTile != null) existingManualTile.transform.position = pos;

            // Même distance / densité d'herbe que les tuiles procédurales (composant de scène, pas l'asset).
            if (grassSettings != null)
            {
                foreach (Terrain terrain in Terrain.activeTerrains)
                {
                    if (terrain.terrainData == manualCenterData) grassSettings.ApplyRendering(terrain);
                }
            }
        }
    }

    void CreateProceduralTile(int gridX, int gridZ, Vector3 position)
    {
        TerrainData tData = new TerrainData();
        
        // 1. Configuration de base
        tData.heightmapResolution = heightmapResolution;
        tData.size = terrainSize;

        // 2. Application des textures (herbe + taches de terre)
        ApplyTextureLayers(tData, position);

        // 3. Génération du Relief
        float[,] heights = GenerateHeights(tData, position);
        tData.SetHeights(0, 0, heights);

        // 4. Ajout de la Végétation (Arbres & Herbe)
        // Note: On le fait APRES SetHeights pour connaitre la hauteur du sol
        GenerateVegetation(tData, position, heights);

        // 5. Création de l'objet
        GameObject terrainObj = Terrain.CreateTerrainGameObject(tData);
        terrainObj.transform.position = position;
        terrainObj.name = $"{ProceduralTilePrefix}{gridX}_{gridZ}";
        terrainObj.transform.SetParent(this.transform);
        // --- REGLAGES DE RENDU (distance et densité de l'herbe, instancing) ---
        Terrain terrainComp = terrainObj.GetComponent<Terrain>();
        if (grassSettings != null) grassSettings.ApplyRendering(terrainComp);
    }

    // ---------------------------------------------------------
    // SECTION 1 : TEXTURES
    // ---------------------------------------------------------
    void ApplyTextureLayers(TerrainData tData, Vector3 worldPos)
    {
        int res = Mathf.Max(16, alphamapResolution);
        if (_grassWeightBuffer == null || _grassWeightBuffer.GetLength(0) != res) _grassWeightBuffer = new float[res, res];

        bool hasDirt = grassLayer != null && dirtLayer != null;
        if (grassLayer != null)
        {
            tData.terrainLayers = hasDirt ? new[] { grassLayer, dirtLayer } : new[] { grassLayer };
            tData.alphamapResolution = res;
        }
        if (!hasDirt)
        {
            // Une seule texture : Unity remplit la couche 0 partout.
            for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) _grassWeightBuffer[z, x] = 1f;
            return;
        }

        if (_alphamapBuffer == null || _alphamapBuffer.GetLength(0) != res) _alphamapBuffer = new float[res, res, 2];
        for (int z = 0; z < res; z++)
        {
            float globalZ = worldPos.z + (z + 0.5f) / res * terrainSize.z;
            for (int x = 0; x < res; x++)
            {
                float globalX = worldPos.x + (x + 0.5f) / res * terrainSize.x;
                float dirt = GetDirtWeight(globalX, globalZ);
                _alphamapBuffer[z, x, 0] = 1f - dirt;
                _alphamapBuffer[z, x, 1] = dirt;
                _grassWeightBuffer[z, x] = 1f - dirt;
            }
        }
        tData.SetAlphamaps(0, 0, _alphamapBuffer);
    }

    // Taches de terre : bruit sur deux octaves, coordonnées monde (continu d'une tuile à l'autre),
    // absent près du Lobby pour raccorder avec le terrain fait main.
    float GetDirtWeight(float globalX, float globalZ)
    {
        float blend = GetSquareBlendFactor(globalX, globalZ);
        if (blend <= 0f) return 0f;
        float n = Mathf.PerlinNoise(globalX * dirtNoiseScale + _dirtOffset.x, globalZ * dirtNoiseScale + _dirtOffset.y) * 0.7f
                + Mathf.PerlinNoise(globalX * dirtNoiseScale * 3.3f + _dirtOffset.y, globalZ * dirtNoiseScale * 3.3f + _dirtOffset.x) * 0.3f;
        float t = Mathf.InverseLerp(dirtThreshold - dirtSoftness, dirtThreshold + dirtSoftness, n);
        return Mathf.SmoothStep(0f, 1f, t) * blend;
    }

    // ---------------------------------------------------------
    // SECTION 2 : RELIEF (CARRÉ)
    // ---------------------------------------------------------
    float[,] GenerateHeights(TerrainData tData, Vector3 worldPos)
    {
        int res = tData.heightmapResolution;
        float[,] heights = new float[res, res];

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                // Conversion locale -> mondiale
                float globalX = worldPos.x + ((float)x / (res - 1)) * terrainSize.x;
                float globalZ = worldPos.z + ((float)z / (res - 1)) * terrainSize.z;

                // Récupération du facteur de mélange (Carré lobby)
                float blendFactor = GetSquareBlendFactor(globalX, globalZ);

                // Bruit
                float noise = Mathf.PerlinNoise(globalX * noiseScale + noiseOffset.x, globalZ * noiseScale + noiseOffset.y);
                
                heights[z, x] = noise * (heightMultiplier / terrainSize.y) * blendFactor; 
                // Note: on divise par terrainSize.y car Unity stocke les hauteurs entre 0 et 1
            }
        }
        return heights;
    }

    // ---------------------------------------------------------
    // SECTION 3 : VÉGÉTATION (ARBRES & HERBE)
    // ---------------------------------------------------------
    void GenerateVegetation(TerrainData tData, Vector3 worldPos, float[,] heightMap)
    {
        // --- A. CONFIGURATION DES PROTOTYPES ---

        // 1. Herbe : touffes en meshes instanciés (voir TerrainGrassSettings)
        if (grassSettings != null) grassSettings.ApplyPrototypes(tData);

        // 2. Configuration des Arbres : treePrefab + les variantes (essences tirées au hasard)
        int treeTypeCount = 0;
        if (treePrefab != null)
        {
            var treeProtos = new List<TreePrototype> { new TreePrototype { prefab = treePrefab } };
            foreach (GameObject variant in treeVariants)
                if (variant != null) treeProtos.Add(new TreePrototype { prefab = variant });
            tData.treePrototypes = treeProtos.ToArray();
            treeTypeCount = treeProtos.Count;
        }

        // --- B. GÉNÉRATION DES ARBRES ---
        if (treePrefab != null)
        {
            List<TreeInstance> trees = new List<TreeInstance>();
            int treeStep = 5; 

            for (int z = 0; z < tData.heightmapResolution; z += treeStep)
            {
                for (int x = 0; x < tData.heightmapResolution; x += treeStep)
                {
                    float normX = (float)x / (tData.heightmapResolution - 1);
                    float normZ = (float)z / (tData.heightmapResolution - 1);
                    float globalX = worldPos.x + normX * terrainSize.x;
                    float globalZ = worldPos.z + normZ * terrainSize.z;

                    float blendFactor = GetSquareBlendFactor(globalX, globalZ);

                    if (blendFactor > 0.1f && _random.NextDouble() < treeDensity)
                    {
                        float offsetX = Mathf.Lerp(-0.01f, 0.01f, (float)_random.NextDouble());
                        float offsetZ = Mathf.Lerp(-0.01f, 0.01f, (float)_random.NextDouble());

                        TreeInstance tree = new TreeInstance();
                        tree.position = new Vector3(normX + offsetX, heightMap[z, x], normZ + offsetZ);
                        tree.prototypeIndex = _random.Next(treeTypeCount);
                        tree.rotation = (float)_random.NextDouble() * Mathf.PI * 2f;
                        float scale = treeScaleFactor * (1f + Mathf.Lerp(-treeScaleJitter, treeScaleJitter, (float)_random.NextDouble()));
                        tree.widthScale = scale;
                        tree.heightScale = scale;
                        tree.color = Color.white;
                        tree.lightmapColor = Color.white;
                        trees.Add(tree);
                    }
                }
            }
            tData.treeInstances = trees.ToArray();
        }

        // --- C. HERBE : suit la splatmap (herbe sur l'herbe, touffes sèches sur la terre) ---
        if (grassSettings != null)
        {
            grassSettings.PaintDetails(tData, _grassWeightBuffer, worldPos, _grassSeed, ref _detailBuffers);
        }
    }

    // ---------------------------------------------------------
    // UTILITAIRES
    // ---------------------------------------------------------

    float GetSquareBlendFactor(float globalX, float globalZ)
    {
        float distX = Mathf.Abs(globalX - worldCenterPoint.x);
        float distZ = Mathf.Abs(globalZ - worldCenterPoint.y); // Attention worldCenterPoint est Vector2 (x, y) -> (x, z)
        float distFromCenter = Mathf.Max(distX, distZ);
        return Mathf.InverseLerp(safeZoneHalfExtent, safeZoneHalfExtent + transitionDistance, distFromCenter);
    }
}