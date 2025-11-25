using UnityEngine;

public class ItemData : ScriptableObject
{
    public string nomObjet;
    public Sprite icone;
    public GameObject prefab;

    // Renvoie TRUE si l'objet doit être détruit après usage.
    // Renvoie FALSE si l'objet doit rester en main.
    public virtual bool Utiliser(GameObject joueur)
    {
        Debug.Log("Utilisation générique");
        return false; // Par défaut, un objet ne disparaît pas (ex: un caillou)
    }
}