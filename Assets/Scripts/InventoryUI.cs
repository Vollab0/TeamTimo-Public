using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem; 

public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private string containerName = "InventoryContainer";

    // --- UI INTERNE ---
    private UIDocument uiDoc;
    private List<VisualElement> slotIcons = new List<VisualElement>();
    private VisualElement currentSelectionElement = null;
    private const string selectionClass = "slot-actif"; // Assure-toi que ce nom correspond à ton CSS

    void OnEnable()
    {
        // 1. Validation des dépendances
        if (inventorySystem == null) 
            inventorySystem = GetComponent<InventorySystem>();
            
        uiDoc = GetComponent<UIDocument>();
        if (uiDoc == null) uiDoc = FindFirstObjectByType<UIDocument>();

        if (uiDoc == null || inventorySystem == null)
        {
            Debug.LogError("InventoryUI: Missing UIDocument or InventorySystem!");
            return;
        }

        // 2. Génération visuelle
        GenerateInterface();

        // 3. Abonnement aux événements du système
        // C'est ici que la magie opère : quand le système change, l'UI se met à jour auto.
        inventorySystem.OnInventoryChanged += UpdateInventoryVisuals;
        inventorySystem.OnSelectionChanged += UpdateSelectionVisual;

        // 4. Force une première mise à jour pour être synchro
        inventorySystem.NotifyChanges();
    }

    void OnDisable()
    {
        // IMPORTANT : Toujours se désabonner quand l'objet est désactivé
        // sinon on risque des erreurs de mémoire ou des crashs.
        if (inventorySystem != null)
        {
            inventorySystem.OnInventoryChanged -= UpdateInventoryVisuals;
            inventorySystem.OnSelectionChanged -= UpdateSelectionVisual;
        }
    }

    // --- LOGIQUE UI TOOLKIT ---

    void GenerateInterface()
    {
        var root = uiDoc.rootVisualElement;
        var container = root.Q<VisualElement>(containerName);
        
        if (container == null) 
        {
            Debug.LogError($"InventoryUI: Container '{containerName}' not found in UXML.");
            return;
        }

        container.Clear();
        slotIcons.Clear();

        // On demande au système combien de slots il faut créer
        for (int i = 0; i < inventorySystem.inventorySize; i++)
        {
            VisualElement slotFrame = new VisualElement();
            slotFrame.AddToClassList("slot");

            VisualElement icon = new VisualElement();
            icon.AddToClassList("slot-icon");

            slotFrame.Add(icon);
            container.Add(slotFrame);
            slotIcons.Add(icon);
        }
    }

    // Appelé automatiquement via l'événement OnInventoryChanged
    void UpdateInventoryVisuals()
    {
        for (int i = 0; i < slotIcons.Count; i++)
        {
            ItemData data = inventorySystem.GetItemAtIndex(i);

            if (data != null)
                slotIcons[i].style.backgroundImage = new StyleBackground(data.icone);
            else
                slotIcons[i].style.backgroundImage = new StyleBackground((Sprite)null);
        }
    }

    // Appelé automatiquement via l'événement OnSelectionChanged
    void UpdateSelectionVisual(int newIndex)
    {
        if (slotIcons.Count == 0) return;

        // Sécurité index
        if (newIndex < 0) newIndex = 0;
        if (newIndex >= slotIcons.Count) newIndex = slotIcons.Count - 1;

        // Retirer la surbrillance précédente
        if (currentSelectionElement != null)
        {
            currentSelectionElement.RemoveFromClassList(selectionClass);
        }

        // Ajouter la nouvelle surbrillance (sur le parent/cadre)
        currentSelectionElement = slotIcons[newIndex].parent;
        currentSelectionElement.AddToClassList(selectionClass);
    }

    // --- INPUT SYSTEM (Molette) ---
    // À lier dans le PlayerInput > Events
    public void OnScrollInventory(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        float scrollValue = context.ReadValue<Vector2>().y;
        if (scrollValue == 0) return;

        int direction = scrollValue > 0 ? -1 : 1;

        // On envoie juste la commande au système, l'UI ne calcule rien !
        inventorySystem.ChangeSelection(direction);
    }
}