using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements; // INDISPENSABLE pour UI Toolkit

public class InventaireUI : MonoBehaviour
{
    private VisualElement slotActifElement = null; // Ajoutez cette variable si ce n'est pas déjà fait
    private string classeSelection = "slot-actif"; // Ajoutez cette variable si ce n'est pas déjà fait

    public void MettreAJourSelection(int nouveauSlotIndex)
    {
        // 1. Retirer la sélection de l'ancien slot
        if (slotActifElement != null)
        {
            slotActifElement.RemoveFromClassList(classeSelection);
        }

        // 2. Appliquer la sélection au nouvel élément
        // slots[i] est l'icone, donc on prend son parent, le slot lui-même
        slotActifElement = slots[nouveauSlotIndex].parent; // Ceci suppose que 'slots' contient des références aux icônes
        slotActifElement.AddToClassList(classeSelection);
    }

    [SerializeField] public int nombreDeSlots = 5;
    private List<VisualElement> slots = new List<VisualElement>();
    private UIDocument uiDoc;

    // Notre "mémoire" de l'inventaire (liste d'objets)
    private ItemData[] contenuInventaire;

    void OnEnable()
    {
        uiDoc = GetComponent<UIDocument>();
        // Si le script est sur un autre objet, trouvez le UI Document :
        if (uiDoc == null) uiDoc = FindFirstObjectByType<UIDocument>();

        var root = uiDoc.rootVisualElement;

        // On prépare la mémoire interne
        contenuInventaire = new ItemData[nombreDeSlots];

        // On cherche le conteneur créé dans l'étape précédente
        var container = root.Q<VisualElement>("Container"); // Assurez-vous que le nom est bon dans l'UXML

        // On génère les slots par le code (plus flexible)
        for (int i = 0; i < nombreDeSlots; i++)
        {
            VisualElement slot = new VisualElement();
            slot.AddToClassList("slot"); // Applique le style CSS .slot

            // On crée un élément vide pour l'icone future
            VisualElement icon = new VisualElement();
            icon.AddToClassList("slot-icon");
            slot.Add(icon);

            container.Add(slot);
            slots.Add(icon); // On garde la référence de l'icone pour la changer plus tard
        }
    }

    public bool AjouterObjet(ItemData item)
    {
        // 1. Chercher le premier slot vide
        for (int i = 0; i < contenuInventaire.Length; i++)
        {
            if (contenuInventaire[i] == null)
            {
                // 2. Sauvegarder la donnée
                contenuInventaire[i] = item;

                // 3. Mettre à jour l'UI (Afficher l'image)
                // En UI Toolkit, l'image de fond se change via le style
                slots[i].style.backgroundImage = new StyleBackground(item.icone);

                return true; // Objet ajouté avec succès
            }
        }

        Debug.Log("Inventaire plein !");
        return false;
    }

    public ItemData RetirerObjet(int index)
    {
        // 1. Vérification de sécurité de l'index
        if (index < 0 || index >= contenuInventaire.Length) return null;

        // 2. DÉCLARATION DE LA VARIABLE (La Ligne Manquante ou Mal Placée)
        // Cette ligne doit être déclarée ICI pour être accessible par la suite.
        ItemData itemRetire = contenuInventaire[index];

        // Si le slot n'est pas vide
        if (itemRetire != null)
        {
            // 3. Vider la case (logique de retrait de l'objet)
            contenuInventaire[index] = null;
            slots[index].style.backgroundImage = new StyleBackground((Sprite)null);

            // 4. RETOURNER LE PREMIER OBJET DÉCLARÉ
            return itemRetire;
        }

        // Si le slot était vide, on retourne null.
        return null;
    }
}