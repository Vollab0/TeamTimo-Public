using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    [Header("Settings")]
    public int damageAmount = 1;
    public bool useTriggerCollision = true;

    // --- Trigger 3D ---
    private void OnTriggerEnter(Collider other)
    {
        if (useTriggerCollision && other.CompareTag("Player"))
        {
            // On envoie la position de cet objet (la source du dégât)
            TryDealDamage(other.gameObject, transform.position);
        }
    }

    // --- Collision 3D ---
    private void OnCollisionEnter(Collision collision)
    {
        if (!useTriggerCollision && collision.gameObject.CompareTag("Player"))
        {
            // On envoie la position de cet objet (la source du dégât)
            TryDealDamage(collision.gameObject, transform.position);
        }
    }

    // On ajoute un paramètre 'damageSourcePosition'
    private void TryDealDamage(GameObject target, Vector3 damageSourcePosition)
    {
        // Multijoueur : les dégâts passent par l'hôte vers la machine du joueur touché.
        PlayerDamageReceiver playerHealth = target.GetComponent<PlayerDamageReceiver>();

        if (playerHealth != null)
        {
            playerHealth.ReceiveDamage(damageAmount, damageSourcePosition); 
            // Debug.Log($"AÏE ! Le joueur a pris {damageAmount} dégâts.");
        }
    }
}