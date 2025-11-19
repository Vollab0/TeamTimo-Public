using UnityEngine;
using UnityEngine.InputSystem;

public class SlotSelection : MonoBehaviour
{
    [SerializeField] private InventaireUI inventaireUI;
    public int slotActif { get; private set; } = 0;

    public void ChangerSlot(InputAction.CallbackContext context)
    {
        // On vérifie que l'action est bien "Performed" (l'action a eu lieu)
        if (context.performed)
        {
            // CORRECTION ICI : On lit un Vector2, pas un float !
            // La molette standard envoie un vecteur (x, y). On veut le Y.
            Vector2 scrollVector = context.ReadValue<Vector2>();
            float scrollDelta = scrollVector.y;

            // --- Le reste de votre logique reste identique ---

            int direction = 0;
            if (scrollDelta > 0) direction = -1;      // Vers le haut (Slot précédent)
            else if (scrollDelta < 0) direction = 1;  // Vers le bas (Slot suivant)

            // Si la souris n'a pas bougé, on ne fait rien
            if (direction == 0) return;

            int nouveauSlot = slotActif + direction;

            // Boucle infinie (0 -> 4 -> 0...)
            if (nouveauSlot < 0)
            {
                nouveauSlot = inventaireUI.nombreDeSlots - 1;
            }
            else if (nouveauSlot >= inventaireUI.nombreDeSlots)
            {
                nouveauSlot = 0;
            }

            // Mise à jour seulement si ça a changé
            if (nouveauSlot != slotActif)
            {
                slotActif = nouveauSlot;
                inventaireUI.MettreAJourSelection(slotActif);
            }
        }
    }
}