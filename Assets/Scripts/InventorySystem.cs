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
    // Ces "Actions" permettent de prévenir l'UI (ou le Joueur) qu'il s'est passé quelque chose
    // sans que ce script ait besoin de connaître l'UI directement.
    public event Action OnInventoryChanged;          // Quand un objet est ajouté/retiré
    public event Action<int> OnSelectionChanged;     // Quand le slot actif change

    void Awake()
    {
        // Initialisation de la mémoire des données
        inventoryContent = new ItemData[inventorySize];
    }

    private void Start()
    {
        // Au démarrage, on notifie tout le monde de l'état initial
        NotifyChanges();
    }

    // --- LOGIQUE DE SÉLECTION ---

    public void ChangeSelection(int direction)
    {
        // Calcul du nouvel index
        int newSlot = SelectedSlotIndex + direction;

        // Gestion de la boucle (Wrapping) : Fin -> Début ou Début -> Fin
        if (newSlot < 0) 
            newSlot = inventorySize - 1;
        else if (newSlot >= inventorySize) 
            newSlot = 0;

        // Si l'index a changé, on applique et on prévient les abonnés
        if (newSlot != SelectedSlotIndex)
        {
            SelectedSlotIndex = newSlot;
            OnSelectionChanged?.Invoke(SelectedSlotIndex);
        }
    }

    // --- GESTION DES ITEMS ---

    public bool AddItem(ItemData item)
    {
        for (int i = 0; i < inventoryContent.Length; i++)
        {
            if (inventoryContent[i] == null)
            {
                inventoryContent[i] = item;
                // On prévient l'UI qu'il faut redessiner les icônes
                OnInventoryChanged?.Invoke();
                return true;
            }
        }
        Debug.Log("Inventory is full!");
        return false;
    }

    public ItemData RemoveItem(int index)
    {
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
        if (SelectedSlotIndex < 0 || SelectedSlotIndex >= inventoryContent.Length) return null;
        return inventoryContent[SelectedSlotIndex];
    }
    
    // Permet à l'UI de lire le contenu (lecture seule) pour l'affichage
    public ItemData GetItemAtIndex(int index)
    {
        if (index < 0 || index >= inventoryContent.Length) return null;
        return inventoryContent[index];
    }

    // Force l'envoi des événements (utile si l'UI se charge après le système)
    public void NotifyChanges()
    {
        OnInventoryChanged?.Invoke();
        OnSelectionChanged?.Invoke(SelectedSlotIndex);
    }
}