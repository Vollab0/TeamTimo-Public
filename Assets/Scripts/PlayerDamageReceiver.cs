using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Point d'entrée des dégâts reçus par un joueur (ADR 002).
/// Les mobs tournent chez l'hôte : quand ils touchent un joueur, l'hôte envoie les dégâts
/// à la machine qui contrôle ce joueur, qui applique la perte de cœurs, le recul et la mort.
/// Sans réseau (scènes de test), les dégâts s'appliquent directement.
/// </summary>
[RequireComponent(typeof(PlayerHealth))]
public class PlayerDamageReceiver : NetworkBehaviour
{
    private PlayerHealth _health;

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
    }

    /// <summary>Inflige des dégâts à ce joueur. À appeler par les mobs, qui ne tournent que chez l'hôte.</summary>
    public void ReceiveDamage(int amount, Vector3 sourcePosition)
    {
        if (!IsSpawned)
        {
            _health.Blesser(amount, sourcePosition);
            return;
        }

        // Seul l'hôte décide des dégâts ; un client qui verrait un coup localement l'ignore.
        if (!IsServer) return;

        ApplyDamageRpc(amount, sourcePosition);
    }

    [Rpc(SendTo.Owner)]
    private void ApplyDamageRpc(int amount, Vector3 sourcePosition)
    {
        _health.Blesser(amount, sourcePosition);
    }
}
