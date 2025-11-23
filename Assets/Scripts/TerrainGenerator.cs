using UnityEngine;
using System.Collections.Generic;
public class TerrainGenerator : MonoBehaviour
{
    [Header("Design Manuel")]
    public TerrainData manualCenterData;

    [Header("Dépendances")]
    [SerializeField] private MazeGenerator mazeGenerator;

    [Header("Design Visuel (Textures & Végétation)")]
    // Créez un Layer dans le projet (Clic droit -> Create -> Terrain Layer) et glissez-le ici
    public TerrainLayer grassLayer; 
    // Texture 2D simple pour l'herbe 3D (brins d'herbe)
    public Texture2D grassDetailTexture; 
    // Votre prefab d'arbre
    public GameObject treePrefab;

    [Header("Densité de la Végétation")]
    [Range(0, 1)] public float treeDensity = 0.02f; // Chance qu'un arbre apparaisse
    public int detailResolutionPerPatch = 16; // Qualité de l'herbe (défaut 16 ou 32)

    [Header("Paramètres de Fusion (Lobby)")]
    public float safeZoneHalfExtent = 50f; // "Rayon" du carré (demi-largeur)
    public float transitionDistance = 30f; 

    [Header("Paramètres Généraux")]
    public Vector3 terrainSize = new Vector3(100, 50, 100);
    public int heightmapResolution = 129;
    // Résolution pour l'herbe (souvent plus élevée que le heightmap, ex: 512)
    public int detailResolution = 512; 

    [Header("Paramètres du Bruit (Noise)")]
    public float noiseScale = 0.05f;
    public float heightMultiplier = 10.0f;
    public Vector2 noiseOffset;

    private Vector2 worldCenterPoint;
    private int gridWidth;
    private int gridDepth;

    void Start()
    {
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

        GenerateGrid();
    }

    void GenerateGrid()
    {
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
            }
        }
    }

    void MoveExistingManualTile(Vector3 pos)
    {
        if (manualCenterData != null)
        {
            GameObject existingManualTile = GameObject.Find("Terrain_CENTRAL_MANUEL");
            if(existingManualTile != null) existingManualTile.transform.position = pos;
        }
    }

    void CreateProceduralTile(int gridX, int gridZ, Vector3 position)
    {
        TerrainData tData = new TerrainData();
        
        // 1. Configuration de base
        tData.heightmapResolution = heightmapResolution;
        tData.size = terrainSize;
        
        // Configuration de la résolution de l'herbe (Details)
        tData.SetDetailResolution(detailResolution, detailResolutionPerPatch);

        // 2. Application de la Texture (Splatmap)
        ApplyTextureLayer(tData);

        // 3. Génération du Relief
        float[,] heights = GenerateHeights(tData, position);
        tData.SetHeights(0, 0, heights);

        // 4. Ajout de la Végétation (Arbres & Herbe)
        // Note: On le fait APRES SetHeights pour connaitre la hauteur du sol
        GenerateVegetation(tData, position, heights);

        // 5. Création de l'objet
        GameObject terrainObj = Terrain.CreateTerrainGameObject(tData);
        terrainObj.transform.position = position;
        terrainObj.name = $"Terrain_Procedural_{gridX}_{gridZ}";
        terrainObj.transform.SetParent(this.transform);
        // --- REGLAGES DE RENDU OBLIGATOIRES ---
        Terrain terrainComp = terrainObj.GetComponent<Terrain>();
        terrainComp.detailObjectDensity = 1.0f; // Densité visuelle au max
    }

    // ---------------------------------------------------------
    // SECTION 1 : TEXTURES
    // ---------------------------------------------------------
    void ApplyTextureLayer(TerrainData tData)
    {
        if (grassLayer == null) return;

        TerrainLayer[] layers = new TerrainLayer[1];
        layers[0] = grassLayer;
        tData.terrainLayers = layers;
        
        // Pas besoin de SetAlphamaps complexe si on a une seule texture, 
        // Unity remplit la couche 0 par défaut partout.
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
        // --- A. CONFIGURATION DES PROTOTYPES (D'après votre image) ---
        
        // 1. Configuration de l'Herbe (Détails)
        if (grassDetailTexture != null)
        {
            DetailPrototype[] detailProtos = new DetailPrototype[1];
            detailProtos[0] = new DetailPrototype 
            { 
                prototypeTexture = grassDetailTexture,
                
                // Case "Billboard" décochée dans votre image = Mode Grass
                renderMode = DetailRenderMode.Grass, 
                
                // Dimensions (Min Width 1, Max Width 2, Min Height 0.5, Max Height 1.5)
                minWidth = 1.0f, 
                maxWidth = 2.0f,
                minHeight = 0.5f, 
                maxHeight = 1.5f,
                
                // Bruit (Noise Spread 0.9)
                noiseSpread = 0.9f,
                
                // Couleurs (Healthy = Vert Fluo, Dry = Vert Foncé)
                healthyColor = Color.green, // #00FF00
                dryColor = new Color(0.0f, 0.5f, 0.0f) // Vert foncé approximatif
            };
            tData.detailPrototypes = detailProtos;
        }

        // 2. Configuration des Arbres
        if (treePrefab != null)
        {
            TreePrototype[] treeProtos = new TreePrototype[1];
            treeProtos[0] = new TreePrototype { prefab = treePrefab };
            tData.treePrototypes = treeProtos;
        }

        // --- B. GÉNÉRATION DES ARBRES (Rien ne change ici) ---
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

                    if (blendFactor > 0.1f && Random.value < treeDensity)
                    {
                        float offsetX = Random.Range(-0.01f, 0.01f);
                        float offsetZ = Random.Range(-0.01f, 0.01f);

                        TreeInstance tree = new TreeInstance();
                        tree.position = new Vector3(normX + offsetX, heightMap[z, x], normZ + offsetZ);
                        tree.prototypeIndex = 0;
                        tree.widthScale = 1f;
                        tree.heightScale = 1f;
                        tree.color = Color.white;
                        tree.lightmapColor = Color.white;
                        trees.Add(tree);
                    }
                }
            }
            tData.treeInstances = trees.ToArray();
        }

// --- TEST DEBUG HERBE ---
        if (grassDetailTexture != null)
        {
            int detailRes = tData.detailResolution;
            int[,] detailMap = new int[detailRes, detailRes];

            for (int z = 0; z < detailRes; z++)
            {
                for (int x = 0; x < detailRes; x++)
                {
                    // On force l'herbe partout, sans bruit ni blending, juste pour tester
                    detailMap[z, x] = 100000; // brins par case
                }
            }
            tData.SetDetailLayer(0, 0, 0, detailMap);
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