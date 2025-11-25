using UnityEngine;

[CreateAssetMenu(fileName = "Potion Vitesse", menuName = "Inventaire/Consommable/Potion Vitesse")]
public class ItemVitesse : ItemData
{
    public float multiplicateur = 2f;
    public float duree = 5f;

    public override bool Utiliser(GameObject joueur)
    {
        PlayerController controller = joueur.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.ActiverBoostVitesse(multiplicateur, duree);
            return true; // <--- DISPARAÎT !
        }
        return false;
    }
}