using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Mob réseau (ADR 002) : l'IA et le NavMeshAgent ne tournent que chez l'hôte ;
/// les clients reçoivent seulement la position (NetworkTransform).
/// Dans une scène sans réseau, le mob fonctionne comme avant.
/// Le NavMeshAgent est désactivé dans le prefab : il n'est activé que là où l'IA tourne
/// (les clients n'ont pas de NavMesh et l'agent y afficherait des erreurs).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyNetworkSetup : NetworkBehaviour
{
    [Tooltip("Composants actifs seulement chez l'hôte (IA, dégâts)")]
    [SerializeField] private Behaviour[] serverOnlyComponents;

    private NavMeshAgent _agent;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();

        // Scène sans réseau : le mob est créé par un simple Instantiate.
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening)
        {
            _agent.enabled = true;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _agent.enabled = true;
            return;
        }

        _agent.enabled = false;
        foreach (Behaviour component in serverOnlyComponents)
        {
            if (component != null) component.enabled = false;
        }
    }
}
