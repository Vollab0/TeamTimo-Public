using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Liste des joueurs présents (locaux et distants), tenue à jour par PlayerNetworkSetup.
/// Les mobs s'en servent pour choisir leur cible au lieu de chercher « le » joueur par tag.
/// </summary>
public static class PlayerRegistry
{
    private static readonly List<Transform> _players = new List<Transform>();

    /// <summary>Joueurs actuellement dans la scène.</summary>
    public static IReadOnlyList<Transform> Players => _players;

    public static void Register(Transform player)
    {
        if (player != null && !_players.Contains(player)) _players.Add(player);
    }

    public static void Unregister(Transform player)
    {
        _players.Remove(player);
    }
}
