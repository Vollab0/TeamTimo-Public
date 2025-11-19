using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq; // Nécessaire pour List<T>.ToList()

public class AttraperObjets : MonoBehaviour
{
    // Références pour l'inventaire
    [Header("Déploiement")]
    [SerializeField] private SlotSelection slotSelection;
    [SerializeField] private InventaireUI inventaireUI;
    [SerializeField] private Transform pointDeDeployment;

    [Header("Inventaire")]
    [SerializeField] private InventaireUI inventaire; // Glissez ici l'objet SystèmeInventaire !

    // Paramètres physiques (conservés pour la tenue d'objet si l'inventaire est plein)
    [Header("Physique")]
    [SerializeField] private Vector3 offset = new Vector3(0.0f, 2f, 1.5f);
    [SerializeField] public float detectionRange = 2f;
    [SerializeField] public Material outlineMaterial;

    private GameObject objetEnMain = null;
    private Rigidbody rbObjetEnMain = null;
    private GameObject highlightedObject = null;
    private CharacterController characterController;

    private void Start()
    {
        TryGetComponent<CharacterController>(out characterController);
    }

    public void Update()
    {
        Outliner();
    }

    public void AttraperObjet(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            // 1. Si on tient déjà un objet, on le lâche.
            if (objetEnMain != null)
            {
                LacherObjet();
                return; // ON S'ARRÊTE ICI.
            }

            // 2. On essaie d'interagir avec le monde (Stocker OU Tenir physiquement)
            // La fonction RamasserObjet() retourne VRAI si elle a fait une action.
            if (RamasserObjet())
            {
                // Une action a été faite dans le monde (stockage ou tenue),
                // DONC on ne doit pas déployer depuis l'inventaire.
                return; // ON S'ARRÊTE ICI.
            }

            // 3. Si on arrive ici, c'est qu'on ne tient rien ET qu'on n'a rien ramassé dans le monde.
            // ALORS on essaie de déployer l'objet actif du slot.
            EquiperObjetDepuisSlot();
        }
    }

    // AJOUTEZ CETTE FONCTION À LA FIN DU SCRIPT !
    void EquiperObjetDepuisSlot()
    {
        // Ne pas continuer si les références ne sont pas faites
        if (slotSelection == null || inventaireUI == null) return;

        // 1. Récupérer l'ItemData et le retirer de l'inventaire
        int indexActif = slotSelection.slotActif;
        ItemData donneesADeployer = inventaireUI.RetirerObjet(indexActif); // Nécessite la fonction RetirerObjet dans InventaireUI

        if (donneesADeployer != null)
        {
            // 2. Charger le Prefab depuis le dossier Resources (le nom du prefab doit être le nom du ScriptableObject)
            string prefabName = donneesADeployer.name;
            GameObject prefab = Resources.Load<GameObject>(prefabName);

            if (prefab != null)
            {
                // 3. Instancier l'objet 3D et l'attraper
                GameObject nouvelObjet = Instantiate(prefab, pointDeDeployment.position, Quaternion.identity, transform);

                // Reprise de la logique d'attrapage physique
                objetEnMain = nouvelObjet;
                rbObjetEnMain = nouvelObjet.GetComponent<Rigidbody>();

                if (rbObjetEnMain != null)
                {
                    rbObjetEnMain.isKinematic = true;
                    objetEnMain.transform.localPosition = offset;
                    objetEnMain.transform.localRotation = Quaternion.identity;
                }
            }
        }
    }

    // ATTENTION : Changer 'void' en 'public bool'
    public bool RamasserObjet()
    {
        // Si rien n'est visé, on retourne immédiatement Faux
        if (highlightedObject == null) return false;

        // On récupère les composants nécessaires sur l'objet ciblé
        ItemWorld itemWorld = highlightedObject.GetComponent<ItemWorld>();
        Rigidbody rb = highlightedObject.GetComponent<Rigidbody>();

        // --- LOGIQUE INVENTAIRE (Priorité) ---
        if (itemWorld != null && itemWorld.donnees != null && inventaire != null)
        {
            // Tente d'ajouter à l'inventaire
            bool aEteAjoute = inventaire.AjouterObjet(itemWorld.donnees);

            if (aEteAjoute)
            {
                // Succès : Détruit l'objet physique et retourne Vrai
                RemoveOutline(highlightedObject);
                Destroy(highlightedObject);
                highlightedObject = null;
                return true; // <--- SUCCÈS : Stockage effectué
            }
        }

        // --- LOGIQUE DE TENUE PHYSIQUE (Si l'inventaire est plein ou l'objet n'a pas ItemData) ---
        if (rb != null)
        {
            // ... (votre code existant pour la tenue physique) ...

            RemoveOutline(objetEnMain);
            highlightedObject = null;
            return true; // <--- SUCCÈS : Tenue physique effectuée
        }

        // Si aucune action n'a eu lieu (rien trouvé, inventaire plein + pas de Rigidbody, etc.)
        return false; // <--- ÉCHEC : Aucune action effectuée
    }

    // La logique de lâcher doit rester pour les objets TENUS physiquement
    public void LacherObjet()
    {
        if (objetEnMain == null) return;

        try
        {
            objetEnMain.transform.SetParent(null);
            rbObjetEnMain.isKinematic = false;

            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                rbObjetEnMain.linearVelocity = cc.velocity;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Erreur en lâchant l'objet : {e.Message}");
        }
        finally
        {
            objetEnMain = null;
            rbObjetEnMain = null;
        }
    }

    // --- Fonctions utilitaires Outliner (non modifiées) ---
    public bool IsHolding()
    {
        return objetEnMain != null;
    }

    private void Outliner()
    {
        if (objetEnMain != null)
        {
            if (highlightedObject != null)
            {
                RemoveOutline(highlightedObject);
                highlightedObject = null;
            }
            return;
        }

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
            if (highlightedObject != null)
            {
                RemoveOutline(highlightedObject);
            }

            if (objetAViser != null)
            {
                AddOutline(objetAViser);
            }
            highlightedObject = objetAViser;
        }
    }

    private void AddOutline(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;

        var materials = new List<Material>(renderer.materials);

        if (!materials.Contains(outlineMaterial))
        {
            materials.Add(outlineMaterial);
            renderer.materials = materials.ToArray();
        }
    }

    private void RemoveOutline(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;

        var materials = new List<Material>(renderer.materials);

        if (materials.Remove(outlineMaterial))
        {
            renderer.materials = materials.ToArray();
        }
    }
}