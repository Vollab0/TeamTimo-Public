using System;
using Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Branche le joueur réseau sur la scène (ADR 002).
/// Seul le joueur local (propriétaire) reçoit l'input, la caméra et l'UI ;
/// les joueurs distants sont de simples avatars déplacés par NetworkTransform / NetworkAnimator.
/// Dans une scène sans réseau (scènes de test), le joueur garde son comportement solo.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(CharacterController))]
public class PlayerNetworkSetup : NetworkBehaviour
{
    [Header("Apparition")]
    [Tooltip("Décalage entre les joueurs à l'apparition, multiplié par le numéro du joueur (hôte = 0)")]
    [SerializeField] private Vector3 spawnSpacing = new Vector3(2f, 0f, 0f);

    [Header("Références du prefab")]
    [Tooltip("Point visé par la caméra 3e personne (enfant LookAt)")]
    [SerializeField] private Transform cameraLookAt;

    [Tooltip("Composants actifs seulement chez le joueur local (déplacement, santé, ramassage, animation pilotée)")]
    [SerializeField] private Behaviour[] localOnlyComponents;

    [Header("Input")]
    [Tooltip("Nom de l'action qui change d'emplacement d'inventaire (molette)")]
    [SerializeField] private string scrollInventoryAction = "ChangerSlot";

    /// <summary>Joueur contrôlé sur cette machine (null tant qu'il n'est pas apparu).</summary>
    public static PlayerNetworkSetup LocalPlayer { get; private set; }

    /// <summary>Déclenché quand le joueur local apparaît sur le réseau.</summary>
    public static event Action<PlayerNetworkSetup> OnLocalPlayerSpawned;

    private PlayerInput _playerInput;
    private CharacterController _characterController;
    private PlayerController _playerController;
    private PlayerHealth _playerHealth;
    private InventoryUI _inventoryUI;
    private PlayerInput.ActionEvent _scrollEvent;

    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        _characterController = GetComponent<CharacterController>();
        _playerController = GetComponent<PlayerController>();
        _playerHealth = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        PlayerRegistry.Register(transform);
    }

    private void OnDisable()
    {
        PlayerRegistry.Unregister(transform);
    }

    private void Start()
    {
        // Scène sans réseau : le prefab garde son comportement solo d'origine.
        // (PlayerInput est désactivé dans le prefab pour ne pas voler le clavier à un autre joueur.)
        if (!IsSpawned)
        {
            _playerInput.enabled = true;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            foreach (Behaviour component in localOnlyComponents)
            {
                if (component != null) component.enabled = false;
            }
            _playerInput.enabled = false;
            return;
        }

        MoveToSpawnPoint();
        BindSceneReferences();
        _playerInput.enabled = true;

        LocalPlayer = this;
        OnLocalPlayerSpawned?.Invoke(this);
    }

    public override void OnNetworkDespawn()
    {
        if (_scrollEvent != null && _inventoryUI != null)
        {
            _scrollEvent.RemoveListener(_inventoryUI.OnScrollInventory);
        }

        if (LocalPlayer == this)
        {
            LocalPlayer = null;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>Active ou coupe les commandes du joueur local (écran de chargement, menus). Sans effet sur un joueur distant.</summary>
    public void SetInputEnabled(bool enabled)
    {
        if (IsSpawned && !IsOwner) return;
        _playerInput.enabled = enabled;
    }

    private void MoveToSpawnPoint()
    {
        // Le CharacterController écrase la position s'il reste actif pendant le déplacement.
        _characterController.enabled = false;
        transform.position += spawnSpacing * (float)OwnerClientId;
        _characterController.enabled = true;
    }

    /// <summary>
    /// Le joueur n'est plus posé dans la scène : on lui rend ici les références
    /// que l'Inspector lui donnait (caméra, HUD, inventaire).
    /// </summary>
    private void BindSceneReferences()
    {
        CinemachineFreeLook freeLook = FindFirstObjectByType<CinemachineFreeLook>();
        if (freeLook != null)
        {
            freeLook.Follow = transform;
            freeLook.LookAt = cameraLookAt != null ? cameraLookAt : transform;
            _playerController.thirdPersonCamera = freeLook;
        }
        else
        {
            Debug.LogWarning("PlayerNetworkSetup : aucune CinemachineFreeLook dans la scène.");
        }

        HeartUIManager heartUI = FindFirstObjectByType<HeartUIManager>();
        if (heartUI != null)
        {
            _playerHealth.hudDocument = heartUI.monDocumentUI;
            heartUI.SetPlayer(_playerHealth);
        }

        _inventoryUI = FindFirstObjectByType<InventoryUI>();
        if (_inventoryUI != null)
        {
            foreach (PlayerInput.ActionEvent actionEvent in _playerInput.actionEvents)
            {
                // actionName est de la forme "Gameplay/ChangerSlot[/Mouse/scroll]"
                if (actionEvent.actionName.Contains("/" + scrollInventoryAction))
                {
                    _scrollEvent = actionEvent;
                    _scrollEvent.AddListener(_inventoryUI.OnScrollInventory);
                    break;
                }
            }
        }
    }
}
