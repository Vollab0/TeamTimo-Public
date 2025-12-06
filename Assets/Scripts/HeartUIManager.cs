using UnityEngine;
using UnityEngine.UIElements;

public class HeartUIManager : MonoBehaviour
{
    [Header("Mes Images")]
    public Sprite imageCoeurPlein;
    public Sprite imageCoeurDemi;
    public Sprite imageCoeurVide;

    [Header("Liaison UI")]
    public UIDocument monDocumentUI;

    private VisualElement conteneurCoeurs;
    private PlayerHealth scriptDuJoueur;

void OnEnable()
    {

        // --- NOUVELLE LIGNE (La version corrigée) ---
        scriptDuJoueur = FindFirstObjectByType<PlayerHealth>();

        if (scriptDuJoueur == null)
        {
            Debug.LogError("Pas de PlayerHealth trouvé dans la scène !");
            return;
        }

        // Le reste ne change pas...
        scriptDuJoueur.OnSanteChangee += MettreAJourLesCoeurs;
        
        var racine = monDocumentUI.rootVisualElement;
        conteneurCoeurs = racine.Q<VisualElement>("HeartsContainer");
    }

    void OnDisable()
    {
        // IMPORTANT : On se désabonne quand l'objet est désactivé pour éviter des erreurs
        if (scriptDuJoueur != null)
        {
            scriptDuJoueur.OnSanteChangee -= MettreAJourLesCoeurs;
        }
    }

    // Cette fonction est appelée automatiquement par le signal du Player
    void MettreAJourLesCoeurs(int vieActuelle)
    {
        if (conteneurCoeurs == null) return;

        conteneurCoeurs.Clear();

        // On récupère le nombre de cœurs max depuis le script du joueur
        int nombreDeCoeurs = scriptDuJoueur.nombreDeCoeurs;

        for (int i = 0; i < nombreDeCoeurs; i++)
        {
            VisualElement coeur = new VisualElement();
            coeur.AddToClassList("heart");

            int viePourCeCoeur = vieActuelle - (i * 2);
            Sprite spriteChoisi = null;

            if (viePourCeCoeur >= 2) spriteChoisi = imageCoeurPlein;
            else if (viePourCeCoeur == 1) spriteChoisi = imageCoeurDemi;
            else spriteChoisi = imageCoeurVide;

            if (spriteChoisi != null)
            {
                coeur.style.backgroundImage = new StyleBackground(spriteChoisi);
            }

            conteneurCoeurs.Add(coeur);
        }
    }
}