using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class AttraperObjets : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0.0f, 2f, 1.5f);
    private GameObject objetEnMain = null;
    private Rigidbody rbObjetEnMain = null;
    private GameObject highlightedObject;
    public float detectionRange = 2f;

    public Material outlineMaterial;

    public void Update()
    {
        Outliner();
    }
    public void AttraperObjet(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            if (objetEnMain == null)
            {
                RamasserObjet();
            }
            else
            {
                LacherObjet();
            }
        }
    }

    void RamasserObjet()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRange); // Crée une sphere de rayon 2 centrée sur le joueur et renvoie tous les colliders à l'interieur
        foreach (Collider col in colliders)
        {
            // Si l'objet est ramassable
            if (col.CompareTag("Ramassable"))
            {
                // Récupère l'objet et le rend enfant du joueur
                objetEnMain = col.gameObject;
                rbObjetEnMain = objetEnMain.GetComponent<Rigidbody>();
                objetEnMain.transform.SetParent(transform);

                // Ajuste la position de l'objet par rapport au joueur
                objetEnMain.transform.localPosition = offset;

                rbObjetEnMain.isKinematic = true;
                RemoveOutline(objetEnMain);
                break; // Sort de la boucle après avoir trouvé le premier objet ramassable
            }
        }
    }

    public void LacherObjet()
    {
        // Détache l'objet du joueur et lui rend sa physique
        if (objetEnMain != null)
        {
            objetEnMain.transform.SetParent(null);
            rbObjetEnMain.isKinematic = false;
            rbObjetEnMain.velocity = GetComponent<CharacterController>().velocity; // Transfère la vitesse du joueur à l'objet
            objetEnMain = null;
            rbObjetEnMain = null;
        }
    }

    public bool IsHolding()
    {
        return rbObjetEnMain != null;
    }

    private void Outliner()
    {

        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRange);

        // Réinitialise l'objet mis en surbrillance
        if (highlightedObject != null)
        {
            RemoveOutline(highlightedObject);
            highlightedObject = null;
        }

        // Parcourt les objets détectés
        foreach (Collider collider in colliders)
        {
            if (collider.gameObject != objetEnMain)
            {
                // Vérifie si le collider est un objet ramassable
                if (collider.CompareTag("Ramassable"))
                {
                    // Applique l'effet outliner à l'objet
                    AddOutline(collider.gameObject);
                    highlightedObject = collider.gameObject;
                }
            }
                
        }
    }

    // Ajoute l'effet outliner à l'objet spécifié
    private void AddOutline(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material[] materials = renderer.materials;
            Material[] newMaterials = new Material[2];
            for (int i = 0; i < materials.Length; i++)
            {
                newMaterials[i] = materials[i];
            }
            newMaterials[materials.Length] = outlineMaterial;
            renderer.materials = newMaterials;
        }
    }

    // Supprime l'effet outliner de l'objet spécifié
    private void RemoveOutline(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material[] materials = renderer.materials;
            Material[] newMaterials = new Material[1];
            for (int i = 0; i < newMaterials.Length; i++)
            {
                newMaterials[i] = materials[i];
            }
            renderer.materials = newMaterials;
        }
    }
}
