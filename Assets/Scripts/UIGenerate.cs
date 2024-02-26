using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UIGenerate : MonoBehaviour
{
    [SerializeField] private MazeGenerator _mazeGenerator;
    [SerializeField] private GameObject _camera;
    [SerializeField] private GameObject _player;
    [SerializeField] private GameObject _ecranChargement;
    public List<GameObject> mobs;//Pour le test

    void Start()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        Button btnGenerer = root.Q<Button>("BtnGenerer");
        Button btnStart = root.Q<Button>("BtnStart");

        btnGenerer.clicked += OnBtnGenererClicked;
        btnStart.clicked += OnBtnStartClicked;
    }

    void OnBtnGenererClicked()
    {
        _ecranChargement.SetActive(true);
        StartCoroutine(GenerateMazeCoroutine());
    }

    IEnumerator GenerateMazeCoroutine()
    {
        yield return StartCoroutine(_mazeGenerator.Generate());
        _ecranChargement.SetActive(false);
    }

    void OnBtnStartClicked()
    {
        _camera.SetActive(false);
        _player.SetActive(true);
        GetComponent<UIDocument>().gameObject.SetActive(false);

        foreach (var m in mobs)
        {
            m.SetActive(true);
        }

    }
}
