using UnityEngine;
using UnityEngine.UIElements;

public class HeartHealthSystem : MonoBehaviour
{
    [Header("Configuration")]
    public int nombreDeCoeurs = 3;

    // --- C'EST ICI QUE CA MANQUAIT ---
    [Header("Mes Images (Glisse tes sprites ici)")]
    public Sprite imageCoeurPlein;
    public Sprite imageCoeurDemi;
    public Sprite imageCoeurVide;

    [Header("Liaison UI")]
    public UIDocument monDocumentUI;

    private int vieActuelle;
    private VisualElement conteneurCoeurs;

    void Start()
    {
        // On initialise la vie au max (3 coeurs * 2 = 6 PV)
        vieActuelle = nombreDeCoeurs * 2;

        // On vérifie que l'UI est bien assignée
        if (monDocumentUI == null)
        {
            Debug.LogError("ATTENTION : Tu as oublié de glisser le fichier UI Document dans le script !");
            return;
        }

        var racine = monDocumentUI.rootVisualElement;
        // On cherche le conteneur défini dans le UXML
        conteneurCoeurs = racine.Q<VisualElement>("HeartsContainer");

        if (conteneurCoeurs == null)
        {
            Debug.LogError("ATTENTION : Je ne trouve pas 'HeartsContainer' dans ton UXML. Vérifie le nom !");
            return;
        }

        MettreAJourLesCoeurs();
    }

    void Update()
    {
        // Tests
        if (Input.GetKeyDown(KeyCode.K)) Blesser(1);
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
        int maxVie = nombreDeCoeurs * 2;
        if (vieActuelle > maxVie) vieActuelle = maxVie;
        MettreAJourLesCoeurs();
    }

    void MettreAJourLesCoeurs()
    {
        if (conteneurCoeurs == null) return;

        conteneurCoeurs.Clear(); // On vide tout pour redessiner

        for (int i = 0; i < nombreDeCoeurs; i++)
        {
            VisualElement coeur = new VisualElement();
            coeur.AddToClassList("heart"); // On applique la taille via le CSS

            // --- LOGIQUE DES IMAGES ---
            int viePourCeCoeur = vieActuelle - (i * 2);
            Sprite spriteChoisi = null;

            if (viePourCeCoeur >= 2) spriteChoisi = imageCoeurPlein;
            else if (viePourCeCoeur == 1) spriteChoisi = imageCoeurDemi;
            else spriteChoisi = imageCoeurVide;

            // On applique l'image directement sur l'élément UI
            if (spriteChoisi != null)
            {
                coeur.style.backgroundImage = new StyleBackground(spriteChoisi);
            }

            conteneurCoeurs.Add(coeur);
        }
    }
}