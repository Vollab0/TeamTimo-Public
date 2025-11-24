using UnityEngine;

public class ItemData : ScriptableObject
{
    public string nomObjet;
    public Sprite icone;
    public GameObject prefab;

    // "virtual" signifie : Les enfants peuvent réécrire cette fonction !
    // On demande "target" (le joueur) pour savoir sur qui appliquer l'effet.
    public virtual void ActiverEffet(GameObject joueur)
    {
        Debug.Log($"L'objet {nomObjet} est utilisé, mais il ne fait rien de spécial.");
    }
}