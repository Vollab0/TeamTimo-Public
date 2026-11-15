using UnityEditor;
using UnityEngine;

/// <summary>Dessine un champ marqué [Label("...")] avec ce libellé (l'infobulle [Tooltip] est conservée).</summary>
[CustomPropertyDrawer(typeof(LabelAttribute))]
public class LabelAttributeDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        label.text = ((LabelAttribute)attribute).Text;
        EditorGUI.PropertyField(position, property, label, true);
    }
}
