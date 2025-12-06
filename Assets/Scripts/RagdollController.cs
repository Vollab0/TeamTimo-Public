using UnityEngine;

public class RagdollController : MonoBehaviour
{
    [Header("Références Principales")]
    public Animator mainAnimator;
    public CharacterController characterController;
    public PlayerController playerController;

    private Rigidbody[] _boneRigidbodies;
    private Collider[] _boneColliders;

    void Awake()
    {
        _boneRigidbodies = GetComponentsInChildren<Rigidbody>(true);
        _boneColliders = GetComponentsInChildren<Collider>(true);

        Rigidbody mainRB = GetComponent<Rigidbody>();
        if (mainRB != null)
            _boneRigidbodies = System.Array.FindAll(_boneRigidbodies, rb => rb != mainRB);
        
        Collider mainCollider = GetComponent<Collider>();
        if (mainCollider != null)
        {
            _boneColliders = System.Array.FindAll(_boneColliders, col => col != mainCollider);
            // Important pour éviter de s'envoler
            foreach (var boneCol in _boneColliders)
                 Physics.IgnoreCollision(mainCollider, boneCol, true);
        }

        DisableRagdoll();
    }

    public void DisableRagdoll()
    {
        if(mainAnimator) mainAnimator.enabled = true;
        if(characterController) characterController.enabled = true;
        if(playerController) playerController.enabled = true;

        foreach (var rb in _boneRigidbodies)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false; 
        }
        
        foreach (var col in _boneColliders)
        {
             if(col != characterController) col.enabled = false;
        }
    }

    // MODIFICATION : On accepte maintenant une force et une position d'impact
    public void EnableRagdoll(Vector3 impactForce, Vector3 impactPosition)
    {
        // 1. On coupe les contrôles
        if(mainAnimator) mainAnimator.enabled = false;
        if(characterController) characterController.enabled = false;
        if(playerController) playerController.enabled = false;

        // 2. On active la physique sur les os
        foreach (var rb in _boneRigidbodies)
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;
            rb.linearVelocity = Vector3.zero; // Reset pour éviter des mouvements bizarres
        }
        
        foreach (var col in _boneColliders)
        {
             if(col != characterController) col.enabled = true;
        }

        // 3. AJOUT : Appliquer la force pour le faire tomber
        // On trouve l'os le plus proche de l'impact
        Rigidbody closestBone = FindClosestBone(impactPosition);
        
        // Si on ne trouve rien (cas rare) ou si pas de force, on prend le premier os (souvent le bassin)
        if (closestBone == null && _boneRigidbodies.Length > 0) closestBone = _boneRigidbodies[0];

        if (closestBone != null && impactForce.magnitude > 0.1f)
        {
            // On applique une impulsion sèche sur cet os
            closestBone.AddForce(impactForce, ForceMode.Impulse);
            Debug.Log($"Ragdoll : Poussée appliquée sur {closestBone.name} pour le faire tomber !");
        }
    }

    // Utilitaire pour trouver l'os le plus proche
    private Rigidbody FindClosestBone(Vector3 hitPos)
    {
        Rigidbody closest = null;
        float minDistance = float.MaxValue;
        foreach (var rb in _boneRigidbodies)
        {
            float distance = Vector3.Distance(rb.position, hitPos);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = rb;
            }
        }
        return closest;
    }
}