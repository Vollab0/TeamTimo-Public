using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Compteur : MonoBehaviour
{
    UIDocument document;
    private int nbr;
    private GameObject obj;

    private void Start()
    {
        obj = GameObject.Find("PlateformeDetection");
        document = GetComponent<UIDocument>();
        
    }
    void Update()
    {
        nbr = obj.GetComponent<CubesDetection>().nbrCubes;
        document.rootVisualElement.Q<Label>("nbr").text = nbr.ToString();
    }
}
