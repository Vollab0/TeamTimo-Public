using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Life : MonoBehaviour
{
    float health;
    float maxHealth = 100f;

    public bool debugTakeDamage;

    // Start is called before the first frame update
    void Awake()
    {
        health = maxHealth;
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

            Debug.Log("Health:" +  health);
        }
    }
}
