using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

public class AttraperObjets : MonoBehaviour
{
    [Header("Références TPS")]
    [SerializeField] private Transform porteObjet; // L'objet vide DANS le joueur
    [SerializeField] private Camera playerCamera;

    [Header("Ajustements")]
    [SerializeField] public Vector3 offsetMain = new Vector3(0f, 1.5f, 2f);
    [SerializeField] private Vector3 rotationMain = Vector3.zero;

    // Références automatisées
    private SlotSelection slotSelection;
    private InventaireUI inventaireUI;
    private CharacterController characterController;

    [Header("Paramètres")]
    [SerializeField] public float detectionRange = 2f;
    [SerializeField] public Material outlineMaterial;

    // --- VARIABLES INTERNES ---
    private GameObject objetVisuelEnMain = null;
    private GameObject highlightedObject = null;
    private int indexSlotPrecedent = -1;

    private void Start()
    {
        TryGetComponent<CharacterController>(out characterController);

        if (playerCamera == null) playerCamera = Camera.main;

        GameObject systeme = GameObject.Find("SystemeInventaire");
        if (systeme != null)
        {
            slotSelection = systeme.GetComponent<SlotSelection>();
            inventaireUI = systeme.GetComponent<InventaireUI>();
        }
    }

    private void Update()
    {
        Outliner();
        VerifierChangementDeSlot();
    }

    // --- LOGIQUE VISUELLE ---

    private void VerifierChangementDeSlot()
    {
        if (slotSelection == null) return;

        if (slotSelection.slotActif != indexSlotPrecedent)
        {
            indexSlotPrecedent = slotSelection.slotActif;
            MettreAJourVisuelMain();
        }
    }

    public void MettreAJourVisuelMain()
    {
        // 1. Nettoyage
        if (objetVisuelEnMain != null)
        {
            Destroy(objetVisuelEnMain);
            objetVisuelEnMain = null;
        }

        // 2. Récupération Données
        ItemData item = inventaireUI.GetItemData(slotSelection.slotActif);

        // 3. Création Visuelle
        if (item != null && item.prefab != null)
        {
            // TPS : Instanciation sur le porteObjet
            objetVisuelEnMain = Instantiate(item.prefab, porteObjet);

            objetVisuelEnMain.transform.localPosition = offsetMain;
            objetVisuelEnMain.transform.localRotation = Quaternion.Euler(rotationMain);

            // Correction Physique pour l'affichage
            Rigidbody rb = objetVisuelEnMain.GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;

            Collider col = objetVisuelEnMain.GetComponent<Collider>();
            if (col) col.enabled = false;
        }
    }

    // --- LOGIQUE D'ACTION SIMPLE (TOUT SUR E) ---

    public void AttraperObjet(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            // CAS 1 : On essaie de RAMASSER un objet au sol
            if (TenterRamassage())
            {
                return; // Si on a ramassé, on s'arrête là.
            }

            // CAS 2 : Si on n'a rien ramassé, on RELÂCHE l'objet qu'on tient
            if (objetVisuelEnMain != null)
            {
                RelacherObjetPhysiquement();
            }
        }
    }

    private bool TenterRamassage()
    {
        if (highlightedObject == null) return false;

        ItemWorld itemWorld = highlightedObject.GetComponent<ItemWorld>();

        if (itemWorld != null && itemWorld.donnees != null)
        {
            bool aEteAjoute = inventaireUI.AjouterObjet(itemWorld.donnees);

            if (aEteAjoute)
            {
                RemoveOutline(highlightedObject);
                Destroy(highlightedObject);
                highlightedObject = null;

                MettreAJourVisuelMain();
                return true;
            }
        }
        return false;
    }

    private void RelacherObjetPhysiquement()
    {
        // 1. On retire de l'UI
        inventaireUI.RetirerObjet(slotSelection.slotActif);

        // 2. On détache du joueur (il redevient indépendant)
        objetVisuelEnMain.transform.SetParent(null);

        // 3. Correction Echelle (Taille normale)
        //objetVisuelEnMain.transform.localScale = Vector3.one;

        // 4. Physique (Gravité)
        Rigidbody rb = objetVisuelEnMain.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = false;

            // Petite impulsion vers l'avant pour ne pas qu'il tombe dans nos pieds
            Vector3 directionLancer = playerCamera.transform.forward;
            rb.AddForce(directionLancer * 5f, ForceMode.Impulse);

            if (characterController != null) rb.linearVelocity += characterController.velocity;
        }

        Collider col = objetVisuelEnMain.GetComponent<Collider>();
        if (col) col.enabled = true;

        // 5. Nettoyage
        objetVisuelEnMain = null;
        MettreAJourVisuelMain();
    }

    // --- OUTLINER (Inchangé & Corrigé) ---

    private void Outliner()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRange);
        GameObject objetAViser = null;
        float distanceMin = float.MaxValue;

        foreach (Collider collider in colliders)
        {
            if (collider.CompareTag("Ramassable"))
            {
                float distance = Vector3.Distance(transform.position, collider.transform.position);
                if (distance < distanceMin)
                {
                    distanceMin = distance;
                    objetAViser = collider.gameObject;
                }
            }
        }

        if (objetAViser != highlightedObject)
        {
            if (highlightedObject != null) RemoveOutline(highlightedObject);
            if (objetAViser != null) AddOutline(objetAViser);
            highlightedObject = objetAViser;
        }
    }

    private void AddOutline(GameObject obj)
    {
        Renderer r = obj.GetComponent<Renderer>();
        if (r == null) return;
        var m = r.sharedMaterials.ToList();
        if (!m.Contains(outlineMaterial)) { m.Add(outlineMaterial); r.sharedMaterials = m.ToArray(); }
    }

    private void RemoveOutline(GameObject obj)
    {
        Renderer r = obj.GetComponent<Renderer>();
        if (r == null) return;
        var m = r.sharedMaterials.ToList();
        if (m.Remove(outlineMaterial)) r.sharedMaterials = m.ToArray();
    }

    // Fonctions externes
    public bool IsHolding() { return objetVisuelEnMain != null; }

    public void LacherObjet()
    {
        if (objetVisuelEnMain != null) RelacherObjetPhysiquement();
    }
}