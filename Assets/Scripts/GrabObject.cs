using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

public class GrabObject : MonoBehaviour
{
    [Header("Références TPS")]
    [SerializeField] private Transform porteObjet;
    [SerializeField] private Camera playerCamera;

    [Header("Ajustements")]
    [SerializeField] public Vector3 offsetMain = new Vector3(0f, 1.5f, 2f);
    [SerializeField] private Vector3 rotationMain = Vector3.zero;

    private SlotSelection slotSelection;
    private InventaireUI inventaireUI;
    private CharacterController characterController;

    [Header("Paramètres")]
    [SerializeField] public float detectionRange = 2f;
    // NOTE : Plus besoin de mettre le matériau ici, on utilise celui du GameManager

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
        if (objetVisuelEnMain != null)
        {
            Destroy(objetVisuelEnMain);
            objetVisuelEnMain = null;
        }

        ItemData item = inventaireUI.GetItemData(slotSelection.slotActif);

        if (item != null && item.prefab != null)
        {
            objetVisuelEnMain = Instantiate(item.prefab, porteObjet);
            objetVisuelEnMain.transform.localPosition = offsetMain;
            objetVisuelEnMain.transform.localRotation = Quaternion.Euler(rotationMain);

            Rigidbody rb = objetVisuelEnMain.GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;
            Collider col = objetVisuelEnMain.GetComponent<Collider>();
            if (col) col.enabled = false;
        }
    }

    public void AttraperObjet(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            if (TenterRamassage()) return;
            if (objetVisuelEnMain != null) RelacherObjetPhysiquement();
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
                // IMPORTANT : On enlève proprement l'outline AVANT de détruire l'objet
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
        inventaireUI.RetirerObjet(slotSelection.slotActif);
        objetVisuelEnMain.transform.SetParent(null);

        Rigidbody rb = objetVisuelEnMain.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = false;
            rb.AddForce(playerCamera.transform.forward * 5f, ForceMode.Impulse);
            if (characterController != null) rb.linearVelocity += characterController.velocity;
        }

        Collider col = objetVisuelEnMain.GetComponent<Collider>();
        if (col) col.enabled = true;

        objetVisuelEnMain = null;
        MettreAJourVisuelMain();
    }

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

    // --- C'EST ICI QUE LA CORRECTION AGIT ---

    private void AddOutline(GameObject obj)
    {
        // 1. On va chercher le matériau "sûr" dans le GameManager
        if (GameManager.Instance == null) return;
        Material mat = GameManager.Instance.globalOutlineMaterial;

        Renderer r = obj.GetComponent<Renderer>();
        if (r == null) return;

        var m = r.sharedMaterials.ToList();
        // 2. On vérifie s'il est déjà là
        if (!m.Contains(mat))
        {
            m.Add(mat);
            r.sharedMaterials = m.ToArray();
        }
    }

    private void RemoveOutline(GameObject obj)
    {
        // 1. On va chercher le MEME matériau "sûr" dans le GameManager
        if (GameManager.Instance == null) return;
        Material mat = GameManager.Instance.globalOutlineMaterial;

        Renderer r = obj.GetComponent<Renderer>();
        if (r == null) return;

        var m = r.sharedMaterials.ToList();
        // 3. On l'enlève. Comme c'est la même référence (mat), ça marche à tous les coups.
        if (m.Remove(mat))
        {
            r.sharedMaterials = m.ToArray();
        }
    }

    public bool IsHolding() { return objetVisuelEnMain != null; }
    public void LacherObjet() { if (objetVisuelEnMain != null) RelacherObjetPhysiquement(); }
}