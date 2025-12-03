using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class InventaireUI : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] public int nombreDeSlots = 5;

    // Mémoire interne
    private List<VisualElement> slots = new List<VisualElement>();
    private ItemData[] contenuInventaire;
    private UIDocument uiDoc;

    // Gestion de la sélection
    private VisualElement slotActifElement = null;
    private string classeSelection = "slot-actif";

    void OnEnable()
    {
        uiDoc = GetComponent<UIDocument>();
        if (uiDoc == null) uiDoc = FindFirstObjectByType<UIDocument>();

        // Si l'UI n'est pas encore prête, on attend
        if (uiDoc == null) return;

        var root = uiDoc.rootVisualElement;

        // 1. Initialiser la mémoire
        contenuInventaire = new ItemData[nombreDeSlots];

        // 2. Trouver le conteneur dans l'UXML
        var container = root.Q<VisualElement>("InventoryContainer");

        if (container == null)
        {
            Debug.LogError("ERREUR CRITIQUE : Je ne trouve pas 'InventoryContainer' dans le UXML !");
            return;
        }

        // 3. Nettoyer le conteneur (au cas où il y a des restes)
        container.Clear();
        slots.Clear();

        // 4. Générer les slots
        for (int i = 0; i < nombreDeSlots; i++)
        {
            VisualElement slot = new VisualElement();
            slot.AddToClassList("slot"); // Utilise le style défini dans le CSS

            VisualElement icon = new VisualElement();
            icon.AddToClassList("slot-icon");
            slot.Add(icon);

            container.Add(slot);
            slots.Add(icon); // On garde l'icône en mémoire pour changer l'image
        }
    }

    // --- CORRECTION DU CRASH ICI ---
    public void MettreAJourSelection(int nouveauSlotIndex)
    {
        // SECURITE : Si la liste est vide ou l'index invalide, on annule tout de suite.
        if (slots == null || slots.Count == 0) return;
        if (nouveauSlotIndex < 0 || nouveauSlotIndex >= slots.Count) return;

        // 1. Retirer la sélection de l'ancien slot
        if (slotActifElement != null)
        {
            slotActifElement.RemoveFromClassList(classeSelection);
        }

        // 2. Appliquer la sélection au nouvel élément
        // slots[i] est l'icone, donc on prend son parent (le cadre du slot)
        slotActifElement = slots[nouveauSlotIndex].parent;
        slotActifElement.AddToClassList(classeSelection);
    }

    public bool AjouterObjet(ItemData item)
    {
        for (int i = 0; i < contenuInventaire.Length; i++)
        {
            if (contenuInventaire[i] == null)
            {
                contenuInventaire[i] = item;
                // Mise à jour visuelle
                if (i < slots.Count)
                {
                    slots[i].style.backgroundImage = new StyleBackground(item.icone);
                }
                return true;
            }
        }
        Debug.Log("Inventaire plein !");
        return false;
    }

    public ItemData RetirerObjet(int index)
    {
        if (index < 0 || index >= contenuInventaire.Length) return null;

        ItemData itemRetire = contenuInventaire[index];

        if (itemRetire != null)
        {
            contenuInventaire[index] = null;
            if (index < slots.Count)
            {
                slots[index].style.backgroundImage = new StyleBackground((Sprite)null);
            }
            return itemRetire;
        }
        return null;
    }

    public ItemData GetItemData(int index)
    {
        if (index < 0 || index >= contenuInventaire.Length) return null;
        return contenuInventaire[index];
    }
}