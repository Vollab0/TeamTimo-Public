using UnityEngine;

public class ItemData : ScriptableObject
{
    public string nameObject;
    public Sprite icone;
    public GameObject prefab;

    // Renvoie TRUE si l'objet doit �tre d�truit apr�s usage.
    // Renvoie FALSE si l'objet doit rester en main.
    public virtual bool Utiliser(GameObject joueur)
    {
        Debug.Log("Utilisation g�n�rique");
        return false; // Par d�faut, un objet ne dispara�t pas (ex: un caillou)
    }
}