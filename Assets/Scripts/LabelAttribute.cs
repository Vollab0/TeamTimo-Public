using UnityEngine;

/// <summary>
/// Remplace le nom affiché d'un champ dans l'Inspector (ex. un libellé en français pour les game designers),
/// sans renommer le champ sérialisé. Dessiné par LabelAttributeDrawer (Editor).
/// </summary>
public class LabelAttribute : PropertyAttribute
{
    public readonly string Text;

    public LabelAttribute(string text)
    {
        Text = text;
    }
}
