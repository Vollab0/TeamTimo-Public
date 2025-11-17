using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq; // Nécessaire pour utiliser .ToList()

public class AttraperObjets : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0.0f, 2f, 1.5f);
    [SerializeField] public float detectionRange = 2f;
    [SerializeField] public Material outlineMaterial;

    private GameObject objetEnMain = null;
    private Rigidbody rbObjetEnMain = null;

    private GameObject highlightedObject = null; // L'objet actuellement en surbrillance
    private CharacterController characterController; // Cache pour le CharacterController

    private void Start()
    {
        // Met en cache le CharacterController pour de meilleures performances
        // TryGetComponent ne provoquera pas d'erreur s'il n'est pas trouvé
        TryGetComponent<CharacterController>(out characterController);
    }

    public void Update()
    {
        // Gère la mise à jour de l'objet en surbrillance
        Outliner();
    }

    // Appelé par le Player Input
    public void AttraperObjet(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            if (objetEnMain == null)
            {
                // On essaie de ramasser l'objet en surbrillance
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
        // On ne peut ramasser que s'il y a un objet en surbrillance
        if (highlightedObject != null)
        {
            // Récupère l'objet et le rend enfant du joueur
            objetEnMain = highlightedObject;
            rbObjetEnMain = objetEnMain.GetComponent<Rigidbody>();
            objetEnMain.transform.SetParent(transform);

            // Ajuste la position de l'objet par rapport au joueur
            objetEnMain.transform.localPosition = offset;

            // On veut que la rotation soit aussi celle du joueur
            objetEnMain.transform.localRotation = Quaternion.identity;

            rbObjetEnMain.isKinematic = true;

            // On retire l'outline car on tient l'objet
            RemoveOutline(objetEnMain);
            highlightedObject = null; // On ne surligne plus rien
        }
    }

    public void LacherObjet()
    {
        if (objetEnMain == null) return; // Sécurité

        try
        {
            // 1. On détache l'objet
            objetEnMain.transform.SetParent(null);

            // 2. On réactive la physique
            rbObjetEnMain.isKinematic = false;

            // 3. On applique la vélocité (la ligne qui cause l'erreur)
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                rbObjetEnMain.linearVelocity = cc.velocity;
            }
            else
            {
                // Avertissement si vous avez oublié le CharacterController
                Debug.LogWarning("Impossible de transférer la vélocité : CharacterController non trouvé !");
            }
        }
        catch (System.Exception e)
        {
            // Attrape n'importe quelle erreur (comme MissingComponent) et l'affiche
            Debug.LogError($"Erreur en lâchant l'objet : {e.Message}");
        }
        finally
        {
            // 4. CECI SERA EXÉCUTÉ MÊME SI LE "try" PLANTE
            // C'est la correction la plus importante.
            objetEnMain = null;
            rbObjetEnMain = null;
        }
    }

    public bool IsHolding()
    {
        return objetEnMain != null; // Simplifié
    }

    private void Outliner()
    {
        // Si on tient un objet, on ne cherche pas à en surligner un autre
        if (objetEnMain != null)
        {
            // S'assure qu'on ne surligne rien par erreur
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

        // Parcourt les objets détectés pour trouver le PLUS PROCHE
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

        // CORRIGÉ : Logique anti-clignotement
        // On ne met à jour l'outline QUE si l'objet visé a changé
        if (objetAViser != highlightedObject)
        {
            // 1. Retirer l'outline de l'ancien objet (s'il y en avait un)
            if (highlightedObject != null)
            {
                RemoveOutline(highlightedObject);
            }

            // 2. Ajouter l'outline au nouvel objet (s'il y en a un)
            if (objetAViser != null)
            {
                AddOutline(objetAViser);
            }

            // 3. Mettre à jour la référence
            highlightedObject = objetAViser;
        }
    }

    // CORRIGÉ : Ajoute l'effet outliner en toute sécurité
    private void AddOutline(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return; // Pas de renderer, on ne peut rien faire

        // Convertit le tableau de matériaux en Liste
        var materials = new List<Material>(renderer.materials);

        // Ajoute l'outline SEULEMENT s'il n'est pas déjà présent
        if (!materials.Contains(outlineMaterial))
        {
            materials.Add(outlineMaterial);
            renderer.materials = materials.ToArray(); // Reconvertit la liste en tableau
        }
    }

    // CORRIGÉ : Supprime l'effet outliner en toute sécurité
    private void RemoveOutline(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;

        var materials = new List<Material>(renderer.materials);

        // Tente de supprimer le matériau d'outline. 
        // .Remove() renvoie 'true' si la suppression a réussi.
        if (materials.Remove(outlineMaterial))
        {
            renderer.materials = materials.ToArray();
        }
    }
}