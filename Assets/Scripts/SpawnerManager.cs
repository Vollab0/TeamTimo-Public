using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class SpawnerManager : MonoBehaviour
{
    [Header("Dependencies")]
    public MazeGenerator mazeGenerator;

    [Header("Global Settings")]
    public float searchRadius = 10.0f; // Augmenté pour être sûr de trouver le sol
    [Tooltip("Layer des Murs ET Piliers")]
    public LayerMask obstacleLayer;

    private float calculatedMapSize;
    private List<Vector3> _bullSpawnHistory = new List<Vector3>();
    private readonly List<GameObject> _spawnedEnemies = new List<GameObject>();
    private Vector3 _mapCenter;

    // En réseau, seul l'hôte fait apparaître les mobs (ils sont ensuite répliqués chez les clients).
    private static bool IsNetworked => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private static bool CanSpawnHere => !IsNetworked || NetworkManager.Singleton.IsServer;

    [System.Serializable]
    public class WaveSettings
    {
        public string waveName;
        public string navMeshAreaName;
        public GameObject enemyPrefab;
        public int count;
    }

    public List<WaveSettings> waves;

    public void StartSpawning()
    {
        if (!CanSpawnHere) return;
        CalculateMapSize();
        _bullSpawnHistory.Clear();

        for (int i = waves.Count - 1; i >= 0; i--)
        {
            StartCoroutine(SpawnWave(waves[i]));
            waves.RemoveAt(i);
        }
    }

    public void AddWave(string areaName, GameObject prefab, int count, string debugName)
    {
        waves.Add(new WaveSettings { navMeshAreaName = areaName, enemyPrefab = prefab, count = count, waveName = debugName });
    }

    public void ClearAllEnemies()
    {
        StopAllCoroutines();
        _bullSpawnHistory.Clear();
        foreach (GameObject enemy in _spawnedEnemies)
        {
            if (enemy == null) continue;
            NetworkObject networkObject = enemy.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned) networkObject.Despawn(true);
            else Destroy(enemy);
        }
        _spawnedEnemies.Clear();
        Debug.Log("--- ALL ENEMIES CLEARED ---");
    }

    private System.Collections.IEnumerator SpawnWave(WaveSettings wave)
    {
        int areaID = NavMesh.GetAreaFromName(wave.navMeshAreaName);
        if (areaID == -1) { Debug.LogError($"Area '{wave.navMeshAreaName}' introuvable !"); yield break; }

        int areaMask = 1 << areaID;
        int spawnedCount = 0;
        int attempts = 0;
        int maxAttempts = wave.count * 50;

        while (spawnedCount < wave.count && attempts < maxAttempts)
        {
            attempts++;
            Vector3 spawnPos = Vector3.zero;
            bool validPositionFound = false;
            bool isBull = wave.enemyPrefab.name.Contains("Bull");

            if (isBull)
            {
                // On cherche un LARGE couloir (min 6m de large)
                spawnPos = GetBestSpawnPointForBull(areaMask, 20, 6.0f);

                if (spawnPos != Vector3.zero)
                {
                    validPositionFound = true;
                    _bullSpawnHistory.Add(spawnPos);
                }
            }
            else
            {
                // Spawn simple
                Vector3? p = GetRandomNavMeshPoint(areaMask);
                if (p.HasValue)
                {
                    spawnPos = p.Value;
                    validPositionFound = true;
                }
            }

            if (validPositionFound)
            {
                GameObject newEnemy = SpawnEnemy(wave.enemyPrefab, spawnPos);
                NavMeshAgent agent = newEnemy.GetComponent<NavMeshAgent>();

                if (agent != null)
                {
                    agent.areaMask = areaMask;
                    // Force la position NavMesh pour éviter tout décalage visuel
                    agent.Warp(spawnPos);
                }
                spawnedCount++;
            }
            if (spawnedCount % 5 == 0) yield return null;
        }
    }

    private GameObject SpawnEnemy(GameObject prefab, Vector3 position)
    {
        GameObject enemy;
        NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
        if (IsNetworked && networkObject != null)
        {
            // Pas de parent : un objet réseau ne peut pas être rangé sous un objet non réseau.
            enemy = Instantiate(prefab, position, Quaternion.identity);
            enemy.GetComponent<NetworkObject>().Spawn(true);
        }
        else
        {
            enemy = Instantiate(prefab, position, Quaternion.identity, transform);
        }
        _spawnedEnemies.Add(enemy);
        return enemy;
    }

    // --- LOGIQUE INTELLIGENTE ---

    private Vector3 GetBestSpawnPointForBull(int areaMask, int samples, float minWidthRequired)
    {
        Vector3 bestPoint = Vector3.zero;
        float maxSpace = -1f;

        for (int i = 0; i < samples; i++)
        {
            // 1. Trouve un point valide SUR le NavMesh (avec la bonne hauteur Y)
            Vector3? candidateOpt = GetRandomNavMeshPoint(areaMask);
            if (!candidateOpt.HasValue) continue;

            Vector3 p = candidateOpt.Value;
            if (IsCorridorOccupied(p)) continue;

            // 2. Mesure l'espace disponible (Mur <-> Mur ou Pilier)
            // On lève le point de mesure (+1.5m) pour taper les murs/piliers et pas le sol
            Vector3 measureP = p + Vector3.up * 1.5f;

            float wX = GetRayDist(measureP, Vector3.right) + GetRayDist(measureP, Vector3.left);
            float wZ = GetRayDist(measureP, Vector3.forward) + GetRayDist(measureP, Vector3.back);

            float corridorSize = Mathf.Max(wX, wZ);

            // 3. Condition critique : Est-ce assez large pour le Bull ?
            if (corridorSize > minWidthRequired && corridorSize > maxSpace)
            {
                maxSpace = corridorSize;
                bestPoint = p;
            }
        }

        // Si on n'a rien trouvé de "parfait", on renvoie Zero pour que la boucle réessaie
        return bestPoint;
    }

    // Retourne un point collé au NavMesh ou null
    private Vector3? GetRandomNavMeshPoint(int areaMask)
    {
        Vector3 randomRaw = GetRandomPointInMap();
        NavMeshHit hit;
        // SearchRadius large (10f) pour trouver le sol même s'il est haut ou bas
        if (NavMesh.SamplePosition(randomRaw, out hit, searchRadius, areaMask))
        {
            return hit.position;
        }
        return null;
    }

    private bool IsCorridorOccupied(Vector3 candidate)
    {
        foreach (Vector3 occupied in _bullSpawnHistory)
        {
            if (Vector3.Distance(candidate, occupied) < 8.0f) return true; // Distance min entre Bulls
        }
        return false;
    }

    private float GetRayDist(Vector3 origin, Vector3 dir)
    {
        if (Physics.Raycast(origin, dir, out RaycastHit hit, 100f, obstacleLayer))
            return hit.distance;
        return 100f;
    }

    private Vector3 GetRandomPointInMap()
    {
        float half = calculatedMapSize / 2f;
        // Centré sur les cellules réelles, un peu au-dessus de leur pied (l'objet Maze peut être déplacé dans la scène).
        Vector3 center = _mapCenter + Vector3.up * 5f;
        return center + new Vector3(Random.Range(-half, half), 0, Random.Range(-half, half));
    }

    private void CalculateMapSize()
    {
        if (mazeGenerator != null)
        {
            calculatedMapSize = Mathf.Max(mazeGenerator.GetMazeWidth(), mazeGenerator.GetMazeDepth()) * mazeGenerator.GetCellSize();
            Vector3 first = mazeGenerator.GetCellWorldPosition(0, 0);
            Vector3 last = mazeGenerator.GetCellWorldPosition(mazeGenerator.GetMazeWidth() - 1, mazeGenerator.GetMazeDepth() - 1);
            _mapCenter = (first + last) / 2f;
        }
        else
        {
            calculatedMapSize = 100f;
            _mapCenter = Vector3.zero;
        }
    }
}