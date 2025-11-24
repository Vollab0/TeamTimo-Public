using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

public class AttraperObjets : MonoBehaviour
{
    [Header("Références TPS")]
    // C'est l'objet vide que tu as créé DANS ton joueur (au niveau du ventre/main)
    [SerializeField] private Transform porteObjet;

    // On garde la caméra JUSTE pour savoir où viser quand on jette
    [SerializeField] private Camera playerCamera;

    [Header("Ajustements")]
    // Règle ça à 0,0,0 pour commencer, puis ajuste si l'objet est trop haut/bas
    // Remplacez X, Y, Z par vos chiffres. N'oubliez pas le 'f' après les nombres décimaux.
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
            // --- CHANGEMENT TPS ---
            // On instancie l'objet sur le PORTE-OBJET (Le joueur)
            // Donc l'objet bougera avec le joueur, pas la caméra.
            objetVisuelEnMain = Instantiate(item.prefab, porteObjet);

            // Position locale (par rapport au joueur)
            objetVisuelEnMain.transform.localPosition = offsetMain;
            objetVisuelEnMain.transform.localRotation = Quaternion.Euler(rotationMain);

            // Désactivation Physique
            Rigidbody rb = objetVisuelEnMain.GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;

            Collider col = objetVisuelEnMain.GetComponent<Collider>();
            if (col) col.enabled = false;
        }
    }

    // --- LOGIQUE D'ACTION (TOUCHE E) ---

    public void AttraperObjet(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            // 1. Ramasser
            if (TenterRamassage()) return;

            // 2. Jeter
            if (objetVisuelEnMain != null)
            {
                JeterObjet();
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

    private void JeterObjet()
    {
        inventaireUI.RetirerObjet(slotSelection.slotActif);

        // On détache l'objet du joueur
        objetVisuelEnMain.transform.SetParent(null);

        // Réactivation Physique
        Rigidbody rb = objetVisuelEnMain.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = false;

            // ASTUCE TPS : On lance dans la direction de la CAMÉRA (pour viser)
            // Même si l'objet part du corps.
            Vector3 directionLancer = playerCamera.transform.forward;
            rb.AddForce(directionLancer * 10f, ForceMode.Impulse); // J'ai augmenté la force un peu

            if (characterController != null) rb.linearVelocity += characterController.velocity;
        }

        Collider col = objetVisuelEnMain.GetComponent<Collider>();
        if (col) col.enabled = true;

        objetVisuelEnMain = null;

        MettreAJourVisuelMain();
    }

    // --- OUTLINER (Inchangé) ---
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
        var m = new List<Material>(r.materials);
        if (!m.Contains(outlineMaterial)) { m.Add(outlineMaterial); r.materials = m.ToArray(); }
    }

    private void RemoveOutline(GameObject obj)
    {
        Renderer r = obj.GetComponent<Renderer>();
        if (r == null) return;
        var m = new List<Material>(r.materials);
        if (m.Remove(outlineMaterial)) r.materials = m.ToArray();
    }

    // Fonctions pour les autres scripts
    public bool IsHolding()
    {
        return objetVisuelEnMain != null;
    }

    public void LacherObjet()
    {
        if (objetVisuelEnMain != null)
        {
            JeterObjet();
        }
    }
}