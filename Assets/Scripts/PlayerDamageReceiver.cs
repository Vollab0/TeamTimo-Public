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
        ReceiveDamage(amount, sourcePosition, -1f);
    }

    /// <summary>
    /// Inflige des dégâts avec une force de recul propre à l'attaque (ex. charge du taureau).
    /// <paramref name="knockbackForce"/> négatif = recul par défaut de PlayerHealth.
    /// </summary>
    public void ReceiveDamage(int amount, Vector3 sourcePosition, float knockbackForce)
    {
        if (!IsSpawned)
        {
            Apply(amount, sourcePosition, knockbackForce);
            return;
        }

        // Seul l'hôte décide des dégâts ; un client qui verrait un coup localement l'ignore.
        if (!IsServer) return;

        ApplyDamageRpc(amount, sourcePosition, knockbackForce);
    }

    [Rpc(SendTo.Owner)]
    private void ApplyDamageRpc(int amount, Vector3 sourcePosition, float knockbackForce)
    {
        Apply(amount, sourcePosition, knockbackForce);
    }

    private void Apply(int amount, Vector3 sourcePosition, float knockbackForce)
    {
        if (knockbackForce < 0f) _health.Blesser(amount, sourcePosition);
        else _health.Blesser(amount, sourcePosition, knockbackForce);
    }
}
