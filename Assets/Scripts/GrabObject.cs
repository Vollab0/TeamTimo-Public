using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

public class GrabObject : MonoBehaviour
{
    [Header("System References")]
    // On ne parle plus à l'UI, mais au cerveau (le système)
    [SerializeField] private InventorySystem inventorySystem;

    [Header("TPS References")]
    [SerializeField] private Transform objectHolder; // Ancien 'porteObjet'
    [SerializeField] private Camera playerCamera;

    [Header("Adjustments")]
    [SerializeField] public Vector3 handOffset = new Vector3(0f, 1.5f, 2f);
    [SerializeField] private Vector3 handRotation = Vector3.zero;

    private CharacterController characterController;

    [Header("Settings")]
    [SerializeField] public float detectionRange = 2f;

    [Header("Visual Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color gizmoColor = Color.yellow;

    // État interne
    private GameObject currentVisualObject = null; // Ancien 'objetVisuelEnMain'
    private GameObject highlightedObject = null;

    private void Start()
    {
        TryGetComponent<CharacterController>(out characterController);
        if (playerCamera == null) playerCamera = Camera.main;

        // 1. Recherche automatique du système si non assigné
        if (inventorySystem == null)
            inventorySystem = FindFirstObjectByType<InventorySystem>();

        // 2. Abonnement aux événements (La magie opère ici)
        if (inventorySystem != null)
        {
            // Si on scroll la molette -> On met à jour la main
            inventorySystem.OnSelectionChanged += OnSelectionChangedCallback;
            // Si on ajoute/retire un item -> On met à jour la main (au cas où on jette l'item tenu)
            inventorySystem.OnInventoryChanged += UpdateHandVisuals;
            
            // Premier affichage au lancement
            UpdateHandVisuals();
        }
        else
        {
            Debug.LogError("ERROR: InventorySystem not found in the scene!");
        }
    }

    private void OnDisable()
    {
        // Toujours se désabonner pour éviter les erreurs quand on quitte/change de scène
        if (inventorySystem != null)
        {
            inventorySystem.OnSelectionChanged -= OnSelectionChangedCallback;
            inventorySystem.OnInventoryChanged -= UpdateHandVisuals;
        }
    }

    private void Update()
    {
        // Plus besoin de vérifier le slot ici, les events s'en chargent !
        HandleOutlining();
    }

    // Petit wrapper car l'event renvoie un int, mais notre fonction n'en a pas besoin
    private void OnSelectionChangedCallback(int index) => UpdateHandVisuals();

    public void UpdateHandVisuals()
    {
        // 1. Nettoyage de l'objet précédent
        if (currentVisualObject != null)
        {
            Destroy(currentVisualObject);
            currentVisualObject = null;
        }

        if (inventorySystem == null) return;

        // 2. Récupération de l'item actuel via le système
        ItemData item = inventorySystem.GetCurrentItem();

        // 3. Instanciation du visuel
        // ATTENTION : Vérifie si ton ItemData utilise 'prefab' ou 'prefab3D'. J'utilise 'prefab' ici.
        if (item != null && item.prefab != null) 
        {
            currentVisualObject = Instantiate(item.prefab, objectHolder);
            currentVisualObject.transform.localPosition = handOffset;
            currentVisualObject.transform.localRotation = Quaternion.Euler(handRotation);

            // Désactivation de la physique pour l'objet en main
            Rigidbody rb = currentVisualObject.GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;
            
            Collider col = currentVisualObject.GetComponent<Collider>();
            if (col) col.enabled = false;
        }
    }

    // À lier dans l'Input System
    public void OnGrabInput(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            // Si on vise quelque chose, on essaie de le prendre
            if (TryPickup()) return;
            
            // Sinon, si on tient quelque chose, on le lâche
            if (currentVisualObject != null) DropObject();
        }
    }

    private bool TryPickup()
    {
        if (highlightedObject == null) return false;

        ItemWorld itemWorld = highlightedObject.GetComponent<ItemWorld>();

        if (itemWorld != null && itemWorld.donnees != null)
        {
            if (inventorySystem == null) return false;

            // Utilisation de la nouvelle méthode AddItem
            bool wasAdded = inventorySystem.AddItem(itemWorld.donnees);

            if (wasAdded)
            {
                RemoveOutline(highlightedObject);
                Destroy(highlightedObject);
                highlightedObject = null;

                // Pas besoin d'appeler UpdateHandVisuals() ici manuellement,
                // car AddItem déclenche l'event OnInventoryChanged qui va le faire tout seul !
                return true;
            }
        }
        return false;
    }

    private void DropObject()
    {
        if (inventorySystem == null) return;
        if (currentVisualObject == null) return;

        // --- CORRECTION DU BUG DE DISPARITION ---

        // 1. On sauvegarde l'objet dans une variable temporaire pour ne pas le perdre
        GameObject objectToThrow = currentVisualObject;

        // 2. On "vide" la main virtuelle TOUT DE SUITE.
        // Comme ça, quand l'inventaire se mettra à jour dans une milliseconde, 
        // il pensera que la main est déjà vide et ne tentera pas de détruire l'objet.
        currentVisualObject = null;

        // 3. On récupère les données de l'item pour les remettre dans l'objet au sol
        ItemData currentData = inventorySystem.GetCurrentItem();

        // 4. On retire l'item de l'inventaire (ceci déclenche l'événement UpdateHandVisuals)
        inventorySystem.RemoveItem(inventorySystem.SelectedSlotIndex);
        
        // ----------------------------------------

        // 5. On détache l'objet du joueur (Parenting)
        objectToThrow.transform.SetParent(null);
        objectToThrow.tag = "Ramassable"; 

        // 6. On remet le script ItemWorld pour qu'il soit ramassable
        ItemWorld itemWorld = objectToThrow.GetComponent<ItemWorld>();
        if (itemWorld == null) itemWorld = objectToThrow.AddComponent<ItemWorld>();
        
        if (currentData != null)
        {
            itemWorld.donnees = currentData;
        }

        // 7. On réactive la physique
        Rigidbody rb = objectToThrow.GetComponent<Rigidbody>();
        Collider col = objectToThrow.GetComponent<Collider>();

        if (col) 
        {
            col.enabled = true;
            // Astuce : On décale légèrement l'objet pour qu'il ne soit pas coincé DANS le joueur
            // Sinon la physique peut le faire disparaître ou le propulser à l'infini
            objectToThrow.transform.position += playerCamera.transform.forward * 0.5f;
        }

        if (rb)
        {
            rb.isKinematic = false;
            // Utilise 'linearVelocity' si tu es sur Unity 6, sinon 'velocity'
            rb.linearVelocity = playerCamera.transform.forward * 5f; 
            
            if (characterController != null) 
                rb.linearVelocity += characterController.velocity;
        }
    }

    private void HandleOutlining()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRange);
        GameObject objectToHighlight = null;
        float minDistance = float.MaxValue;

        foreach (Collider collider in colliders)
        {
            if (collider.CompareTag("Ramassable"))
            {
                float distance = Vector3.Distance(transform.position, collider.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    objectToHighlight = collider.gameObject;
                }
            }
        }

        if (objectToHighlight != highlightedObject)
        {
            if (highlightedObject != null) RemoveOutline(highlightedObject);
            if (objectToHighlight != null) AddOutline(objectToHighlight);
            highlightedObject = objectToHighlight;
        }
    }

    private void AddOutline(GameObject obj)
    {
        if (GameManager.Instance == null) return;
        Material mat = GameManager.Instance.globalOutlineMaterial;

        Renderer r = obj.GetComponent<Renderer>();
        if (r == null) return;

        var m = r.sharedMaterials.ToList();
        if (!m.Contains(mat))
        {
            m.Add(mat);
            r.sharedMaterials = m.ToArray();
        }
    }

    private void RemoveOutline(GameObject obj)
    {
        if (GameManager.Instance == null) return;
        Material mat = GameManager.Instance.globalOutlineMaterial;

        Renderer r = obj.GetComponent<Renderer>();
        if (r == null) return;

        var m = r.sharedMaterials.ToList();
        if (m.Remove(mat))
        {
            r.sharedMaterials = m.ToArray();
        }
    }

    public bool IsHolding() { return currentVisualObject != null; }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}