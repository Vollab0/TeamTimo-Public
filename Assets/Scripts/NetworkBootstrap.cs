using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Lance ou rejoint une partie (ADR 002) avec Unity Transport, en attendant le transport Steam.
/// - Éditeur principal : héberge automatiquement (on joue seul comme avant).
/// - Joueurs virtuels de Multiplayer Play Mode : rejoignent automatiquement l'éditeur principal.
/// - Build : menu de debug « Héberger » / « Rejoindre (IP) », utilisable en réseau local.
/// </summary>
public class NetworkBootstrap : MonoBehaviour
{
    [Header("Démarrage automatique (éditeur)")]
    [Tooltip("Dans l'éditeur principal, héberger une partie dès le Play (pratique pour tester seul)")]
    [SerializeField] private bool autoHostInEditor = true;

    [Tooltip("Dans les joueurs virtuels de Multiplayer Play Mode, rejoindre automatiquement l'éditeur principal")]
    [SerializeField] private bool autoJoinVirtualPlayers = true;

    [Tooltip("Nombre de tentatives de connexion automatique (l'hôte peut démarrer après le joueur virtuel)")]
    [SerializeField] private int autoJoinAttempts = 5;

    [Tooltip("Délai entre deux tentatives de connexion automatique (secondes)")]
    [SerializeField] private float autoJoinDelay = 1.5f;

    [Header("Connexion")]
    [Tooltip("Adresse proposée par défaut dans le champ « Rejoindre »")]
    [SerializeField] private string defaultAddress = "127.0.0.1";

    [Tooltip("Port UDP utilisé par l'hôte")]
    [SerializeField] private ushort port = 7777;

    [Tooltip("Durée sans nouvelles avant de couper une connexion (secondes). Doit dépasser le temps de génération du labyrinthe, qui bloque Unity pendant ce temps (Unity Transport : 30 s par défaut).")]
    [SerializeField] private float disconnectTimeoutSeconds = 180f;

    [Header("Interface")]
    [Tooltip("UIDocument qui affiche NetworkMenu.uxml")]
    [SerializeField] private UIDocument menuDocument;

    private NetworkManager _networkManager;
    private UnityTransport _transport;

    private VisualElement _menuRoot;
    private TextField _addressField;
    private Button _hostButton;
    private Button _joinButton;
    private Label _statusLabel;

    private int _remainingAutoJoinAttempts;

    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();
        if (_networkManager == null) _networkManager = FindFirstObjectByType<NetworkManager>();
        _transport = _networkManager != null ? _networkManager.GetComponent<UnityTransport>() : null;

        // La génération du labyrinthe fige chaque machine plusieurs dizaines de secondes :
        // sans ce délai, l'hôte coupe le client qui génère (et inversement).
        if (_transport != null) _transport.DisconnectTimeoutMS = Mathf.RoundToInt(disconnectTimeoutSeconds * 1000f);
    }

    private void OnEnable()
    {
        if (menuDocument != null)
        {
            VisualElement root = menuDocument.rootVisualElement;
            _menuRoot = root.Q<VisualElement>("NetworkMenu");
            _addressField = root.Q<TextField>("AddressField");
            _hostButton = root.Q<Button>("HostButton");
            _joinButton = root.Q<Button>("JoinButton");
            _statusLabel = root.Q<Label>("StatusLabel");

            if (_addressField != null) _addressField.value = defaultAddress;
            if (_hostButton != null) _hostButton.clicked += OnHostClicked;
            if (_joinButton != null) _joinButton.clicked += OnJoinClicked;
        }

        if (_networkManager != null)
        {
            _networkManager.OnClientConnectedCallback += OnClientConnected;
            _networkManager.OnClientStopped += OnLocalClientStopped;
        }
    }

    private void OnDisable()
    {
        if (_hostButton != null) _hostButton.clicked -= OnHostClicked;
        if (_joinButton != null) _joinButton.clicked -= OnJoinClicked;

        if (_networkManager != null)
        {
            _networkManager.OnClientConnectedCallback -= OnClientConnected;
            _networkManager.OnClientStopped -= OnLocalClientStopped;
        }
    }

    private void Start()
    {
        if (_networkManager == null || _transport == null)
        {
            Debug.LogError("NetworkBootstrap : il faut un NetworkManager avec un UnityTransport dans la scène.");
            return;
        }

#if UNITY_EDITOR
        bool isVirtualPlayer = !Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor;
        if (isVirtualPlayer && autoJoinVirtualPlayers)
        {
            _remainingAutoJoinAttempts = autoJoinAttempts;
            StartCoroutine(AutoJoinRoutine());
            return;
        }
        if (!isVirtualPlayer && autoHostInEditor)
        {
            Host();
            return;
        }
#endif
        ShowMenu(true, "");
    }

    // --- Actions ---

    public void Host()
    {
        // Écoute sur toutes les interfaces : les autres PC du réseau local peuvent rejoindre par IP.
        _transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
        if (!_networkManager.StartHost())
        {
            ShowMenu(true, "Impossible d'héberger (port " + port + " déjà utilisé ?)");
        }
    }

    public void Join(string address)
    {
        _transport.SetConnectionData(address, port);
        ShowMenu(true, "Connexion à " + address + "…");
        if (!_networkManager.StartClient())
        {
            ShowMenu(true, "Impossible de démarrer la connexion.");
        }
    }

    private IEnumerator AutoJoinRoutine()
    {
        // Laisse le temps à l'éditeur principal de démarrer l'hôte.
        yield return new WaitForSeconds(autoJoinDelay);
        _remainingAutoJoinAttempts--;
        Join(defaultAddress);
    }

    private void OnHostClicked() => Host();

    private void OnJoinClicked()
    {
        string address = _addressField != null && !string.IsNullOrWhiteSpace(_addressField.value)
            ? _addressField.value.Trim()
            : defaultAddress;
        Join(address);
    }

    // --- Événements réseau ---

    private void OnClientConnected(ulong clientId)
    {
        // Chez l'hôte, ce rappel arrive aussi pour les autres joueurs : on ne réagit qu'au joueur local.
        if (clientId != _networkManager.LocalClientId) return;
        _remainingAutoJoinAttempts = 0;
        ShowMenu(false, "");
    }

    private void OnLocalClientStopped(bool wasHost)
    {
        if (_remainingAutoJoinAttempts > 0)
        {
            StartCoroutine(AutoJoinRoutine());
            return;
        }

        string reason = _networkManager.DisconnectReason;
        ShowMenu(true, string.IsNullOrEmpty(reason) ? "Déconnecté." : "Déconnecté : " + reason);
    }

    // --- Interface ---

    private void ShowMenu(bool visible, string status)
    {
        if (_menuRoot != null)
        {
            _menuRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
        if (_statusLabel != null) _statusLabel.text = status;

        if (visible)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }
    }
}
