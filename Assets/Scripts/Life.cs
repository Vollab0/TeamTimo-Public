using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Life : MonoBehaviour
{
    public HealthBar healthbar;
    public Transform respawnPoint; // Point de réapparition

    float health;
    float maxHealth = 4f;
    bool debugTakeDamage = false; // Variable pour simuler les dégâts dans le débogage

    // Start is called before the first frame update
    void Awake()
    {
        health = maxHealth;
    }

    void Start()
    {
        healthbar.SetHealth(health, maxHealth);
    }

    // Update is called once per frame
    void Update()
    {
        // Simuler des dégâts pour le débogage
        if (debugTakeDamage)
        {
            TakeDamage();
            debugTakeDamage = false;
        }

        // Vérifier si la vie est inférieure à 1 et respawn si nécessaire
        if (health < 1f)
        {
            Respawn();
        }

        // Debug: Diminuer la santé lorsqu'on appuie sur la touche espace
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TakeDamage();
        }
    }

    // Fonction pour simuler des dégâts
    void TakeDamage()
    {
        health -= 1f;

        if (health < 1f)
            health = maxHealth;

        healthbar.SetHealth(health, maxHealth);
    }

    // Fonction de respawn
    void Respawn()
    {
        // Déplacer le joueur vers le point de réapparition
        
            transform.position = respawnPoint.position;
            health = maxHealth; // Réinitialiser la santé du joueur
            healthbar.SetHealth(health, maxHealth); // Mettre à jour la barre de vie
    
        
    }
}