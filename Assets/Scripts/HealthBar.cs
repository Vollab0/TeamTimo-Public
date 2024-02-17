using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBar : MonoBehaviour
{
    [Header("Components")]
    public RawImage healthbar;
    public TextMeshProUGUI txtHealth;
    public TextMeshProUGUI txtMaxHealth;

    RectTransform healthBarTransform;

    //Largeur max barre de vie 
    float healthbarWidthMax;
    float healthbarHeightMax;

    void Awake()
    {
        healthBarTransform = healthbar.GetComponent<RectTransform>();
    }


    // Start is called before the first frame update
    void Start()
    {
        //Assigner largeur max 
        Vector2 dimensions = healthbar.transform.parent.GetComponent<RectTransform>().sizeDelta;
        healthbarWidthMax = dimensions.x;
        healthbarHeightMax = dimensions.y;

        //Appliquer cette largeur à la barre de vie 
        healthBarTransform.sizeDelta = new Vector2(healthbarWidthMax, healthbarHeightMax);

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetHealth(float health, float maxHealth)
    {
        //Modifier valeurs des champs texte
        txtHealth.text = string.Format("{0:#0}", health);
        txtMaxHealth.text = string.Format("/ {0:#0}", maxHealth);

        //Appliquer valeur de la vie dans barre de vie 
        healthBarTransform.sizeDelta = new Vector2(health / maxHealth * healthbarWidthMax, healthbarHeightMax);
    }
}
