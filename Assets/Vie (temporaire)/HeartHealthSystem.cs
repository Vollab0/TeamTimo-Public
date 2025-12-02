using UnityEngine;
using UnityEngine.UIElements; // Indispensable pour l'UI Toolkit

public class HeartHealthSystem : MonoBehaviour
{
    // --- CONFIGURATION ---
    public int nombreDeCoeurs = 3; // Tu pourras changer ça dans Unity
    private int vieActuelle;

    // --- UI ---
    // C'est ici qu'on glissera le fichier UI Document
    public UIDocument monDocumentUI;

    private VisualElement conteneurCoeurs;

    void Start()
    {
        // Au début du jeu, on a toute la vie
        // 1 coeur = 2 points de vie (Plein + Moitié)
        vieActuelle = nombreDeCoeurs * 2;

        // On cherche la boite "HeartsContainer" qu'on a créée dans le UXML
        var racine = monDocumentUI.rootVisualElement;
        conteneurCoeurs = racine.Q<VisualElement>("HeartsContainer");

        // On dessine les coeurs
        MettreAJourLesCoeurs();
    }

    void Update()
    {
        // --- TESTS (Appuie sur Espace ou H en jeu) ---
        if (Input.GetKeyDown(KeyCode.Space)) Blesser(1);
        if (Input.GetKeyDown(KeyCode.H)) Soigner(1);
    }

    public void Blesser(int degats)
    {
        vieActuelle -= degats;
        if (vieActuelle < 0) vieActuelle = 0;
        MettreAJourLesCoeurs();
    }

    public void Soigner(int soin)
    {
        vieActuelle += soin;
        if (vieActuelle > nombreDeCoeurs * 2) vieActuelle = nombreDeCoeurs * 2;
        MettreAJourLesCoeurs();
    }

    void MettreAJourLesCoeurs()
    {
        if (conteneurCoeurs == null) return;

        conteneurCoeurs.Clear(); // On efface tout pour tout redessiner

        for (int i = 0; i < nombreDeCoeurs; i++)
        {
            // On crée un nouveau carré pour un coeur
            VisualElement nouveauCoeur = new VisualElement();
            nouveauCoeur.AddToClassList("heart"); // On lui donne la taille de base

            // Calcul savant : quel est l'état de ce coeur là ?
            int viePourCeCoeur = vieActuelle - (i * 2);

            if (viePourCeCoeur >= 2)
                nouveauCoeur.AddToClassList("heart-full"); // Plein
            else if (viePourCeCoeur == 1)
                nouveauCoeur.AddToClassList("heart-half"); // Moitié
            else
                nouveauCoeur.AddToClassList("heart-empty"); // Vide

            conteneurCoeurs.Add(nouveauCoeur);
        }
    }
}