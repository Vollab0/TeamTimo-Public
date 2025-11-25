using UnityEngine;

[CreateAssetMenu(fileName = "Epee", menuName = "Inventaire/Equipement/Epee")]
public class ItemEpee : ItemData
{
    public int degats = 10;

    public override bool Utiliser(GameObject joueur)
    {
        // Ici, on joue l'animation d'attaque
        Debug.Log($"⚔️ SLASH ! J'attaque avec {nomObjet} et je fais {degats} dégâts.");

        // On pourrait déclencher une animation ici :
        // joueur.GetComponentInChildren<Animator>().SetTrigger("Attaque");

        return false; // <--- NE DISPARAÎT PAS ! Je garde l'épée en main.
    }
}