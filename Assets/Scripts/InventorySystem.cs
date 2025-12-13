using System;
using UnityEngine;

public class InventorySystem : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] public int inventorySize = 5;

    // --- DONNÉES ---
    private ItemData[] inventoryContent;
    public int SelectedSlotIndex { get; private set; } = 0;

    // --- ÉVÉNEMENTS ---
    public event Action OnInventoryChanged;
    public event Action<int> OnSelectionChanged;

    void Awake()
    {
        InitInventory();
    }

    private void Start()
    {
        NotifyChanges();
    }

    // Méthode d'initialisation sécurisée
    private void InitInventory()
    {
        if (inventoryContent == null)
        {
            inventoryContent = new ItemData[inventorySize];
        }
    }

    // --- LOGIQUE DE SÉLECTION ---

    public void ChangeSelection(int direction)
    {
        int newSlot = SelectedSlotIndex + direction;

        if (newSlot < 0) 
            newSlot = inventorySize - 1;
        else if (newSlot >= inventorySize) 
            newSlot = 0;

        if (newSlot != SelectedSlotIndex)
        {
            SelectedSlotIndex = newSlot;
            OnSelectionChanged?.Invoke(SelectedSlotIndex);
        }
    }

    // --- GESTION DES ITEMS ---

    public bool AddItem(ItemData item)
    {
        // SÉCURITÉ : On s'assure que le tableau existe
        InitInventory();

        for (int i = 0; i < inventoryContent.Length; i++)
        {
            if (inventoryContent[i] == null)
            {
                inventoryContent[i] = item;
                OnInventoryChanged?.Invoke();
                return true;
            }
        }
        Debug.Log("Inventory is full!");
        return false;
    }

    public ItemData RemoveItem(int index)
    {
        InitInventory(); // Sécurité

        if (index < 0 || index >= inventoryContent.Length) return null;

        ItemData item = inventoryContent[index];
        if (item != null)
        {
            inventoryContent[index] = null;
            OnInventoryChanged?.Invoke();
        }
        return item;
    }

    public ItemData GetCurrentItem()
    {
        InitInventory(); // Sécurité
        if (SelectedSlotIndex < 0 || SelectedSlotIndex >= inventoryContent.Length) return null;
        return inventoryContent[SelectedSlotIndex];
    }
    
    // --- C'EST ICI QUE TU AVAIS LE CRASH ---
    public ItemData GetItemAtIndex(int index)
    {
        // SÉCURITÉ : Si le tableau est null (pas encore prêt), on renvoie null sans planter
        if (inventoryContent == null) return null;

        if (index < 0 || index >= inventoryContent.Length) return null;
        return inventoryContent[index];
    }

    public void NotifyChanges()
    {
        // S'assurer que tout est prêt avant de notifier
        InitInventory();
        OnInventoryChanged?.Invoke();
        OnSelectionChanged?.Invoke(SelectedSlotIndex);
    }
}