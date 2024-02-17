using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Life : MonoBehaviour
{

    public HealthBar healthbar;

    float health;
    float maxHealth = 100f;

    public bool debugTakeDamage;

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
        if (debugTakeDamage) 
        {
            debugTakeDamage = false;

            health -= Random.Range(5f, 10f);

            if (health < 0f)
                health = maxHealth;

            healthbar.SetHealth(health, maxHealth);
        }
    }
}
