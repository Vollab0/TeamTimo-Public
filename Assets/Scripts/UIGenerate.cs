using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UIGenerate : MonoBehaviour
{
    [SerializeField] private MazeGenerator _mazeGenerator; 
    void Start()
    {
        // Récupérer la racine de l'élément visuel de votre document UI
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        // Récupérer le bouton à partir de la racine en utilisant le sélecteur
        Button btnGenerer = root.Q<Button>("BtnGenerer");

        // Ajouter un gestionnaire d'événements au bouton
        btnGenerer.clicked += OnBtnGenererClicked;
    }

    // Méthode appelée lorsque le bouton est cliqué
    void OnBtnGenererClicked()
    {
        // Mettez ici le code que vous souhaitez exécuter lorsque le bouton est cliqué
        _mazeGenerator.Generate();
    }
}
