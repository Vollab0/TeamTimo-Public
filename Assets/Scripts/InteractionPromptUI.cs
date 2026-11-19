using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Invite « [E] Ramasser la Pelle » affichée au-dessus de l'objet ramassable le plus proche du joueur local.
/// Se branche sur GrabObject.OnHighlightChanged ; l'élément UI (InteractionPrompt) est dans InventoryHealthUXML.uxml,
/// son style dans InteractionPromptStyle.uss. À poser sur l'objet du HUD (celui qui porte l'UIDocument).
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    [Header("Liaison UI")]
    [Tooltip("Document du HUD ; si vide, celui de cet objet")]
    [SerializeField] private UIDocument hudDocument;

    [Tooltip("Nom de l'élément de l'invite dans le UXML")]
    [SerializeField] private string promptName = "InteractionPrompt";

    [Header("Texte")]
    [Tooltip("Touche affichée dans la pastille (action Grab : E au clavier)")]
    [SerializeField] private string keyText = "E";

    [Tooltip("Affiche aussi « Ramasser : <objet> » à côté de la touche (sinon la touche seule)")]
    [SerializeField] private bool showActionText = false;

    [Tooltip("Verbe affiché devant le nom de l'objet (si showActionText)")]
    [SerializeField] private string actionText = "Ramasser";

    [Header("Placement")]
    [Tooltip("Hauteur (m) de l'invite au-dessus du haut de l'objet")]
    [Min(0f)]
    [SerializeField] private float heightAboveObject = 0.3f;

    private const string VisibleClass = "interaction-prompt--visible";

    private VisualElement _prompt;
    private Label _keyLabel;
    private Label _actionLabel;

    private GrabObject _grab;
    private Camera _camera;
    private Transform _target;
    private Collider _targetCollider;

    private void OnEnable()
    {
        if (hudDocument == null) hudDocument = GetComponent<UIDocument>();
        if (hudDocument != null)
        {
            VisualElement root = hudDocument.rootVisualElement;
            _prompt = root.Q<VisualElement>(promptName);
            if (_prompt != null)
            {
                _keyLabel = _prompt.Q<Label>("InteractionKey");
                _actionLabel = _prompt.Q<Label>("InteractionText");
                if (_keyLabel != null) _keyLabel.text = keyText;
                if (_actionLabel != null) _actionLabel.style.display = showActionText ? DisplayStyle.Flex : DisplayStyle.None;
                _prompt.RemoveFromClassList(VisibleClass);
            }
            else
            {
                Debug.LogWarning($"InteractionPromptUI : élément '{promptName}' introuvable dans le HUD.");
            }
        }

        // En réseau, le joueur local apparaît plus tard (PlayerNetworkSetup).
        PlayerNetworkSetup.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
        if (PlayerNetworkSetup.LocalPlayer != null) HandleLocalPlayerSpawned(PlayerNetworkSetup.LocalPlayer);
    }

    private void Start()
    {
        // Scène sans réseau : le joueur est déjà posé dans la scène.
        if (_grab == null) SetPlayer(FindFirstObjectByType<GrabObject>());
    }

    private void OnDisable()
    {
        PlayerNetworkSetup.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
        SetPlayer(null);
    }

    private void HandleLocalPlayerSpawned(PlayerNetworkSetup player)
    {
        SetPlayer(player != null ? player.GetComponent<GrabObject>() : null);
    }

    /// <summary>Branche l'invite sur le GrabObject du joueur local.</summary>
    public void SetPlayer(GrabObject grab)
    {
        if (_grab != null) _grab.OnHighlightChanged -= HandleHighlightChanged;
        _grab = grab;
        if (_grab != null)
        {
            _grab.OnHighlightChanged += HandleHighlightChanged;
            HandleHighlightChanged(_grab.HighlightedObject);
        }
        else
        {
            HandleHighlightChanged(null);
        }
    }

    private void HandleHighlightChanged(GameObject target)
    {
        _target = target != null ? target.transform : null;
        _targetCollider = target != null ? target.GetComponent<Collider>() : null;

        if (_prompt == null) return;
        if (_target == null)
        {
            _prompt.RemoveFromClassList(VisibleClass);
            return;
        }

        if (_actionLabel != null)
        {
            ItemWorld item = target.GetComponent<ItemWorld>();
            string itemName = item != null && item.donnees != null ? item.donnees.nameObject : null;
            _actionLabel.text = string.IsNullOrEmpty(itemName) ? actionText : $"{actionText} : {itemName}";
        }

        UpdatePosition();
        _prompt.AddToClassList(VisibleClass);
    }

    private void LateUpdate()
    {
        if (_target != null && _target.gameObject.activeInHierarchy)
        {
            UpdatePosition();
        }
        else if (_prompt != null && _prompt.ClassListContains(VisibleClass))
        {
            // Objet ramassé ou détruit sans nouvel événement : on cache l'invite.
            _target = null;
            _targetCollider = null;
            _prompt.RemoveFromClassList(VisibleClass);
        }
    }

    private void UpdatePosition()
    {
        if (_prompt == null || _prompt.panel == null) return;
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        Vector3 worldPosition;
        if (_targetCollider != null)
        {
            Bounds bounds = _targetCollider.bounds;
            worldPosition = new Vector3(bounds.center.x, bounds.max.y + heightAboveObject, bounds.center.z);
        }
        else
        {
            worldPosition = _target.position + Vector3.up * heightAboveObject;
        }

        // Derrière la caméra : on cache l'invite (sinon elle apparaît en miroir).
        bool inFront = _camera.WorldToViewportPoint(worldPosition).z > 0f;
        _prompt.style.display = inFront ? DisplayStyle.Flex : DisplayStyle.None;
        if (!inFront) return;

        Vector2 panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(_prompt.panel, worldPosition, _camera);
        _prompt.style.left = panelPosition.x;
        _prompt.style.top = panelPosition.y;
    }
}
