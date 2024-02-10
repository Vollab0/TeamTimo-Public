using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class But_UI : EditorWindow
{
    public void OnEnable()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // Create a label element with ID "textebut" and add it to the root VisualElement
        Label label = new Label();
        label.text = "Initial Text";
        label.name = "testbut"; // Set the name as "textebut"
        root.Add(label);

        root.Q<Label>("testbut").text = "Test Script";

    }
}
