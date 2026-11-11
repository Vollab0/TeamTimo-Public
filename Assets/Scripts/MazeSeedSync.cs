using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Partage la graine du labyrinthe en réseau (ADR 002) : l'hôte la choisit,
/// les clients la reçoivent à la connexion, et chaque machine génère le même labyrinthe.
/// À placer dans la scène sur un objet avec un NetworkObject.
/// </summary>
public class MazeSeedSync : NetworkBehaviour
{
    [Header("Références")]
    [Tooltip("Générateur du labyrinthe de la scène")]
    [SerializeField] private MazeGenerator mazeGenerator;

    private readonly NetworkVariable<int> _seed = new NetworkVariable<int>(0);
    private bool _generated;

    private void Awake()
    {
        // Awake passe avant tous les Start de la scène : le labyrinthe attendra la graine du réseau.
        if (mazeGenerator != null) mazeGenerator.WaitForExternalSeed();
        else Debug.LogError("MazeSeedSync : la référence MazeGenerator n'est pas assignée.");
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _seed.Value = mazeGenerator.PickSeed();
        }

        if (_seed.Value != 0)
        {
            Generate(_seed.Value);
        }
        else
        {
            // Sécurité : la valeur arrive normalement avec l'apparition de l'objet.
            _seed.OnValueChanged += OnSeedChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        _seed.OnValueChanged -= OnSeedChanged;
        _generated = false;
    }

    private void OnSeedChanged(int previous, int current)
    {
        if (current == 0) return;
        _seed.OnValueChanged -= OnSeedChanged;
        Generate(current);
    }

    private void Generate(int seed)
    {
        if (_generated) return;
        _generated = true;
        mazeGenerator.GenerateWithSeed(seed);
    }
}
