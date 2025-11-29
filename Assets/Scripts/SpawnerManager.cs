using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class SpawnerManager : MonoBehaviour
{
    [Header("Dependencies")]
    public MazeGenerator mazeGenerator;

    [Header("Global Settings")]
    [Tooltip("Search radius around the random point to find a valid NavMesh position.")]
    public float searchRadius = 5.0f;

    // Calculated automatically from the MazeGenerator settings
    private float calculatedMapSize; 

    [System.Serializable]
    public class WaveSettings
    {
        public string waveName; 
        public string navMeshAreaName; // Must match EXACTLY the name in Navigation tab
        public GameObject enemyPrefab;
        public int count;
    }

    [Header("Waves Queue")]
    [Tooltip("List of waves waiting to be spawned. This list is cleared as waves are processed.")]
    public List<WaveSettings> waves;

    // --- MAIN METHODS ---

    /// <summary>
    /// Processes the current list of waves, spawns them, and removes them from the list.
    /// </summary>
    public void StartSpawning()
    {
        CalculateMapSize();

        // Loop backwards to remove items safely while iterating
        for (int i = waves.Count - 1; i >= 0; i--)
        {
            StartCoroutine(SpawnWave(waves[i]));
            
            // Remove the wave from the list so it doesn't spawn again if we call StartSpawning later
            waves.RemoveAt(i);
        }
    }

    /// <summary>
    /// Adds a wave to the queue without spawning it immediately. Call StartSpawning() to trigger it.
    /// </summary>
    public void AddWave(string areaName, GameObject prefab, int count, string debugName = "Dynamic Wave")
    {
        WaveSettings newWave = new WaveSettings
        {
            navMeshAreaName = areaName,
            enemyPrefab = prefab,
            count = count,
            waveName = debugName
        };

        waves.Add(newWave);
    }

    /// <summary>
    /// Destroys all enemies (children of this object) and stops pending spawns.
    /// </summary>
    public void ClearAllEnemies()
    {
        // 1. Stop current spawning coroutines to prevent new mobs from appearing
        StopAllCoroutines();

        // 2. Destroy all children
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        Debug.Log("--- ALL ENEMIES CLEARED ---");
    }

    // --- INTERNAL LOGIC ---

    private void CalculateMapSize()
    {
        if (calculatedMapSize > 0) return; // Already calculated

        if (mazeGenerator != null)
        {
            float width = mazeGenerator.GetMazeWidth() * mazeGenerator.GetCellSize();
            float depth = mazeGenerator.GetMazeDepth() * mazeGenerator.GetCellSize();
            calculatedMapSize = Mathf.Max(width, depth);
        }
        else
        {
            Debug.LogError("SpawnerManager: MazeGenerator is not assigned!");
        }
    }

    private System.Collections.IEnumerator SpawnWave(WaveSettings wave)
    {
        int areaID = NavMesh.GetAreaFromName(wave.navMeshAreaName);
        
        if (areaID == -1) 
        {
            Debug.LogError($"ERROR: NavMesh Area '{wave.navMeshAreaName}' not found!");
            yield break;
        }

        // Create a bitmask for this specific area
        int areaMask = 1 << areaID;
        int spawnedCount = 0;
        int attempts = 0;
        
        // Safety limit to prevent infinite loops if the area is too small
        int maxAttempts = wave.count * 20;

        while (spawnedCount < wave.count && attempts < maxAttempts)
        {
            attempts++;
            Vector3 randomPoint = GetRandomPointInMap();
            NavMeshHit hit;

            // Check if the random point is valid AND belongs to the correct Area
            if (NavMesh.SamplePosition(randomPoint, out hit, searchRadius, areaMask))
            {
                // Instantiate as child of SpawnerManager
                GameObject newEnemy = Instantiate(wave.enemyPrefab, hit.position, Quaternion.identity, transform);
                
                // Assign the Area Mask to the agent so it stays in its zone
                NavMeshAgent agent = newEnemy.GetComponent<NavMeshAgent>();
                if (agent != null) 
                {
                    agent.areaMask = areaMask;
                }

                spawnedCount++;
            }
            
            // Small delay to prevent game freeze during heavy generation
            if (spawnedCount % 10 == 0) yield return null;
        }
        
        // Logging results
        if (spawnedCount < wave.count)
            Debug.LogWarning($"Wave '{wave.waveName}' incomplete: {spawnedCount}/{wave.count} (Check Area size or NavMesh)");
        else
            Debug.Log($"Wave '{wave.waveName}' finished: {spawnedCount}/{wave.count}");
    }

    private Vector3 GetRandomPointInMap()
    {
        float halfSize = calculatedMapSize / 2f;
        float x = Random.Range(-halfSize, halfSize);
        float z = Random.Range(-halfSize, halfSize);
        return new Vector3(x, 0, z);
    }
}