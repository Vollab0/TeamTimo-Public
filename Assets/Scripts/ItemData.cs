using UnityEngine;

[CreateAssetMenu(fileName = "Nouvel Objet", menuName = "Inventaire/Item")]
public class ItemData : ScriptableObject
{
    public string nomObjet;
    public Sprite icone; // L'image de l'objet DANS l'inventaire
}